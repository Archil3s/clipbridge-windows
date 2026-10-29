using System.IO;
using System.Text.Json;

namespace ClipBridge.Core;

public sealed class LibrarySettings(string path)
{
    public string? ReadRoot()
    {
        if (!File.Exists(path)) return null;
        string? root = JsonSerializer.Deserialize<string>(File.ReadAllText(path));
        return string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root);
    }

    public void SaveRoot(string root)
    {
        string fullPath = Path.GetFullPath(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(fullPath));
        File.Move(temporary, path, overwrite: true);
    }
}
