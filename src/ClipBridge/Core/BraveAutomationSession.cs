using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Playwright;

namespace ClipBridge.Core;

public sealed class BraveAutomationSession : IDisposable
{
    private readonly string _profile = Path.Combine(AutomaticUploadQueue.Folder, "PlaywrightBraveProfile");
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    public IPage? Page { get; private set; }

    public async Task OpenAsync(string url, CancellationToken ct)
    {
        url = AutomaticUploadQueue.ValidateUrl(url);
        string? endpoint = await FindEndpointAsync(ct);
        if (endpoint is null)
        {
            Directory.CreateDirectory(_profile);
            var start = new ProcessStartInfo(BraveBrowserLocator.FindRequired()) { UseShellExecute = false };
            start.ArgumentList.Add("--user-data-dir=" + _profile);
            start.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
            start.ArgumentList.Add("--remote-debugging-port=0");
            start.ArgumentList.Add("--new-window");
            start.ArgumentList.Add(url);
            using var process = Process.Start(start) ?? throw new IOException("Could not start Brave.");
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (endpoint is null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(500, ct);
                endpoint = await FindEndpointAsync(ct);
            }
        }
        if (endpoint is null) throw new IOException("Close the ClipBridge automation Brave window, then open it again from ClipBridge.");
        ct.ThrowIfCancellationRequested();
        _playwright ??= await Playwright.CreateAsync();
        ct.ThrowIfCancellationRequested();
        if (_browser is null || !_browser.IsConnected)
            _browser = await _playwright.Chromium.ConnectOverCDPAsync(endpoint, new() { Timeout = 15000 }).WaitAsync(ct);
        var context = _browser.Contexts.FirstOrDefault() ?? throw new IOException("The automation browser has no profile context.");
        Page ??= context.Pages.FirstOrDefault(page => page.Url.TrimEnd('/') == url);
        if (Page is null || Page.IsClosed) Page = await context.NewPageAsync().WaitAsync(ct);
        if (Page.Url.TrimEnd('/') != url) await Page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 }).WaitAsync(ct);
        await Page.BringToFrontAsync().WaitAsync(ct);
        ct.ThrowIfCancellationRequested();
    }

    private async Task<string?> FindEndpointAsync(CancellationToken ct)
    {
        string portFile = Path.Combine(_profile, "DevToolsActivePort");
        if (!File.Exists(portFile)) return null;
        try
        {
            string[] lines = await File.ReadAllLinesAsync(portFile, ct);
            if (lines.Length < 2 || !int.TryParse(lines[0], out int port) || port is < 1024 or > 65535 ||
                !System.Text.RegularExpressions.Regex.IsMatch(lines[1], @"^/devtools/browser/[a-zA-Z0-9-]+$")) return null;
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await client.GetAsync($"http://127.0.0.1:{port}/json/version", ct);
            if (!response.IsSuccessStatusCode) return null;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            string? endpoint = json.RootElement.GetProperty("webSocketDebuggerUrl").GetString();
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "ws" &&
                uri.Host == "127.0.0.1" && uri.Port == port && uri.AbsolutePath == lines[1] ? endpoint : null;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException or KeyNotFoundException ||
            (ex is TaskCanceledException && !ct.IsCancellationRequested))
        { return null; }
    }

    public void Dispose()
    {
        // Disconnect the driver; leave the user's visible browser and login intact.
        _playwright?.Dispose();
        _playwright = null;
        _browser = null;
        Page = null;
    }
}
