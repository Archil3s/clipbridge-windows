using System.IO;
using System.Text.Json;

namespace ClipBridge.Core;

internal sealed class SharedSplitOutput
{
    private const string StateFolder = ".clipbridge-state";
    private int _next;
    public string DirectoryPath { get; }
    public string WorkDirectory { get; }

    public SharedSplitOutput(string output)
    {
        DirectoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(output));
        Directory.CreateDirectory(DirectoryPath);
        _next = checked(Directory.EnumerateFiles(DirectoryPath, "part_*.*")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name!.Length > 5 && int.TryParse(name.AsSpan(5), out int number) ? number : 0)
            .DefaultIfEmpty(0).Max() + 1);
        WorkDirectory = Path.Combine(DirectoryPath, ".clipbridge-work-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(WorkDirectory);
        File.SetAttributes(WorkDirectory, File.GetAttributes(WorkDirectory) | FileAttributes.Hidden);
    }

    public async Task<string[]> CollectAsync(string[] clips, string key, bool preserveSource, CancellationToken ct)
    {
        if (clips.Length == 0) throw new InvalidDataException("No validated clips are available to collect.");
        var collected = new List<string>();
        var moves = new List<(string Source, string Destination)>();
        var pendingFiles = new List<string>();
        try
        {
            foreach (string clip in clips)
            {
                ct.ThrowIfCancellationRequested();
                var source = new FileInfo(clip);
                if (!source.Exists || source.Length == 0) throw new InvalidDataException("A prepared clip is missing or empty.");
                if (preserveSource && string.Equals(Path.GetDirectoryName(source.FullName), DirectoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    collected.Add(source.FullName);
                    continue;
                }
                string destination = Path.Combine(DirectoryPath, $"part_{_next++:D4}{source.Extension}");
                if (preserveSource)
                {
                    string temporary = Path.Combine(WorkDirectory, Guid.NewGuid().ToString("N") + ".pending");
                    pendingFiles.Add(temporary);
                    await using (var input = new FileStream(source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true))
                    await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
                        await input.CopyToAsync(output, ct);
                    ct.ThrowIfCancellationRequested();
                    if (new FileInfo(temporary).Length != source.Length) throw new IOException("Clip changed while collecting output.");
                    moves.Add((temporary, destination));
                }
                else
                {
                    if (!source.FullName.StartsWith(WorkDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Only this run's temporary split files can be moved.");
                    moves.Add((source.FullName, destination));
                }
                collected.Add(destination);
            }
            ct.ThrowIfCancellationRequested();
            foreach (var move in moves) File.Move(move.Source, move.Destination);
            string state = Path.Combine(DirectoryPath, StateFolder);
            Directory.CreateDirectory(state);
            File.SetAttributes(state, File.GetAttributes(state) | FileAttributes.Hidden);
            string manifest = Path.Combine(state, key + ".json");
            string pending = manifest + ".tmp";
            File.WriteAllText(pending, JsonSerializer.Serialize(collected.Select(path => new ClipEntry(Path.GetFileName(path), new FileInfo(path).Length))));
            File.Move(pending, manifest, true);
            return collected.ToArray();
        }
        finally
        {
            foreach (string temporary in pendingFiles) if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static string[]? ReadFiles(string output, string key)
    {
        if (!Directory.Exists(output)) return null;
        string state = Path.Combine(output, StateFolder);
        if (!Directory.Exists(state))
            return Directory.GetFiles(output, "part_*.*").Where(VideoFileDiscovery.IsSupported).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        try
        {
            string manifest = Path.Combine(state, key + ".json");
            if (!File.Exists(manifest)) return null;
            var entries = JsonSerializer.Deserialize<ClipEntry[]>(File.ReadAllText(manifest));
            if (entries is null || entries.Length == 0 || entries.Any(entry => string.IsNullOrWhiteSpace(entry.Name) ||
                Path.GetFileName(entry.Name) != entry.Name || !entry.Name.StartsWith("part_", StringComparison.OrdinalIgnoreCase) ||
                !VideoFileDiscovery.IsSupported(entry.Name) || entry.Bytes <= 0) ||
                entries.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length) return null;
            var paths = entries.Select(entry => Path.Combine(output, entry.Name)).ToArray();
            return paths.Where((path, index) => !File.Exists(path) || new FileInfo(path).Length != entries[index].Bytes).Any() ? null : paths;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    public bool RemoveEmptyWorkDirectories(bool completed = false)
    {
        try
        {
            foreach (string child in Directory.EnumerateDirectories(WorkDirectory))
                if (Path.GetFullPath(child).StartsWith(WorkDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    !Directory.EnumerateFileSystemEntries(child).Any()) Directory.Delete(child);
            if (completed)
            {
                if (Directory.EnumerateFileSystemEntries(WorkDirectory).Any()) return false;
                Directory.Delete(WorkDirectory);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private sealed record ClipEntry(string Name, long Bytes);
}
