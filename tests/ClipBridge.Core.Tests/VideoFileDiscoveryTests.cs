using ClipBridge.Core;

internal static class VideoFileDiscoveryTests
{
    public static void Run(string root)
    {
        string folder = Path.Combine(root, "folder-discovery");
        string nested = Path.Combine(folder, "nested");
        Directory.CreateDirectory(nested);
        string first = Path.Combine(folder, "same.MOV");
        string second = Path.Combine(folder, "other.mp4");
        string child = Path.Combine(nested, "same.MOV");
        foreach (string file in new[] { first, second, child, Path.Combine(folder, "notes.txt") })
            File.WriteAllText(file, "fixture");
        var direct = VideoFileDiscovery.Collect([folder], false, CancellationToken.None);
        Check(direct.Count == 2 && direct.Contains(first) && direct.Contains(second),
            "folder discovery includes top-level videos and ignores unsupported files");
        var all = VideoFileDiscovery.Collect([folder], true, CancellationToken.None);
        Check(all.Count == 3 && all.Contains(child), "subfolder discovery retains videos with matching basenames");
        var duplicate = VideoFileDiscovery.Collect([folder, nested, first, first.ToUpperInvariant()], true, CancellationToken.None);
        Check(duplicate.SequenceEqual(all), "overlapping folders and Windows path casing do not duplicate queue inputs");
        Check(VideoFileDiscovery.Collect([Path.Combine(folder, "missing"), Path.Combine(folder, "notes.txt")], true,
            CancellationToken.None).Count == 0, "missing and unsupported inputs add no videos");
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        bool cancelled = false;
        try { VideoFileDiscovery.Collect([folder], true, cancel.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "folder discovery honors cancellation before returning a partial result");
        using var midway = new CancellationTokenSource();
        IEnumerable<string> CancelBetweenInputs()
        {
            yield return folder;
            midway.Cancel();
            yield return nested;
        }
        cancelled = false;
        try { VideoFileDiscovery.Collect(CancelBetweenInputs(), false, midway.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "cancelling after scanning one input still returns no partial collection");
        Check(File.ReadAllText(first) == "fixture" && File.ReadAllText(child) == "fixture",
            "folder discovery leaves source videos untouched");
        foreach (string fixture in new[] { first, second, child, Path.Combine(folder, "notes.txt") })
            File.Delete(fixture);
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
}
