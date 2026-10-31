using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClipBridge.Core;

// This journal is local-only. Never sync or commit it: it contains source paths.
// An unfinished job is requeued after restart; it is NOT resumed mid-transcode.
public sealed record JournalEntry(
    string Key, string InputPath, long InputLength, DateTime InputModifiedUtc,
    int MaxSizeMb, bool Compress, int Crf, string OutputDirectory,
    string State, string? Error, DateTime UpdatedUtc);

public sealed class JobJournal
{
    private readonly string _filePath;
    private readonly object _gate = new();
    private List<JournalEntry> _entries;

    public JobJournal() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipBridge", "job-history.json")) { }

    internal JobJournal(string filePath)
    {
        _filePath = filePath;
        try
        {
            _entries = File.Exists(_filePath)
                ? JsonSerializer.Deserialize<List<JournalEntry>>(File.ReadAllText(_filePath)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _entries = [];
        }
    }

    public IReadOnlyList<string> RecoverableInputs()
    {
        lock (_gate)
        {
            return _entries
                .Where(x => x.State is "processing" or "failed" or "cancelled")
                .OrderByDescending(x => x.UpdatedUtc)
                .Select(x => x.InputPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(File.Exists)
                .Take(30)
                .ToArray();
        }
    }

    public static string KeyFor(string inputPath, int targetMb, bool compress, int crf)
    {
        var file = new FileInfo(inputPath);
        string fingerprint = string.Join("|",
            Path.GetFullPath(inputPath).ToUpperInvariant(),
            file.Length, file.LastWriteTimeUtc.Ticks, targetMb, compress, crf);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
    }

    public SplitResult? FindCompleted(string inputPath, int targetMb, bool compress, int crf)
    {
        string key = KeyFor(inputPath, targetMb, compress, crf);
        JournalEntry? entry;
        lock (_gate) entry = _entries.LastOrDefault(x => x.Key == key && x.State == "completed");
        if (entry is null || !Directory.Exists(entry.OutputDirectory)) return null;
        string[]? clips = SharedSplitOutput.ReadFiles(entry.OutputDirectory, key);
        long maxBytes = (long)targetMb * 1_000_000L;
        if (clips is null || clips.Length == 0 || clips.Any(x => new FileInfo(x).Length <= 0 || new FileInfo(x).Length > maxBytes))
            return null;
        // A completed job was recorded only after FFmpeg and size validation returned success.
        return new SplitResult(entry.OutputDirectory, clips.Length, true);
    }

    public void SetState(string inputPath, int targetMb, bool compress, int crf,
        string outputDirectory, string state, string? error = null)
    {
        var input = new FileInfo(inputPath);
        string key = KeyFor(inputPath, targetMb, compress, crf);
        lock (_gate)
        {
            _entries.RemoveAll(x => x.Key == key);
            _entries.Add(new JournalEntry(key, input.FullName, input.Length, input.LastWriteTimeUtc,
                targetMb, compress, crf, outputDirectory, state, error, DateTime.UtcNow));
            if (_entries.Count > 100) _entries = _entries.OrderByDescending(x => x.UpdatedUtc).Take(100).ToList();
            Save();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        string temporary = _filePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_entries,
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, _filePath, true);
    }
}
