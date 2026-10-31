using System.Net;
using System.Text.Json;
using ClipBridge.Core;

static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}

string root = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "work"), "ClipBridge-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    UploadDebugTests.Run();
    if (args.Contains("--automation"))
    {
        await AutomaticUploadTests.RunAsync(root);
        return;
    }
    if (args.Contains("--serve"))
    {
        var fixtures = new List<string>();
        for (int i = 1; i <= 4; i++)
        {
            string fixture = Path.Combine(root, $"transport-{i}.mp4");
            await File.WriteAllBytesAsync(fixture, Enumerable.Range(0, 700000).Select(n => (byte)((n + i) % 251)).ToArray());
            fixtures.Add(fixture);
        }
        await using var fixtureBridge = new BraveUploadBridge(new string('A', 64), Path.Combine(root, "attempts.json"), 38429);
        fixtureBridge.AddFiles(fixtures);
        await fixtureBridge.StartAsync();
        Console.WriteLine("BRIDGE_TEST_READY");
        await Task.Delay(TimeSpan.FromSeconds(90));
        return;
    }
    string status = "";
    Check(CloudflareStatusReporter.CreateOptional(false, "bad", "", s => status = s) is null,
        "disabled monitoring ignores invalid configuration");
    Check(CloudflareStatusReporter.CreateOptional(true, "https://example.com", "short", s => status = s) is null &&
        status.Contains("Local processing continues"), "invalid monitoring reports error and returns disabled");
    Check(CloudflareStatusReporter.CreateOptional(true, "https://example.com", new string('a', 32) + "\n", _ => { }) is null,
        "malformed monitoring authorization is disabled before starting reporter");
    Check(CloudflareStatusReporter.CreateOptional(true, "http://localhost", new string('a', 32), _ => { }) is null,
        "monitoring still rejects insecure endpoints");
    await using var reporter = CloudflareStatusReporter.CreateOptional(true, "https://example.com", new string('a', 32), _ => { });
    Check(reporter is not null, "valid monitoring remains available");

    var settings = new LibrarySettings(Path.Combine(root, "settings.json"));
    settings.SaveRoot(root);
    Check(new LibrarySettings(Path.Combine(root, "settings.json")).ReadRoot() == root,
        "custom B-roll destination survives settings reload");
    string video = Path.Combine(root, "clip.mp4");
    await File.WriteAllBytesAsync(video, [1, 2, 3, 4]);
    var clip = new BRollClip(video, "Planting", TimeSpan.Zero, TimeSpan.FromSeconds(1));
    await BRollLibrary.SaveMetadataAsync(clip, "source.mp4", "Seed bed", ["Planting", "Watering"], default);
    Check(BRollLibrary.FindClips(root, []).Count == 1, "saved clip reloads with no filters");
    Check(BRollLibrary.FindClips(root, ["Tools", "Watering"]).Count == 1, "filters match any selected gardening tag");
    Check(BRollLibrary.FindClips(root, ["Tools"]).Count == 0, "unmatched tags hide clip");
    await BRollLibrary.UpdateTagsAsync(BRollLibrary.FindClips(root, [])[0], ["Harvesting", "Tools"], default);
    Check(BRollLibrary.FindClips(root, ["Tools"])[0].Tags.Count == 2, "edited tags persist on disk");
    Check(BRollLibrary.FindClips(root, ["Planting"]).Count == 0, "old tags replaced");
    Check(BRollCutter.SanitizeLabel("../CON") == "__CON", "category cannot escape library directory");

    if (args.Contains("--video"))
    {
        string ffmpeg = Path.Combine(DependencyInstaller.ToolsDirectory, "ffmpeg.exe");
        string source = Path.Combine(root, "synthetic.mp4");
        var start = new System.Diagnostics.ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in new[] { "-hide_banner", "-loglevel", "error", "-f", "lavfi",
            "-i", "testsrc2=size=160x120:rate=24", "-f", "lavfi", "-i", "sine=frequency=440",
            "-t", "3", "-c:v", "libx264", "-c:a", "aac", source }) start.ArgumentList.Add(argument);
        using var generation = System.Diagnostics.Process.Start(start)!;
        await generation.WaitForExitAsync();
        Check(generation.ExitCode == 0, "synthetic video fixture generated");
        byte[] original = await File.ReadAllBytesAsync(source);
        var cutter = new BRollCutter();
        var exported = await cutter.CutAsync(source, Path.Combine(root, "library"), "Planting",
            "Seed bed", TimeSpan.FromSeconds(.5), TimeSpan.FromSeconds(1.5), null, default);
        Check(File.Exists(exported.FilePath) && exported.Category == "Planting",
            "B-roll exports MP4 into category directory");
        Check(Math.Abs((await cutter.GetDurationAsync(exported.FilePath, default)).TotalSeconds - 1) < .3,
            "B-roll In/Out produces expected duration");
        var splitter = new VideoSplitter();
        foreach (bool compress in new[] { false, true })
        {
            await using var disabled = CloudflareStatusReporter.CreateOptional(compress,
                "https://example.com", "invalid", _ => { });
            var split = await splitter.SplitAsync(source, Path.Combine(root, "split-" + compress),
                10, compress, 28, null, default);
            Check(split.AllWithinLimit && split.Parts > 0,
                compress ? "compression succeeds with misconfigured monitoring disabled" :
                    "local splitting succeeds with monitoring off");
        }
        byte[] after = await File.ReadAllBytesAsync(source);
        Check(original.SequenceEqual(after), "source preserved after cut/split/compression");
    }

    var key = new string('A', 64);
    await using var bridge = new BraveUploadBridge(key, Path.Combine(root, "attempts.json"), 38429);
    bridge.AddFiles([video, video]);
    await bridge.StartAsync();
    using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:38429") };
    async Task<HttpResponseMessage> Call(string path, string? origin, string? token, HttpMethod? method = null,
        string? claim = null, string? range = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        if (origin is not null) request.Headers.Add("Origin", origin);
        if (token is not null) request.Headers.Add("X-ClipBridge-Key", token);
        request.Headers.Add("X-ClipBridge-Extension", new string('a', 32));
        if (claim is not null) request.Headers.Add("X-ClipBridge-Claim", claim);
        if (range is not null) request.Headers.Add("Range", range);
        return await http.SendAsync(request);
    }
    const string extension = "chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    foreach (string? origin in new[] { "https://evil.example", "https://chatgpt.com", "null", null })
    {
        using var denied = await Call("/api/next", origin, key);
        Check(denied.StatusCode == HttpStatusCode.Forbidden, "web and missing origins denied: " + origin);
    }
    using (var denied = await Call("/api/health", extension, "bad"))
        Check(denied.StatusCode == HttpStatusCode.Unauthorized, "wrong pairing key denied");
    using (var extensionGet = new HttpRequestMessage(HttpMethod.Get, "/api/health"))
    {
        extensionGet.Headers.Add("X-ClipBridge-Key", key);
        extensionGet.Headers.Add("X-ClipBridge-Extension", new string('a', 32));
        extensionGet.Headers.Add("Sec-Fetch-Site", "none");
        using var response = await http.SendAsync(extensionGet);
        Check(response.IsSuccessStatusCode, "Chromium extension GET without Origin requires key, extension ID and Fetch Metadata");
    }
    using var next = await Call("/api/next", extension, key);
    var item = JsonDocument.Parse(await next.Content.ReadAsStringAsync()).RootElement;
    string id = item.GetProperty("id").GetString()!;
    string claim = item.GetProperty("claim").GetString()!;
    using (var denied = await Call("/api/file/" + id, extension, key, claim: "stale"))
        Check(denied.StatusCode == HttpStatusCode.NotFound, "stale claim cannot retrieve file");
    using (var bytes = await Call("/api/file/" + id, extension, key, claim: claim, range: "bytes=1-2"))
        Check(bytes.StatusCode == HttpStatusCode.PartialContent &&
            (await bytes.Content.ReadAsByteArrayAsync()).SequenceEqual(new byte[] { 2, 3 }), "authenticated ranged local transfer");
    using (var blocked = await Call("/api/next", extension, key))
        Check((await blocked.Content.ReadAsStringAsync()).Contains("review-required"), "claimed clip is not skipped or automatically replayed");
    bridge.RetryUnoffered();
    using var retry = await Call("/api/next", extension, key);
    var retried = JsonDocument.Parse(await retry.Content.ReadAsStringAsync()).RootElement;
    string newClaim = retried.GetProperty("claim").GetString()!;
    Check(retried.GetProperty("id").GetString() == id && newClaim != claim, "explicit retry keeps clip and rotates lease");
    using (var stale = await Call("/api/offered/" + id, extension, key, HttpMethod.Post, claim))
        Check(stale.StatusCode == HttpStatusCode.NotFound, "old transfer cannot acknowledge new lease");
    using (var ack = await Call("/api/offered/" + id, extension, key, HttpMethod.Post, newClaim))
        Check(ack.IsSuccessStatusCode, "attempt reserved");
    using (var ack = await Call("/api/offered/" + id, extension, key, HttpMethod.Post, newClaim))
        Check(ack.IsSuccessStatusCode, "duplicate reservation is idempotent");
    using (var conflict = await Call("/api/retry/" + id, extension, key, HttpMethod.Post, newClaim))
        Check(conflict.StatusCode == HttpStatusCode.Conflict, "uncertain attempt cannot silently retry");
    bridge.RetryUnoffered();
    using (var empty = await Call("/api/next", extension, key))
        Check((await empty.Content.ReadAsStringAsync()).Contains("empty"), "duplicate paths and reserved attempts are not requeued");
    for (int i = 0; i < 4; i++)
    {
        string file = Path.Combine(root, $"batch-{i}.mp4");
        await File.WriteAllBytesAsync(file, [0]);
        bridge.AddFiles([file]);
    }
    for (int i = 0; i < 2; i++)
    {
        using var queued = await Call("/api/next", extension, key);
        var q = JsonDocument.Parse(await queued.Content.ReadAsStringAsync()).RootElement;
        using var ack = await Call("/api/offered/" + q.GetProperty("id").GetString(), extension, key,
            HttpMethod.Post, q.GetProperty("claim").GetString());
        Check(ack.IsSuccessStatusCode, "batch attempt " + (i + 2));
    }
    using (var limited = await Call("/api/next", extension, key))
        Check((await limited.Content.ReadAsStringAsync()).Contains("batch-complete"), "batch capped at three attempts including retries");
    bridge.AllowNextBatch();
    using (var more = await Call("/api/next", extension, key))
        Check((await more.Content.ReadAsStringAsync()).Contains("\"ready\":true"), "explicit next batch resumes queue");
    bridge.Clear();
    bridge.AddFiles(Directory.GetFiles(root, "batch-*.mp4"));
    using (var invalidBatch = await Call("/api/batch?count=11", extension, key))
        Check(invalidBatch.StatusCode == HttpStatusCode.BadRequest, "batch size is bounded without claiming files");
    using var batchResponse = await Call("/api/batch?count=3", extension, key);
    var batch = JsonDocument.Parse(await batchResponse.Content.ReadAsStringAsync()).RootElement;
    var batchItems = batch.GetProperty("items").EnumerateArray().ToArray();
    Check(batchItems.Length == 2, "batch claims only remaining unreserved clips without replay");
    async Task<HttpResponseMessage> ReserveBatch(object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/offered-batch");
        request.Headers.Add("Origin", extension);
        request.Headers.Add("X-ClipBridge-Key", key);
        request.Headers.Add("X-ClipBridge-Extension", new string('a', 32));
        request.Content = System.Net.Http.Json.JsonContent.Create(body);
        return await http.SendAsync(request);
    }
    using (var invalid = await ReserveBatch(batchItems.Select((item, index) => new
    { id = item.GetProperty("id").GetString(), claim = index == 0 ? "stale" : item.GetProperty("claim").GetString() })))
        Check(invalid.StatusCode == HttpStatusCode.Conflict, "stale claim rejects entire batch atomically");
    var reservations = batchItems.Select(item => new { id = item.GetProperty("id").GetString(), claim = item.GetProperty("claim").GetString() }).ToArray();
    using (var reserved = await ReserveBatch(reservations))
        Check(reserved.IsSuccessStatusCode, "all batch attempts reserved together");
    using (var reservedAgain = await ReserveBatch(reservations))
        Check(reservedAgain.IsSuccessStatusCode, "batch reservation is idempotent");
    using (var emptyBatch = await Call("/api/batch?count=3", extension, key))
        Check((await emptyBatch.Content.ReadAsStringAsync()).Contains("empty"), "next batch does not duplicate reserved files");
    await bridge.DisposeAsync();
    await using var restarted = new BraveUploadBridge(key, Path.Combine(root, "attempts.json"), 38429);
    restarted.AddFiles([video]);
    await restarted.StartAsync();
    using (var remembered = await Call("/api/next", extension, key))
        Check((await remembered.Content.ReadAsStringAsync()).Contains("empty"), "reserved file is not replayed after desktop bridge restart");
}
finally
{
    string fixtureRoot = Path.GetFullPath(root);
    string fixtureParent = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "work")) + Path.DirectorySeparatorChar;
    if (!fixtureRoot.StartsWith(fixtureParent, StringComparison.OrdinalIgnoreCase))
        throw new IOException("Refusing to remove a fixture outside the test workspace.");
    for (int attempt = 0; ; attempt++)
    {
        try { Directory.Delete(fixtureRoot, recursive: true); break; }
        // Chromium child processes can release profile files just after the parent exits.
        catch (IOException) when (attempt < 25) { await Task.Delay(200); }
    }
}

