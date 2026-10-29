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
    private readonly object _gate = new();
    private readonly List<UploadItem> _items = [];
    private WebApplication? _server;
    private int _inCurrentBatch;
    private bool _running;
    private readonly string _pairingKey;

    private sealed class UploadItem
    {
        public required string Id { get; init; }
        public required string Path { get; init; }
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
                return $"{offered} file(s) handed to Brave; {pending} waiting. " +
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
                _items.Add(new UploadItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Path = path
                });
            }
        }
    }

    public void AllowNextBatch()
    {
        lock (_gate)
        {
            if (_items.Any(x => x.Claimed && !x.Offered &&
                    x.ClaimUntilUtc > DateTime.UtcNow))
                throw new InvalidOperationException(
                    "A clip is still being transferred to Brave. Try again after it finishes.");
            _inCurrentBatch = 0;
        }
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_running) return;
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, Port));
        WebApplication app = builder.Build();

        app.Use(async (context, next) =>
        {
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            string origin = context.Request.Headers.Origin.ToString();
            bool trustedOrigin = origin == "https://chatgpt.com" ||
                origin.StartsWith("chrome-extension://", StringComparison.Ordinal);
            if (trustedOrigin)
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                context.Response.Headers["Vary"] = "Origin";
                context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                context.Response.Headers["Access-Control-Allow-Headers"] =
                    "X-ClipBridge-Key, Content-Type";
            }
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = trustedOrigin ? 204 : 403;
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
            await next();
        });

        app.MapGet("/api/health", () => Results.Json(new { ok = true, version = 1 }));
        app.MapGet("/api/next", () =>
        {
            lock (_gate)
            {
                if (_inCurrentBatch >= 3)
                    return Results.Json(new { ready = false, reason = "batch-complete" });

                UploadItem? item = _items.FirstOrDefault(x =>
                    !x.Offered && (!x.Claimed || x.ClaimUntilUtc < DateTime.UtcNow));
                if (item is null)
                    return Results.Json(new { ready = false, reason = "empty" });

                if (!File.Exists(item.Path))
                {
                    item.Offered = true;
                    return Results.Json(new { ready = false, reason = "missing-file" });
                }

                item.Claimed = true;
                item.ClaimUntilUtc = DateTime.UtcNow.AddMinutes(6);
                _inCurrentBatch++;
                return Results.Json(new
                {
                    ready = true,
                    id = item.Id,
                    name = Path.GetFileName(item.Path),
                    size = new FileInfo(item.Path).Length
                });
            }
        });
        app.MapGet("/api/file/{id}", (string id) =>
        {
            lock (_gate)
            {
                UploadItem? item = _items.FirstOrDefault(x =>
                    x.Id == id && x.Claimed && !x.Offered &&
                    x.ClaimUntilUtc >= DateTime.UtcNow);
                if (item is null || !File.Exists(item.Path)) return Results.NotFound();
                return Results.File(item.Path, "application/octet-stream", enableRangeProcessing: true);
            }
        });
        app.MapPost("/api/offered/{id}", (string id) =>
        {
            lock (_gate)
            {
                UploadItem? item = _items.FirstOrDefault(x => x.Id == id && x.Claimed);
                if (item is null) return Results.NotFound();
                // This reports that a File was handed to ChatGPT's input.
                // It intentionally does not claim the upload succeeded.
                item.Offered = true;
                return Results.Ok(new { status = "attempted", next = "verify-in-chatgpt" });
            }
        });
        app.MapPost("/api/retry/{id}", (string id) =>
        {
            lock (_gate)
            {
                UploadItem? item = _items.FirstOrDefault(x => x.Id == id && x.Claimed);
                if (item is null) return Results.NotFound();
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
