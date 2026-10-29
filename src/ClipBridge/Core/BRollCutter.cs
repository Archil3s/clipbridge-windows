using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace ClipBridge.Core;

public sealed record BRollClip(string FilePath, string Category, TimeSpan Start, TimeSpan End);

/// <summary>
/// Makes exact, independent MP4 excerpts for a reusable local B-roll library.
/// Does not change the source file or send footage to a cloud service.
/// </summary>
public sealed class BRollCutter
{
    private readonly string _ffmpeg = FindTool("ffmpeg.exe");
    private readonly string _ffprobe = FindTool("ffprobe.exe");

    public static string DefaultLibraryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "ClipBridge", "B-Roll");

    public static string SanitizeLabel(string? value, string fallback = "Unsorted")
    {
        var result = new StringBuilder();
        foreach (char ch in (value ?? "").Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ')
                result.Append(ch);
            else if (ch is '.' or '&')
                result.Append('_');
        }
        string cleaned = result.ToString().Trim(' ', '.');
        if (cleaned.Length > 45) cleaned = cleaned[..45].Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) return fallback;

        // Windows reserved device names cannot be used as directories.
        string name = cleaned.Split('.')[0].Trim().ToUpperInvariant();
        string[] devices = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4",
            "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3",
            "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
        return devices.Contains(name) ? "_" + cleaned : cleaned;
    }

    public async Task<TimeSpan> GetDurationAsync(string input, CancellationToken ct)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("Video not found.", input);
        string result = await RunCaptureAsync(_ffprobe,
            ["-v", "error", "-show_entries", "format=duration",
             "-of", "default=noprint_wrappers=1:nokey=1", input], ct);
        if (!double.TryParse(result.Trim(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double seconds) ||
            !double.IsFinite(seconds) || seconds <= 0)
            throw new InvalidOperationException("Cannot read video duration with FFprobe.");
        return TimeSpan.FromSeconds(seconds);
    }

    public async Task<BRollClip> CutAsync(
        string input, string libraryRoot, string category, string title,
        TimeSpan start, TimeSpan end, IProgress<double>? progress, CancellationToken ct)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("Video not found.", input);
        if (string.IsNullOrWhiteSpace(libraryRoot))
            throw new ArgumentException("Choose a B-roll library folder.", nameof(libraryRoot));

        TimeSpan sourceDuration = await GetDurationAsync(input, ct);
        if (start < TimeSpan.Zero || end <= start || end > sourceDuration + TimeSpan.FromMilliseconds(250))
            throw new ArgumentException("Start and end must define a valid range inside the source video.");
        TimeSpan length = end - start;
        if (length.TotalSeconds < .2)
            throw new ArgumentException("Choose a clip at least 0.2 seconds long.");

        string safeCategory = SanitizeLabel(category);
        string safeTitle = SanitizeLabel(title, "B-Roll");
        string sourceName = SanitizeLabel(Path.GetFileNameWithoutExtension(input), "Video");
        string destinationFolder = Path.Combine(Path.GetFullPath(libraryRoot), safeCategory);
        Directory.CreateDirectory(destinationFolder);
        string stamp = start.ToString(@"hh\mm\ss", CultureInfo.InvariantCulture) +
            "-" + end.ToString(@"hh\mm\ss", CultureInfo.InvariantCulture);
        string name = $"{safeTitle}_{sourceName}_{stamp}_{Guid.NewGuid():N}"[..Math.Min(
            150, ($"{safeTitle}_{sourceName}_{stamp}_{Guid.NewGuid():N}").Length)];
        string destination = Path.Combine(destinationFolder, name + ".mp4");
        string temporary = Path.Combine(destinationFolder, name + ".partial.mp4");

        var arguments = new List<string>
        {
            "-hide_banner", "-nostdin", "-loglevel", "error", "-y",
            "-ss", start.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", input,
            "-t", length.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            "-map", "0:v:0", "-map", "0:a?", "-sn", "-dn",
            "-c:v", "libx264", "-preset", "fast", "-crf", "21",
            "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "160k",
            "-movflags", "+faststart",
            "-progress", "pipe:1", "-nostats", temporary
        };

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpeg, UseShellExecute = false,
                    CreateNoWindow = true, RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            if (!process.Start()) throw new IOException("FFmpeg did not start.");

            using var cancellation = ct.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            });
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(ct);
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(ct)) is not null)
            {
                if (!line.StartsWith("out_time=", StringComparison.Ordinal)) continue;
                if (!TimeSpan.TryParse(line["out_time=".Length..],
                    CultureInfo.InvariantCulture, out TimeSpan processed)) continue;
                progress?.Report(Math.Clamp(processed.TotalSeconds / length.TotalSeconds * 100, 0, 99));
            }
            await process.WaitForExitAsync(ct);
            ct.ThrowIfCancellationRequested();
            string stderr = await stderrTask;
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Could not export this B-roll clip: {stderr.Trim()}");

            if (!File.Exists(temporary) || new FileInfo(temporary).Length <= 0)
                throw new InvalidOperationException("FFmpeg produced an empty B-roll clip.");
            TimeSpan actualDuration = await GetDurationAsync(temporary, ct);
            if (Math.Abs((actualDuration - length).TotalSeconds) >
                Math.Max(0.75, length.TotalSeconds * 0.1))
                throw new InvalidOperationException(
                    "Exported clip duration failed validation. Try another start/end range.");

            ct.ThrowIfCancellationRequested();
            File.Move(temporary, destination);
            progress?.Report(100);
            return new BRollClip(destination, safeCategory, start, end);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try { File.Delete(temporary); }
                catch (IOException) { /* Leave failed output visibly marked partial. */ }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static string FindTool(string fileName)
    {
        string besideApp = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(besideApp)) return besideApp;
        string installed = Path.Combine(DependencyInstaller.ToolsDirectory, fileName);
        return File.Exists(installed) ? installed : Path.GetFileNameWithoutExtension(fileName);
    }

    private static async Task<string> RunCaptureAsync(string exe, IReadOnlyList<string> args, CancellationToken ct)
    {
        var info = new ProcessStartInfo
        {
            FileName = exe, CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var a in args) info.ArgumentList.Add(a);
        using var p = new Process { StartInfo = info };
        if (!p.Start()) throw new IOException($"Could not launch {exe}.");
        using var cancellation = ct.Register(() =>
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });
        Task<string> stdout = p.StandardOutput.ReadToEndAsync(ct);
        Task<string> stderr = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        ct.ThrowIfCancellationRequested();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"FFprobe could not inspect video: {await stderr}");
        return await stdout;
    }
}
