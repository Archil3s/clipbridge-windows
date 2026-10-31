using System.IO;

namespace ClipBridge.Core;

internal static class VideoFileDiscovery
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".webm"
    };

    public static bool IsSupported(string path) => Extensions.Contains(Path.GetExtension(path));

    public static IReadOnlyList<string> Collect(IEnumerable<string> inputs, bool recursive, CancellationToken ct)
    {
        var videos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string input in inputs)
        {
            ct.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(input);
            if (File.Exists(path))
            {
                if (IsSupported(path)) videos.Add(path);
                continue;
            }
            if (!Directory.Exists(path)) continue;
            var pending = new Stack<string>();
            pending.Push(path);
            while (pending.TryPop(out string? folder))
            {
                ct.ThrowIfCancellationRequested();
                if (!visited.Add(folder)) continue;
                foreach (string file in Directory.EnumerateFiles(folder))
                {
                    ct.ThrowIfCancellationRequested();
                    if (IsSupported(file)) videos.Add(Path.GetFullPath(file));
                }
                if (!recursive) continue;
                foreach (string child in Directory.EnumerateDirectories(folder))
                {
                    ct.ThrowIfCancellationRequested();
                    // Cloud placeholders may be reparse points without being directory links.
                    if (new DirectoryInfo(child).LinkTarget is null) pending.Push(child);
                }
            }
        }
        return videos.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
