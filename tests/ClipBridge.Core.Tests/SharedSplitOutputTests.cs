using ClipBridge.Core;

internal static class SharedSplitOutputTests
{
    public static async Task RunAsync(string root)
    {
        string test = Path.Combine(root, "shared-output");
        string legacy = Path.Combine(test, "old-output");
        Directory.CreateDirectory(legacy);
        string inputA = Path.Combine(test, "a.mp4"), inputB = Path.Combine(test, "b.mp4");
        File.WriteAllBytes(inputA, [1, 2, 3]); File.WriteAllBytes(inputB, [4, 5, 6]);
        string keyA = JobJournal.KeyFor(inputA, 10, false, 28), keyB = JobJournal.KeyFor(inputB, 10, false, 28);
        string[] oldClips = [Path.Combine(legacy, "part_0001.mp4"), Path.Combine(legacy, "part_0002.mp4")];
        foreach (string clip in oldClips) File.WriteAllBytes(clip, [7, 8, 9]);
        var legacyJournal = new JobJournal(Path.Combine(test, "legacy-journal.json"));
        legacyJournal.SetState(inputA, 10, false, 28, legacy, "completed");
        var legacyDestination = new SharedSplitOutput(legacy);
        Check(legacyJournal.FindCompleted(inputA, 10, false, 28)?.Parts == 2,
            "choosing a legacy output folder does not invalidate its recoverable clips");
        legacyDestination.RemoveEmptyWorkDirectories(completed: true);
        var shared = new SharedSplitOutput(Path.Combine(test, "chosen-output") + Path.DirectorySeparatorChar);
        string[] a = await shared.CollectAsync(oldClips, keyA, true, default);
        Check(a.Length == 2 && oldClips.All(File.Exists), "legacy split outputs are copied into the selected folder and preserved");
        string owned = Path.Combine(shared.WorkDirectory, "source-b");
        Directory.CreateDirectory(owned);
        string[] fresh = Enumerable.Range(1, 3).Select(i => Path.Combine(owned, $"part_{i:D4}.mp4")).ToArray();
        foreach (string clip in fresh) File.WriteAllBytes(clip, [10, 11, 12]);
        string[] b = await shared.CollectAsync(fresh, keyB, false, default);
        Check(a.Concat(b).All(path => Path.GetDirectoryName(path) == shared.DirectoryPath) &&
            a.Concat(b).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 5,
            "multiple sources produce five uniquely named clips directly in one chosen folder");
        Check(fresh.All(path => !File.Exists(path)) && File.ReadAllBytes(inputB).SequenceEqual(new byte[] { 4, 5, 6 }),
            "only owned temporary split files move; original videos remain untouched");
        var journal = new JobJournal(Path.Combine(test, "journal.json"));
        journal.SetState(inputA, 10, false, 28, shared.DirectoryPath, "completed");
        journal.SetState(inputB, 10, false, 28, shared.DirectoryPath, "completed");
        Check(journal.FindCompleted(inputA, 10, false, 28)?.Parts == 2 &&
            journal.FindCompleted(inputB, 10, false, 28)?.Parts == 3,
            "shared-folder recovery scopes clip counts to each source");
        string[] reused = await shared.CollectAsync(a, keyA, true, default);
        Check(reused.SequenceEqual(a) && Directory.GetFiles(shared.DirectoryPath, "part_*.*").Length == 5,
            "reusing clips already in the chosen folder creates no duplicates, including trailing-slash paths");
        File.Delete(Path.Combine(shared.DirectoryPath, ".clipbridge-state", keyA + ".json"));
        Check(journal.FindCompleted(inputA, 10, false, 28) is null && journal.FindCompleted(inputB, 10, false, 28)?.Parts == 3,
            "missing source manifest cannot fall back to accepting another video's clips");
        File.AppendAllText(b[0], "changed");
        Check(journal.FindCompleted(inputB, 10, false, 28) is null, "changed shared clips invalidate completed-output recovery");
        File.WriteAllText(Path.Combine(shared.DirectoryPath, ".clipbridge-state", keyA + ".json"),
            System.Text.Json.JsonSerializer.Serialize(new[] { new { Name = "../part_0001.mp4", Bytes = 3L } }));
        Check(SharedSplitOutput.ReadFiles(shared.DirectoryPath, keyA) is null,
            "recovery manifests cannot reference files outside the shared folder");
        bool rejected = false;
        try { await shared.CollectAsync(oldClips, keyA, false, default); }
        catch (IOException) { rejected = true; }
        Check(rejected && oldClips.All(File.Exists), "fresh-output moves reject files outside this run's staging directory");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        rejected = false;
        try { await shared.CollectAsync(oldClips, keyA, true, cancelled.Token); }
        catch (OperationCanceledException) { rejected = true; }
        Check(rejected && Directory.GetFiles(shared.DirectoryPath, "part_*.*").Length == 5, "cancelled collection publishes no additional clips");
        shared.RemoveEmptyWorkDirectories(completed: true);
        Check(!Directory.Exists(shared.WorkDirectory), "successful collection removes its empty work directories");
    }

    public static async Task VideoAsync(string source, string root)
    {
        string secondFolder = Path.Combine(root, "second-video");
        Directory.CreateDirectory(secondFolder);
        string second = Path.Combine(secondFolder, Path.GetFileName(source));
        File.Copy(source, second);
        var shared = new SharedSplitOutput(Path.Combine(root, "flat-video-output"));
        var splitter = new VideoSplitter();
        var all = new List<string>();
        foreach (string input in new[] { source, second })
        {
            var result = await splitter.SplitAsync(input, shared.WorkDirectory, 10, false, 28, null, default);
            string key = JobJournal.KeyFor(input, 10, false, 28);
            string[] clips = SharedSplitOutput.ReadFiles(result.OutputDirectory, key)!;
            all.AddRange(await shared.CollectAsync(clips, key, false, default));
            shared.RemoveEmptyWorkDirectories();
        }
        byte[] firstBytes = await File.ReadAllBytesAsync(source), secondBytes = await File.ReadAllBytesAsync(second);
        Check(all.Count == 2 && all.All(path => Path.GetDirectoryName(path) == shared.DirectoryPath) &&
            firstBytes.SequenceEqual(secondBytes),
            "real FFmpeg outputs from same-named sources collect into one folder without changing inputs");
        shared.RemoveEmptyWorkDirectories(completed: true);
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
}
