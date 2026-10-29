using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ClipBridge.Core;

/// <summary>
/// Opt-in, localhost-only bridge for the ClipBridge Brave extension.
/// No external web server and no video is sent to Cloudflare.
/// The extension retrieves prepared clips one at a time while ChatGPT is open.
/// </summary>
public sealed class BraveUploadBridge : IAsyncDisposable
{
    public const int Port = 38419;
    public const int MaximumBatchCount = 10;
    public const long MaximumBatchBytes = 1_000_000_000;
    private sealed record BatchReservation(string Id, string Claim);
    private readonly object _gate = new();
    private readonly List<UploadItem> _items = [];
    private WebApplication? _server;
    private int _inCurrentBatch;
    private bool _running;
    private DateTime? _lastContactUtc;
    private readonly string _pairingKey;
    private readonly int _port = Port;
    private readonly string? _historyPath;
    private readonly HashSet<string> _attempts = [];

    internal BraveUploadBridge(string key, string? historyPath = null, int port = Port)
    {
        _pairingKey = key;
        _port = port;
        _historyPath = historyPath;
        LoadAttempts();
    }

    private void LoadAttempts()
    {
        if (_historyPath is not null && File.Exists(_historyPath))
        {
            foreach (string entry in JsonSerializer.Deserialize<string[]>(File.ReadAllText(_historyPath)) ?? [])
                _attempts.Add(entry);
        }
    }

    private sealed class UploadItem
    {
        public required string Id { get; init; }
        public required string Path { get; init; }
        public required string Fingerprint { get; init; }
        public string Claim { get; set; } = "";
        public bool Claimed { get; set; }
        public bool Offered { get; set; }
        public DateTime ClaimUntilUtc { get; set; }
    }

    public BraveUploadBridge()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipBridge");
        Directory.CreateDirectory(folder);
        _historyPath = Path.Combine(folder, "brave-attempts.json");
        LoadAttempts();
        string tokenPath = Path.Combine(folder, "brave-upload-key.txt");
        if (File.Exists(tokenPath))
        {
            string token = File.ReadAllText(tokenPath).Trim();
            if (token.Length == 64 && token.All(Uri.IsHexDigit))
            {
                _pairingKey = token;
                return;
            }
        }

        _pairingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        // Best-effort inherited per-user ACL from %LOCALAPPDATA%. Never put
        // this token in the repository or cloud telemetry.
        File.WriteAllText(tokenPath, _pairingKey);
    }

    public string PairingKey => _pairingKey;
    public bool Running => _running;

    public string Status
    {
        get
        {
            lock (_gate)
            {
                int offered = _items.Count(x => x.Offered);
                int pending = _items.Count(x => !x.Offered);
                int blocked = _items.Count(x => x.Claimed && !x.Offered);
                string contact = _lastContactUtc is { } last
                    ? $"Last paired request {(DateTime.UtcNow - last).TotalSeconds:0}s ago. "
                    : "No paired request received yet. ";
                return contact + $"{offered} attempt(s) reserved (upload unknown); {pending} waiting, {blocked} claimed. " +
                       "Verify attachment completion in ChatGPT before sending.";
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
            _inCurrentBatch = 0;
        }
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        lock (_gate)
        {
            foreach (string path in paths.Where(File.Exists))
            {
                if (_items.Any(x => string.Equals(x.Path, path,
                        StringComparison.OrdinalIgnoreCase))) continue;
                var info = new FileInfo(path);
                string fingerprint = Convert.ToHexString(SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(
                        info.FullName.ToUpperInvariant() + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks)));
                _items.Add(new UploadItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Path = path,
                    Fingerprint = fingerprint,
                    Offered = _attempts.Contains(fingerprint)
                });
            }
        }
    }

    public void AllowNextBatch()
    {
        lock (_gate)
        {
            if (_items.Any(x => x.Claimed && !x.Offered))
                throw new InvalidOperationException(
                    "A clip is still being transferred to Brave. Try again after it finishes.");
            _inCurrentBatch = 0;
        }
    }

    public void RetryUnoffered()
    {
        lock (_gate)
        {
            foreach (var item in _items.Where(x => x.Claimed && !x.Offered))
            {
                item.Claimed = false;
                item.Claim = "";
                _inCurrentBatch = Math.Max(0, _inCurrentBatch - 1);
            }
        }
    }

    private void ReserveAttempts(IReadOnlyList<UploadItem> items)
    {
        if (items.All(item => item.Offered)) return;
        var attempts = _attempts.Concat(items.Select(item => item.Fingerprint)).Distinct().ToArray();
        if (_historyPath is not null)
        {
            string temporary = _historyPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(attempts));
            File.Move(temporary, _historyPath, overwrite: true);
        }
        foreach (var item in items)
        {
            _attempts.Add(item.Fingerprint);
            item.Offered = true;
        }
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_running) return;
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, _port));
        WebApplication app = builder.Build();

        app.Use(async (context, next) =>
        {
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            string origin = context.Request.Headers.Origin.ToString();
            bool trustedOrigin = System.Text.RegularExpressions.Regex.IsMatch(
                origin, @"^chrome-extension://[a-p]{32}$");
            string extensionId = context.Request.Headers["X-ClipBridge-Extension"].ToString();
            bool validExtensionId = System.Text.RegularExpressions.Regex.IsMatch(extensionId, @"^[a-p]{32}$");
            // Chromium omits Origin for extension GETs; Fetch Metadata still identifies browser-initiated requests.
            bool extensionRequest = validExtensionId &&
                ((trustedOrigin && origin == "chrome-extension://" + extensionId) ||
                (origin.Length == 0 && context.Request.Headers["Sec-Fetch-Site"] == "none"));
            if (!trustedOrigin && !extensionRequest)
            {
                context.Response.StatusCode = 403;
                return;
            }
            if (trustedOrigin) context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Vary"] = "Origin";
            context.Response.Headers["Access-Control-Expose-Headers"] = "Content-Range";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "X-ClipBridge-Key, X-ClipBridge-Claim, X-ClipBridge-Extension, Range, Content-Type";
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                return;
            }

            if (!extensionRequest)
            {
                context.Response.StatusCode = 403;
                return;
            }
            string submitted = context.Request.Headers["X-ClipBridge-Key"].ToString();
            if (submitted.Length != _pairingKey.Length ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(submitted),
                    System.Text.Encoding.ASCII.GetBytes(_pairingKey)))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Not paired" });
                return;
            }
            lock (_gate) _lastContactUtc = DateTime.UtcNow;
            await next();
        });

        app.MapGet("/api/health", () => Results.Json(new { ok = true, version = 2, maxBatchCount = MaximumBatchCount, maxBatchBytes = MaximumBatchBytes }));
        app.MapGet("/api/batch", (int count) =>
        {
            if (count < 1 || count > MaximumBatchCount) return Results.BadRequest(new { error = "Choose 1 to 10 clips per batch." });
            lock (_gate)
            {
                if (_items.Any(item => item.Claimed && !item.Offered))
                    return Results.Json(new { ready = false, reason = "review-required" });
                var candidates = _items.Where(item => !item.Offered).Take(count).ToArray();
                if (candidates.Length == 0) return Results.Json(new { ready = false, reason = "empty" });
                var selected = new List<UploadItem>();
                long bytes = 0;
                foreach (var item in candidates)
                {
                    if (!File.Exists(item.Path)) return Results.Json(new { ready = false, reason = "missing-file" });
                    long size = new FileInfo(item.Path).Length;
                    if (size <= 0 || size > 525_000_000) return Results.Json(new { ready = false, reason = "unsupported-size" });
                    if (bytes + size > MaximumBatchBytes) break;
                    selected.Add(item);
                    bytes += size;
                }
                foreach (var item in selected)
                {
                    item.Claimed = true;
                    item.Claim = Guid.NewGuid().ToString("N");
                    item.ClaimUntilUtc = DateTime.UtcNow.AddHours(1);
                }
                _inCurrentBatch = selected.Count;
                return Results.Json(new
                {
                    ready = true,
                    remaining = _items.Count(item => !item.Offered) - selected.Count,
                    items = selected.Select(item => new
                    {
                        id = item.Id,
                        claim = item.Claim,
                        name = Path.GetFileName(item.Path),
                        size = new FileInfo(item.Path).Length
                    }).ToArray()
                });
            }
        });
        app.MapPost("/api/offered-batch", async (HttpContext context) =>
        {
            var reservations = await context.Request.ReadFromJsonAsync<List<BatchReservation>>();
            if (reservations is null || reservations.Count < 1 || reservations.Count > MaximumBatchCount ||
                reservations.Select(item => item.Id).Distinct().Count() != reservations.Count)
                return Results.BadRequest(new { error = "Invalid batch reservation." });
            lock (_gate)
            {
                var items = new List<UploadItem>();
                foreach (var reservation in reservations)
                {
                    var item = _items.FirstOrDefault(item => item.Id == reservation.Id &&
                        item.Claim == reservation.Claim && item.Claimed);
                    if (item is null) return Results.Conflict(new { error = "Batch claim changed; review before retrying." });
                    items.Add(item);
                }
                ReserveAttempts(items);
                return Results.Ok(new { status = "reserved", count = items.Count, upload = "unknown" });
            }
        });
        app.MapGet("/api/next", () =>
        {
            lock (_gate)
            {
                if (_inCurrentBatch >= 3)
                    return Results.Json(new { ready = false, reason = "batch-complete" });

                UploadItem? item = _items.FirstOrDefault(x =>
                    !x.Offered);
                if (item is null)
                    return Results.Json(new { ready = false, reason = "empty" });

                if (item.Claimed)
                    return Results.Json(new { ready = false, reason = "review-required" });

                if (!File.Exists(item.Path))
                {
                    return Results.Json(new { ready = false, reason = "missing-file" });
                }

                item.Claimed = true;
                item.Claim = Guid.NewGuid().ToString("N");
                item.ClaimUntilUtc = DateTime.UtcNow.AddHours(1);
                _inCurrentBatch++;
                return Results.Json(new
                {
                    ready = true,
                    id = item.Id,
                    claim = item.Claim,
                    name = Path.GetFileName(item.Path),
                    size = new FileInfo(item.Path).Length
                });
            }
        });
        app.MapGet("/api/file/{id}", (string id, HttpContext context) =>
        {
            lock (_gate)
            {
                UploadItem? item = _items.FirstOrDefault(x =>
                    x.Id == id && x.Claim == context.Request.Headers["X-ClipBridge-Claim"].ToString() && x.Claimed && !x.Offered &&
                    x.ClaimUntilUtc >= DateTime.UtcNow);
                if (item is null || !File.Exists(item.Path)) return Results.NotFound();
                return Results.File(item.Path, "application/octet-stream", enableRangeProcessing: true);
            }
        });
        app.MapPost("/api/offered/{id}", (string id, HttpContext context) =>
        {
            lock (_gate)
            {
                UploadItem? item = _items.FirstOrDefault(x => x.Id == id && x.Claim == context.Request.Headers["X-ClipBridge-Claim"].ToString() && x.Claimed);
                if (item is null) return Results.NotFound();
                // Reserve before DOM dispatch so an uncertain attempt cannot replay.
                ReserveAttempts([item]);
                return Results.Ok(new { status = "attempted", next = "verify-in-chatgpt" });
            }
        });
        app.MapPost("/api/retry/{id}", (string id, HttpContext context) =>
        {
            lock (_gate)
            {
                UploadItem? item = _items.FirstOrDefault(x => x.Id == id && x.Claim == context.Request.Headers["X-ClipBridge-Claim"].ToString() && x.Claimed);
                if (item is null) return Results.NotFound();
                if (item.Offered) return Results.Conflict(new { error = "Review attempted clip in ChatGPT; use manual retry." });
                item.Claimed = false;
                item.ClaimUntilUtc = DateTime.MinValue;
                _inCurrentBatch = Math.Max(0, _inCurrentBatch - 1);
                return Results.Ok(new { status = "retry-enabled" });
            }
        });

        try
        {
            await app.StartAsync(ct);
            _server = app;
            _running = true;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        WebApplication? app = _server;
        _server = null;
        _running = false;
        if (app is null) return;
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
