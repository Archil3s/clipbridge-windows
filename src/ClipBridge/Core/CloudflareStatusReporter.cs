using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ClipBridge.Core;

public sealed record CloudflareStatus(
    string JobId, string FileName, string Status, double Progress,
    int ClipsDone, int ClipsTotal, double FileSizeGb);

// Explicit opt-in. Sends only sanitized status metadata (never video bytes,
// cookies or conversation data). The ingest token remains in memory.
public sealed class CloudflareStatusReporter : IAsyncDisposable
{
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(8)
    };
    private readonly Uri _endpoint;
    private readonly string _token;
    private readonly CancellationTokenSource _stop = new();
    private readonly Action<string>? _onConnection;
    private CloudflareStatus? _latest;
    private Task? _loop;

    public CloudflareStatusReporter(string workerUrl, string token, Action<string>? onConnection = null)
    {
        if (!Uri.TryCreate(workerUrl.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.IsLoopback || uri.HostNameType != UriHostNameType.Dns)
            throw new ArgumentException("Provide the HTTPS address of your Cloudflare Worker.", nameof(workerUrl));
        if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
            throw new ArgumentException("Cloudflare ingest token must contain at least 32 characters.", nameof(token));

        _endpoint = new Uri(uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/api/status");
        _token = token;
        _onConnection = onConnection;
    }

    public void Publish(CloudflareStatus status) =>
        Interlocked.Exchange(ref _latest, status);

    public void Start()
    {
        if (_loop is not null) return;
        _loop = LoopAsync();
    }

    private async Task LoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        // Immediately push the initial status, then send heartbeats while active.
        do
        {
            var status = Volatile.Read(ref _latest);
            if (status is not null)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                    {
                        Content = JsonContent.Create(new
                        {
                            jobId = status.JobId,
                            fileName = status.FileName,
                            status = status.Status,
                            progress = Math.Clamp(status.Progress, 0, 100),
                            clipsDone = status.ClipsDone,
                            clipsTotal = status.ClipsTotal,
                            fileSizeGb = status.FileSizeGb
                        })
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                    using var result = await _http.SendAsync(request, _stop.Token);
                    _onConnection?.Invoke(result.IsSuccessStatusCode
                        ? "Cloudflare: connected"
                        : $"Cloudflare: HTTP {(int)result.StatusCode} — check KV and INGEST_TOKEN");
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    _onConnection?.Invoke("Cloudflare: offline (retrying)");
                }
            }
        }
        while (await timer.WaitForNextTickAsync(_stop.Token));
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_loop is not null)
        {
            try { await _loop; }
            catch (OperationCanceledException) { }
        }
        _http.Dispose();
        _stop.Dispose();
    }
}
