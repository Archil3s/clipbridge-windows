using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ClipBridge.Core;

public sealed record SplitResult(string OutputDirectory, int Parts, bool AllWithinLimit);

/// <summary>
/// Lossless (stream-copy) video splitter. Uses the FFmpeg segment muxer so
/// subsequent clips begin at available keyframes. Output size is validated;
/// a failed size check is never presented as success.
/// </summary>
public sealed class VideoSplitter
{
    private readonly string _ffmpeg = FindTool("ffmpeg.exe");
    private readonly string _ffprobe = FindTool("ffprobe.exe");

    public async Task VerifyToolsAsync(CancellationToken cancellationToken)
    {
        await RunProcessAsync(_ffmpeg, ["-version"], cancellationToken);
        await RunProcessAsync(_ffprobe, ["-version"], cancellationToken);
    }

    public async Task<SplitResult> SplitAsync(string inputPath, string outputRoot, int targetMb,
        bool compress, int crf, CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath)) throw new FileNotFoundException("Video file not found.", inputPath);
        if (crf < 18 || crf > 36) throw new ArgumentOutOfRangeException(nameof(crf));
        // 1 MB = 1,000,000 bytes. Allow a margin for muxing overhead.
        long hardLimit = (long)targetMb * 1_000_000L;
        long planningLimit = hardLimit * 80 / 100;

        string safeName = Path.GetFileNameWithoutExtension(inputPath);
        foreach (char invalid in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(invalid, '_');
        string outputDirectory = Path.Combine(outputRoot,
            $"{safeName}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N")[..6]}");
        Directory.CreateDirectory(outputDirectory);

        string workingFile = inputPath;
        string? intermediate = null;
        try
        {
            if (compress)
            {
                intermediate = Path.Combine(outputDirectory, "_compressed_working_file.mp4");
                // Re-encode once. After that, use the fast segmenter to cut the
                // smaller compressed file, without recompressing every segment.
                await RunProcessAsync(_ffmpeg,
                [
                    "-hide_banner", "-nostdin", "-y", "-loglevel", "error",
                    "-i", inputPath,
                    "-map", "0:v:0", "-map", "0:a?", "-sn", "-dn",
                    "-c:v", "libx264", "-preset", "veryfast",
                    "-crf", crf.ToString(CultureInfo.InvariantCulture),
                    "-pix_fmt", "yuv420p",
                    "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart",
                    intermediate
                ], cancellationToken);
                workingFile = intermediate;
            }

            var file = new FileInfo(workingFile);
            if (file.Length <= 0)
                throw new InvalidOperationException("Output video has zero bytes.");

        if (file.Length <= hardLimit)
        {
            string destination = Path.Combine(outputDirectory, "part_0001" + (compress ? ".mp4" : file.Extension));
            await using (var source = new FileStream(workingFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 1024 * 1024, FileOptions.Asynchronous))
                await source.CopyToAsync(target, cancellationToken);
            return new SplitResult(outputDirectory, 1, true);
        }

        (double duration, long size) = await ProbeAsync(workingFile, cancellationToken);
        if (duration <= 0 || !double.IsFinite(duration))
            throw new InvalidOperationException("FFprobe could not determine the video duration.");
        double bytesPerSecond = Math.Max(1.0, size / duration);
        double initialSegmentSeconds = Math.Max(0.25, planningLimit / bytesPerSecond);
        string outputPattern = Path.Combine(outputDirectory, "part_%04d.mp4");

        // Variable bitrate footage may need shorter time segments. Retry
        // with smaller durations and verify output size after FFmpeg exits.
        // Sparse keyframes can still prevent an exact bound in stream-copy mode.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string previous in Directory.EnumerateFiles(outputDirectory, "part_*.mp4"))
                File.Delete(previous);

            double segmentSeconds = initialSegmentSeconds * Math.Pow(0.62, attempt);
            segmentSeconds = Math.Max(0.15, segmentSeconds);
            string length = segmentSeconds.ToString("0.###", CultureInfo.InvariantCulture);

            await RunProcessAsync(_ffmpeg,
            [
                "-hide_banner", "-nostdin", "-y", "-loglevel", "error",
                "-i", workingFile,
                "-map", "0:v:0?", "-map", "0:a?", "-sn", "-dn",
                "-c", "copy", "-f", "segment", "-segment_format", "mp4",
                "-segment_time", length, "-segment_start_number", "1", "-reset_timestamps", "1",
                outputPattern
            ], cancellationToken);

            FileInfo[] segments = new DirectoryInfo(outputDirectory).GetFiles("part_*.mp4")
                .OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
            if (segments.Length == 0)
                throw new InvalidOperationException("FFmpeg did not produce any video segments.");
            if (segments.All(x => x.Length > 0 && x.Length <= hardLimit))
                return new SplitResult(outputDirectory, segments.Length, true);
        }

        FileInfo[] finalSegments = new DirectoryInfo(outputDirectory).GetFiles("part_*.mp4");
        return new SplitResult(outputDirectory, finalSegments.Length, false);
        }
        finally
        {
            // Keep the original and output clips; remove the temporary full-length transcode.
            if (intermediate is not null && File.Exists(intermediate))
            {
                try { File.Delete(intermediate); } catch (IOException) { /* Keep visible until next cleanup. */ }
            }
        }
    }

    private async Task<(double duration, long size)> ProbeAsync(string inputPath, CancellationToken ct)
    {
        string output = await RunProcessAsync(_ffprobe,
            ["-v", "error", "-show_entries", "format=duration,size", "-of", "json", inputPath], ct);
        using JsonDocument doc = JsonDocument.Parse(output);
        JsonElement format = doc.RootElement.GetProperty("format");
        string durationString = format.GetProperty("duration").GetString() ?? "0";
        string sizeString = format.GetProperty("size").GetString() ?? "0";
        if (!double.TryParse(durationString, NumberStyles.Float, CultureInfo.InvariantCulture, out double duration) ||
            !long.TryParse(sizeString, NumberStyles.Integer, CultureInfo.InvariantCulture, out long size))
            throw new InvalidOperationException("FFprobe returned invalid video metadata.");
        return (duration, size);
    }

    private static string FindTool(string exe)
    {
        string bundled = Path.Combine(AppContext.BaseDirectory, exe);
        if (File.Exists(bundled)) return bundled;
        string installed = Path.Combine(DependencyInstaller.ToolsDirectory, exe);
        if (File.Exists(installed)) return installed;
        return Path.GetFileNameWithoutExtension(exe);
    }

    private static async Task<string> RunProcessAsync(string executable,
        IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new InvalidOperationException($"Could not start {executable}.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot find {executable}. Install FFmpeg + FFprobe, add them to PATH, " +
                "or place both executables beside ClipBridge.exe.", ex);
        }

        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{Path.GetFileName(executable)} failed (exit {process.ExitCode}): " +
                (string.IsNullOrWhiteSpace(stderr) ? "Unknown error" : stderr.Trim()));
        return stdout;
    }
}
