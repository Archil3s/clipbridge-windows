using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Playwright;

namespace ClipBridge.Core;

public sealed class BraveAutomationSession : IDisposable
{
    private readonly string _profile;
    private readonly string? _browserPath;
    private readonly Func<ProcessStartInfo, Process?> _start;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private ICDPSession? _focusSession;
    public IPage? Page { get; private set; }
    internal UploadDebugLog? DebugLog { get; set; }
    internal Action<string>? ConnectionStatus { get; set; }

    public BraveAutomationSession() : this(Path.Combine(AutomaticUploadQueue.Folder, "PlaywrightBraveProfile")) { }

    internal BraveAutomationSession(string profile, string? browserPath = null, Func<ProcessStartInfo, Process?>? start = null)
    {
        _profile = profile;
        _browserPath = browserPath;
        _start = start ?? Process.Start;
    }

    public async Task OpenForSignInAsync(string url, CancellationToken ct)
    {
        Dispose();
        url = AutomaticUploadQueue.ValidateUrl(url);
        if (await FindEndpointAsync(ct) is not null)
            throw new IOException("Close the ClipBridge automation Brave windows first, then click Open browser / sign in again. Your usual Brave windows can stay open.");
        Directory.CreateDirectory(_profile);
        ct.ThrowIfCancellationRequested();
        using var process = _start(CreateStartInfo(url, automation: false)) ?? throw new IOException("Could not start Brave.");
    }

    private ProcessStartInfo CreateStartInfo(string url, bool automation)
    {
        var start = new ProcessStartInfo(_browserPath ?? BraveBrowserLocator.FindRequired()) { UseShellExecute = false };
        start.ArgumentList.Add("--user-data-dir=" + _profile);
        if (automation)
        {
            start.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
            start.ArgumentList.Add("--remote-debugging-port=0");
            start.ArgumentList.Add("--disable-background-timer-throttling");
            start.ArgumentList.Add("--disable-backgrounding-occluded-windows");
            start.ArgumentList.Add("--disable-renderer-backgrounding");
        }
        start.ArgumentList.Add("--new-window");
        start.ArgumentList.Add(url);
        return start;
    }

    public async Task OpenAsync(string url, CancellationToken ct)
    {
        url = AutomaticUploadQueue.ValidateUrl(url);
        DebugLog?.Stage(UploadDebugStage.Endpoint);
        string? endpoint = await FindEndpointAsync(ct);
        bool launched = endpoint is null;
        if (endpoint is null)
        {
            Directory.CreateDirectory(_profile);
            ct.ThrowIfCancellationRequested();
            using var process = _start(CreateStartInfo(url, automation: true)) ?? throw new IOException("Could not start Brave.");
            var launchTime = Stopwatch.StartNew();
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (endpoint is null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(500, ct);
                endpoint = await FindEndpointAsync(ct);
                DebugLog?.Endpoint(process.HasExited, endpoint is not null);
                if (endpoint is null && process.HasExited && launchTime.Elapsed >= TimeSpan.FromSeconds(3))
                    throw new IOException("Brave's launch process exited before the automation connection opened. Close every window of the separate ClipBridge sign-in browser, then click Start automatic uploads + Send. Your usual Brave windows can stay open; your login is saved.");
                if (endpoint is null && launchTime.Elapsed >= TimeSpan.FromSeconds(3))
                    ConnectionStatus?.Invoke("Waiting for Brave's automation connection. If the separate ClipBridge sign-in browser is still open, close all of its windows. Then stop this attempt and click Start automatic uploads + Send. Your login is saved.");
            }
        }
        if (endpoint is null) throw new IOException("Close all ClipBridge sign-in Brave windows, then click Start again. Your usual Brave windows can stay open.");
        ct.ThrowIfCancellationRequested();
        DebugLog?.Stage(UploadDebugStage.Driver);
        _playwright ??= await Playwright.CreateAsync();
        ct.ThrowIfCancellationRequested();
        DebugLog?.Stage(UploadDebugStage.Connect);
        if (_browser is null || !_browser.IsConnected)
            _browser = await _playwright.Chromium.ConnectOverCDPAsync(endpoint,
                new() { Timeout = 15000, NoDefaults = true, IsLocal = true }).WaitAsync(ct);
        DebugLog?.Stage(UploadDebugStage.SelectTab);
        var context = _browser.Contexts.FirstOrDefault() ?? throw new IOException("The automation browser has no profile context.");
        var candidates = context.Pages.Where(page => !page.IsClosed && AutomaticUploadQueue.BrowserMatches(page.Url, url)).ToArray();
        var pageDeadline = DateTime.UtcNow.AddSeconds(15);
        while (launched && candidates.Length == 0 && DateTime.UtcNow < pageDeadline)
        {
            await Task.Delay(250, ct);
            candidates = context.Pages.Where(page => !page.IsClosed && AutomaticUploadQueue.BrowserMatches(page.Url, url)).ToArray();
        }
        DebugLog?.Tabs(context.Pages.Count, candidates.Length,
            context.Pages.Count(page => !page.IsClosed && UploadDebugLog.Kind(page.Url) == "chat"));
        Page = candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new IOException("Open the chosen conversation in the ClipBridge Brave window and finish signing in. Copy its address into the conversation link, then click Start."),
            _ => throw new IOException("More than one ClipBridge Brave tab has this conversation open. Close the duplicate tabs, then click Start.")
        };
        DebugLog?.Stage(UploadDebugStage.BrowserReady);
        if (_focusSession is not null) await _focusSession.DetachAsync().WaitAsync(ct);
        _focusSession = await context.NewCDPSessionAsync(Page).WaitAsync(ct);
        await _focusSession.SendAsync("Emulation.setFocusEmulationEnabled", new Dictionary<string, object> { ["enabled"] = true }).WaitAsync(ct);
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
        _focusSession = null;
        Page = null;
    }
}
