using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace ClipBridge.Core;

/// <summary>
/// First-run installer for third-party FFmpeg binaries. Files are downloaded
/// directly by the user's PC from gyan.dev, a Windows build provider linked by
/// ffmpeg.org. The fixed release archive is SHA-256 verified before extraction.
/// Neither videos nor login details are transmitted to this provider.
/// </summary>
public static class DependencyInstaller
{
    private const string DownloadUrl =
        "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip";
    private const string ExpectedSha256 =
        "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba";

    public static string ToolsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClipBridge", "Tools");

    public static bool Installed =>
        (File.Exists(Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe")) &&
        File.Exists(Path.Combine(AppContext.BaseDirectory, "ffprobe.exe"))) ||
        File.Exists(Path.Combine(ToolsDirectory, "ffmpeg.exe")) &&
        File.Exists(Path.Combine(ToolsDirectory, "ffprobe.exe"));

    public static async Task EnsureInstalledAsync(IProgress<string>? progress, CancellationToken ct)
    {
        if (Installed)
        {
            progress?.Report("Video engine ready.");
            return;
        }

        // An existing installed or portable copy may already have the tools.
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe")) &&
            File.Exists(Path.Combine(AppContext.BaseDirectory, "ffprobe.exe")))
        {
            progress?.Report("Using FFmpeg included beside the application.");
            return;
        }

        Directory.CreateDirectory(ToolsDirectory);
        string staging = Path.Combine(
            Path.GetTempPath(), "ClipBridge-Setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        string archivePath = Path.Combine(staging, "ffmpeg.zip");
        try
        {
            progress?.Report("Downloading the video engine (one-time setup, about 110 MB)...");
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
            using HttpResponseMessage response = await client.GetAsync(
                DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long? expectedLength = response.Content.Headers.ContentLength;
            long received = 0;
            await using (Stream source = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[1024 * 1024];
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    received += read;
                    if (expectedLength > 0)
                        progress?.Report($"Downloading FFmpeg: {(received * 100.0 / expectedLength):0}%");
                    else
                        progress?.Report($"Downloading FFmpeg: {received / 1_000_000.0:0} MB");
                }
            }

            progress?.Report("Verifying downloaded video engine...");
            await using (var zipStream = File.OpenRead(archivePath))
            {
                string actual = Convert.ToHexString(await SHA256.HashDataAsync(zipStream, ct));
                if (!string.Equals(actual, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "FFmpeg download failed its SHA-256 security check. No tools were installed.");
            }

            progress?.Report("Installing FFmpeg and FFprobe locally...");
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                foreach (string name in new[] { "ffmpeg.exe", "ffprobe.exe" })
                {
                    ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(e =>
                        string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (entry is null)
                        throw new InvalidDataException($"Downloaded FFmpeg archive is missing {name}.");

                    string destination = Path.Combine(staging, name);
                    await using Stream source = entry.Open();
                    await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
                        FileShare.None, 1024 * 1024, FileOptions.Asynchronous);
                    await source.CopyToAsync(output, ct);
                    if (new FileInfo(destination).Length == 0)
                        throw new InvalidDataException($"{name} extracted as an empty file.");
                }
            }

            ct.ThrowIfCancellationRequested();
            // Files live in a per-user directory; no system PATH edits or admin rights.
            foreach (string name in new[] { "ffmpeg.exe", "ffprobe.exe" })
                File.Copy(Path.Combine(staging, name), Path.Combine(ToolsDirectory, name), true);

            if (!Installed)
                throw new IOException("The video engine was not installed completely.");
            progress?.Report("Video engine installed. Ready to split videos.");
        }
        finally
        {
            try { Directory.Delete(staging, recursive: true); }
            catch (IOException) { /* Windows may briefly hold archive handles. */ }
            catch (UnauthorizedAccessException) { /* Cleanup will be retried by OS. */ }
        }
    }
}
