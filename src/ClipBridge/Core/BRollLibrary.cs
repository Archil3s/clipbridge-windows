using System.IO;
using System.Text.Json;

namespace ClipBridge.Core;

public sealed record BRollClipMetadata(
    string Title, string SourceFileName, string Category, List<string> Tags,
    double StartSeconds, double EndSeconds, DateTime SavedUtc);

public sealed record BRollLibraryItem(
    string FilePath, string Title, string Category, IReadOnlyList<string> Tags)
{
    public string DisplayText =>
        $"{Title}  ·  {Category}  ·  {(Tags.Count > 0 ? string.Join(", ", Tags) : "No tags")}";
}

/// <summary>
/// Saves gardening tags alongside local MP4 files. Clip metadata is never uploaded.
/// When filters are selected, an item matches if it has any selected tag.
/// </summary>
public static class BRollLibrary
{
    public static readonly string[] GardeningTags =
    [
        "Planting", "Harvesting", "Watering", "Compost & Soil", "Pruning",
        "Seedlings", "Tools", "Wildlife", "Weeding", "Garden Overview"
    ];

    public static async Task SaveMetadataAsync(
        BRollClip clip, string sourceFileName, string title,
        IEnumerable<string> selectedTags, CancellationToken cancellationToken)
    {
        var tags = selectedTags
            .Where(tag => GardeningTags.Contains(tag, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var metadata = new BRollClipMetadata(
            title.Trim(), Path.GetFileName(sourceFileName), clip.Category, tags,
            clip.Start.TotalSeconds, clip.End.TotalSeconds, DateTime.UtcNow);
        string destination = Path.ChangeExtension(clip.FilePath, ".broll.json");
        string staging = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(staging, json, cancellationToken);
            File.Move(staging, destination, overwrite: false);
        }
        finally
        {
            if (File.Exists(staging))
                try { File.Delete(staging); }
                catch (IOException) { }
        }
    }

    public static IReadOnlyList<BRollLibraryItem> FindClips(
        string libraryRoot, IEnumerable<string> checkedFilters)
    {
        if (!Directory.Exists(libraryRoot)) return [];
        var filters = checkedFilters
            .Where(tag => GardeningTags.Contains(tag, StringComparer.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var results = new List<BRollLibraryItem>();
        foreach (string path in Directory.EnumerateFiles(
            libraryRoot, "*.mp4", SearchOption.AllDirectories))
        {
            BRollClipMetadata? metadata = null;
            string sidecar = Path.ChangeExtension(path, ".broll.json");
            if (File.Exists(sidecar))
            {
                try
                {
                    metadata = JsonSerializer.Deserialize<BRollClipMetadata>(File.ReadAllText(sidecar));
                }
                catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
                {
                    // A damaged tag sidecar does not hide the corresponding video.
                }
            }
            var tags = (metadata?.Tags ?? [])
                .Where(tag => GardeningTags.Contains(tag, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (filters.Count > 0 && !tags.Any(filters.Contains)) continue;
            string folder = Path.GetFileName(Path.GetDirectoryName(path));
            results.Add(new BRollLibraryItem(
                path,
                metadata?.Title is { Length: > 0 } title
                    ? title : Path.GetFileNameWithoutExtension(path),
                metadata?.Category ?? folder, tags));
        }
        return results
            .OrderByDescending(item => File.GetLastWriteTimeUtc(item.FilePath))
            .ToList();
    }
}
