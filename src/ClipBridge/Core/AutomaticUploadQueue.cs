using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClipBridge.Core;

public sealed record AutomaticClip(string Path, string Fingerprint, string State = "Ready")
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string Display => $"{State}  |  {Name}";
}

public sealed record AutomaticUploadSettings
{
    public string ConversationUrl { get; init; } = "https://chatgpt.com/";
    public int BatchSize { get; init; } = 3;
    public string Prompt { get; init; } = "Gardening B-roll clips for reels. Please wait for my instructions.";
    public List<AutomaticClip> Clips { get; init; } = [];
}

public sealed class AutomaticUploadQueue : IDisposable
{
    public const long MaximumClipBytes = 525_000_000;
    public const long MaximumBatchBytes = 1_000_000_000;
    private readonly string _path;
    private readonly FileStream _lock;
    public AutomaticUploadSettings Settings { get; private set; }
    public IReadOnlyList<AutomaticClip> Clips => Settings.Clips;
    public static string Folder => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipBridge");

    public AutomaticUploadQueue(string? path = null)
    {
        _path = path ?? System.IO.Path.Combine(Folder, "automatic-uploads.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        _lock = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            Settings = File.Exists(_path)
                ? JsonSerializer.Deserialize<AutomaticUploadSettings>(File.ReadAllText(_path))
                    ?? throw new InvalidDataException("The saved upload queue is empty or damaged.")
                : new AutomaticUploadSettings();
            if (Settings.Clips is null || Settings.Clips.Any(clip => clip is null ||
                string.IsNullOrWhiteSpace(clip.Path) || string.IsNullOrWhiteSpace(clip.Fingerprint) ||
                clip.State is not ("Ready" or "Attachment attempted" or "Send attempted" or "Submitted")))
                throw new InvalidDataException("The saved upload queue is damaged. Preserve it before repairing it.");
        }
        catch { _lock.Dispose(); throw; }
    }

    public static string Fingerprint(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length <= 0 || file.Length > MaximumClipBytes ||
            !new[] { ".mp4", ".mov", ".m4v", ".webm" }.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException($"{file.Name}: choose a nonempty MP4/MOV/M4V/WebM clip up to 525 MB.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            file.FullName.ToUpperInvariant() + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks)));
    }

    public int Add(IEnumerable<string> paths)
    {
        var clips = Settings.Clips.ToList();
        int added = 0;
        foreach (string path in paths)
        {
            string fullPath = System.IO.Path.GetFullPath(path);
            string fingerprint = Fingerprint(fullPath);
            if (clips.Any(clip => clip.Fingerprint == fingerprint)) continue;
            clips.Add(new AutomaticClip(fullPath, fingerprint));
            added++;
        }
        Commit(Settings with { Clips = clips });
        return added;
    }

    public void Configure(string url, int count, string prompt)
    {
        if (count is < 1 or > 10 || string.IsNullOrWhiteSpace(prompt) || prompt.Length > 1000)
            throw new ArgumentException("Choose 1 to 10 clips per batch and a message of 1 to 1000 characters.");
        Commit(Settings with { ConversationUrl = ValidateUrl(url), BatchSize = count, Prompt = prompt.Trim() });
    }

    public static string ValidateUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.Host != "chatgpt.com" || !uri.IsDefaultPort ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            !(uri.AbsolutePath == "/" || System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath, @"^/c/[a-zA-Z0-9-]+/?$")))
            throw new ArgumentException("Use https://chatgpt.com/ or a chatgpt.com/c/ conversation link.");
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    internal static string BrowserConversationUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new ArgumentException("The browser has no supported conversation address.");
        return ValidateUrl(uri.GetLeftPart(UriPartial.Path));
    }

    internal static bool BrowserMatches(string value, string target)
    {
        try { return BrowserConversationUrl(value) == ValidateUrl(target); }
        catch (ArgumentException) { return false; }
    }

    public IReadOnlyList<AutomaticClip> NextBatch(int count)
    {
        if (count is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(count));
        var result = new List<AutomaticClip>();
        long bytes = 0;
        foreach (var clip in Clips.Where(clip => clip.State == "Ready").Take(count))
        {
            if (Fingerprint(clip.Path) != clip.Fingerprint)
                throw new IOException($"{clip.Name} changed. Remove that queue entry and add the clip again.");
            long size = new FileInfo(clip.Path).Length;
            if (bytes + size > MaximumBatchBytes) break;
            bytes += size;
            result.Add(clip);
        }
        return result;
    }

    public void Transition(IReadOnlyList<AutomaticClip> batch, string from, string to)
    {
        if (batch.Count == 0 || batch.Any(item => !Clips.Any(clip => clip.Fingerprint == item.Fingerprint && clip.State == from)))
            throw new InvalidOperationException("Upload queue changed; review it before continuing.");
        var ids = batch.Select(item => item.Fingerprint).ToHashSet();
        Commit(Settings with { Clips = Clips.Select(clip => ids.Contains(clip.Fingerprint) ? clip with { State = to } : clip).ToList() });
    }

    public void Remove(IEnumerable<AutomaticClip> selected)
    {
        var ids = selected.Select(item => item.Fingerprint).ToHashSet();
        Commit(Settings with { Clips = Clips.Where(clip => !ids.Contains(clip.Fingerprint)).ToList() });
    }

    public void ConfirmSubmitted(IReadOnlyList<AutomaticClip> batch, string conversationUrl)
    {
        string url = ValidateUrl(conversationUrl);
        if (!url.StartsWith("https://chatgpt.com/c/", StringComparison.Ordinal))
            throw new InvalidOperationException("The sent batch has no saved conversation address yet. Review it before continuing.");
        if (batch.Count == 0 || batch.Any(item => !Clips.Any(clip =>
            clip.Fingerprint == item.Fingerprint && clip.State == "Send attempted")))
            throw new InvalidOperationException("Upload queue changed; review it before continuing.");
        var ids = batch.Select(item => item.Fingerprint).ToHashSet();
        Commit(Settings with { ConversationUrl = url,
            Clips = Clips.Select(clip => ids.Contains(clip.Fingerprint) ? clip with { State = "Submitted" } : clip).ToList() });
    }

    public void RetryReviewed(IEnumerable<AutomaticClip> selected)
    {
        var ids = selected.Select(item => item.Fingerprint).ToHashSet();
        Commit(Settings with { Clips = Clips.Select(clip => ids.Contains(clip.Fingerprint) ? clip with { State = "Ready" } : clip).ToList() });
    }

    private void Commit(AutomaticUploadSettings next)
    {
        string temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, _path, overwrite: true);
        Settings = next;
    }

    public void Dispose() => _lock.Dispose();
}
