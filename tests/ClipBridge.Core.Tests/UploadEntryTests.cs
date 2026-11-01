using System.Diagnostics;
using ClipBridge.Core;

internal static class UploadEntryTests
{
    internal static async Task RunAsync(string root)
    {
        string folder = Path.Combine(root, "upload-entry");
        Directory.CreateDirectory(folder);
        for (int i = 1; i <= 208; i++)
            await File.WriteAllBytesAsync(Path.Combine(folder, $"part_{i:D4}.MP4"), [1, 2, 3]);
        await File.WriteAllTextAsync(Path.Combine(folder, "tags.json"), "{}");
        Directory.CreateDirectory(Path.Combine(folder, "nested"));
        await File.WriteAllBytesAsync(Path.Combine(folder, "nested", "not-selected.mp4"), [1]);
        string[] files = PreparedClipFolder.Read(folder);
        Check(files.Length == 208 && Path.GetFileName(files[0]) == "part_0001.MP4",
            "direct folder import finds all 208 top-level clips, ignoring metadata and nested folders");
        string queuePath = Path.Combine(root, "upload-entry-queue.json");
        using (var queue = new AutomaticUploadQueue(queuePath))
        {
            queue.Add(files.Take(1));
            var first = queue.NextBatch(1);
            queue.Transition(first, "Ready", "Send attempted");
            queue.ConfirmSubmitted(first, "https://chatgpt.com/c/fixture");
            Check(queue.Add(files) == 207 && queue.Clips.Count == 208 && queue.Clips[0].State == "Submitted",
                "loading a folder preserves submitted states and adds remaining clips without duplicates");
        }
        using (var restored = new AutomaticUploadQueue(queuePath))
            Check(restored.Clips.Count == 208 && restored.Clips.Count(clip => clip.State == "Ready") == 207,
                "imported folder clips and their states survive queue reload");
        bool missing = false;
        try { PreparedClipFolder.Read(Path.Combine(root, "missing-folder")); }
        catch (DirectoryNotFoundException) { missing = true; }
        Check(missing, "missing import folder reports an error instead of a false zero-clip success");

        string profile = Path.Combine(root, "exited-launch-profile");
        var debug = new UploadDebugLog();
        using var session = new BraveAutomationSession(profile, "fixture-browser", _ => Process.Start(new ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/c", "exit", "0" }, UseShellExecute = false, CreateNoWindow = true
        }));
        session.DebugLog = debug;
        var clock = Stopwatch.StartNew();
        bool failed = false;
        try { await session.OpenAsync("https://chatgpt.com/", default); }
        catch (IOException ex) { failed = ex.Message.Contains("sign-in browser", StringComparison.Ordinal); }
        Check(failed && clock.Elapsed < TimeSpan.FromSeconds(15),
            "forwarded or exited browser launch fails early with sign-in window recovery instructions");
        string report = debug.Export([], "https://chatgpt.com/", false);
        Check(report.Contains("launchExited=1 connected=0", StringComparison.Ordinal) && !report.Contains(profile, StringComparison.Ordinal),
            "endpoint diagnostics identify exited launch without recording the private profile path");
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
