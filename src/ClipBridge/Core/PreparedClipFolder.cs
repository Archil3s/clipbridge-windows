using System.IO;

namespace ClipBridge.Core;

internal static class PreparedClipFolder
{
    public static string[] Read(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            throw new DirectoryNotFoundException("Choose an existing clip folder or paste its path into Clip folder.");
        string[] extensions = [".mp4", ".mov", ".m4v", ".webm"];
        return Directory.EnumerateFiles(folder)
            .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
