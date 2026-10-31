using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace ClipBridge.Core;

public sealed class PlaywrightChatUploader
{
    private readonly IPage _page;
    private readonly AutomaticUploadQueue _queue;
    private readonly Action<string> _report;
    private readonly string _token = Guid.NewGuid().ToString("N");
    private readonly TimeSpan _uploadTimeout;
    private string _conversation;
    private bool _dialog;
    internal UploadDebugLog? DebugLog { get; set; }
    private static readonly string Script = "args => {\n" + Resource("ClipBridge.Composer.js") +
        "\n" + Resource("ClipBridge.AutomaticUploadPage.js") + "\n}";

    public PlaywrightChatUploader(IPage page, AutomaticUploadQueue queue, Action<string> report,
        TimeSpan? uploadTimeout = null)
    {
        _page = page;
        _queue = queue;
        _report = report;
        _conversation = AutomaticUploadQueue.BrowserConversationUrl(page.Url);
        if (_conversation != AutomaticUploadQueue.ValidateUrl(queue.Settings.ConversationUrl))
            throw new InvalidOperationException("The open chat does not match the queue's conversation link.");
        _uploadTimeout = uploadTimeout ?? TimeSpan.FromMinutes(15);
    }

    private static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Upload component missing: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        EventHandler<IDialog> onDialog = (_, _) => _dialog = true;
        _page.Dialog += onDialog;
        try
        {
            int number = 0;
            while (_queue.Clips.Any(clip => clip.State == "Ready"))
            {
                ct.ThrowIfCancellationRequested();
                IReadOnlyList<AutomaticClip> batch = _queue.NextBatch(_queue.Settings.BatchSize);
                DebugLog?.Stage(UploadDebugStage.Composer);
                _report("Waiting for the ChatGPT message box. Sign in in the automation browser if needed.");
                UiState ui = await WaitAsync(batch, "", 0, state => state.Found && !state.Responding, TimeSpan.FromMinutes(2), ct);
                if (!ui.Empty) throw new InvalidOperationException("The message box contains a draft or attachments. Review them, then start again.");
                DebugLog?.Stage(UploadDebugStage.High);
                ui = await EnsureHighAsync(ui, batch, ct);
                IFileChooser? chooser = null;
                if (ui.Input is null)
                {
                    DebugLog?.Stage(UploadDebugStage.FilePicker);
                    (ui, chooser) = await FindFileTargetAsync(ui, batch, ct);
                }
                bool multiple = chooser?.IsMultiple ?? ui.Multiple;
                if (!multiple) batch = batch.Take(1).ToArray();
                string[] paths = batch.Select(clip => clip.Path).ToArray();
                // Persist before attachment: an interrupted browser call has an uncertain outcome.
                _queue.Transition(batch, "Ready", "Attachment attempted");
                DebugLog?.Stage(UploadDebugStage.Attach);
                _report($"Attaching {batch.Count} clip(s) directly from disk. ChatGPT upload is starting.");
                CheckContext(ct);
                if (chooser is not null) await chooser.SetFilesAsync(paths, new() { Timeout = 60000 }).WaitAsync(ct);
                else await _page.Locator(ui.Input!).SetInputFilesAsync(paths, new() { Timeout = 60000 }).WaitAsync(ct);
                CheckContext(ct);
                string message = _queue.Settings.Prompt + $" (Batch {++number}, {DateTime.UtcNow:yyyyMMdd-HHmmss}-{_token[..6]})";
                ui = await SnapshotAsync(batch, message, 0, ct);
                if (!ui.Found || ui.Text.Length > 0) throw new InvalidOperationException("The message box changed while attaching. Review the batch before continuing.");
                await _page.Locator(ui.Editor!).FillAsync(message, new() { Timeout = 5000 }).WaitAsync(ct);
                _report($"Batch {number}: waiting for all {batch.Count} attachments to finish uploading.");
                string lastWait = "";
                DebugLog?.Stage(UploadDebugStage.UploadReady);
                ui = await WaitAsync(batch, message, 0, state =>
                {
                    if (state.Found && state.Text != message) throw new InvalidOperationException("Your draft changed. Automatic sending stopped to preserve your text.");
                    if (state.Found && state.Thinking != "High") throw new InvalidOperationException("Thinking effort changed. Select High before resuming.");
                    if (!state.Ready && state.Waiting.Length > 0 && state.Waiting != lastWait)
                    {
                        lastWait = state.Waiting;
                        _report($"Batch {number}: {lastWait}");
                    }
                    return state.Ready;
                }, _uploadTimeout, ct, stable: true);
                int usersBefore = ui.Users;
                int assistantsBefore = ui.Assistants;
                _queue.Transition(batch, "Attachment attempted", "Send attempted");
                ui = await SnapshotAsync(batch, message, usersBefore, ct);
                if (!ui.Ready || ui.Editor is null || ui.Thinking != "High") throw new InvalidOperationException("Readiness changed before Send. Review this batch manually.");
                DebugLog?.Stage(UploadDebugStage.Enter);
                await _page.Locator(ui.Editor).PressAsync("Enter", new() { Timeout = 5000 }).WaitAsync(ct);
                DebugLog?.Stage(UploadDebugStage.Confirm);
                _report($"Batch {number}: waiting for the sent message to appear in chat.");
                bool responseObserved = false;
                ui = await WaitAsync(batch, message, usersBefore, state =>
                {
                    responseObserved |= state.Responding;
                    return state.Confirmed && state.Empty &&
                        state.Conversation.StartsWith("https://chatgpt.com/c/", StringComparison.Ordinal);
                },
                    TimeSpan.FromSeconds(45), ct, allowNewConversation: true);
                _conversation = ui.Conversation;
                _queue.ConfirmSubmitted(batch, _conversation);
                DebugLog?.Stage(UploadDebugStage.Response);
                _report($"Batch {number} submitted. Waiting for ChatGPT to finish responding.");
                await WaitAsync(batch, message, usersBefore,
                    state =>
                    {
                        responseObserved |= state.Responding;
                        return !state.Responding && (state.Assistants > assistantsBefore || responseObserved);
                    },
                    _uploadTimeout, ct, stable: true);
            }
            DebugLog?.Stage(UploadDebugStage.Complete);
            int review = _queue.Clips.Count(clip => clip.State is "Attachment attempted" or "Send attempted");
            _report(review > 0 ? $"Ready clips finished. {review} earlier uncertain clip(s) need review." :
                "All queued clips submitted. Review the chat and give ChatGPT your reel instructions.");
        }
        finally { _page.Dialog -= onDialog; }
    }

    private async Task<UiState> EnsureHighAsync(UiState ui, IReadOnlyList<AutomaticClip> batch, CancellationToken ct)
    {
        if (ui.Thinking == "High") return ui;
        if (ui.ThinkingControl is null)
            throw new InvalidOperationException("Cannot identify thinking effort. Select High in ChatGPT, then start again. No clips were attached.");
        _report("Setting thinking effort to High before attaching the batch.");
        await _page.Locator(ui.ThinkingControl).ClickAsync(new() { Timeout = 5000 }).WaitAsync(ct);
        var high = _page.GetByRole(AriaRole.Menuitemradio, new() { Name = "High", Exact = true })
            .Or(_page.GetByRole(AriaRole.Menuitem, new() { Name = "High", Exact = true }))
            .Or(_page.GetByRole(AriaRole.Radio, new() { Name = "High", Exact = true }))
            .Or(_page.GetByRole(AriaRole.Option, new() { Name = "High", Exact = true }));
        var slider = _page.Locator("[role=menu], [data-radix-popper-content-wrapper]").GetByRole(AriaRole.Slider);
        try
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                CheckContext(ct);
                if (await high.CountAsync() == 1 && await high.IsVisibleAsync())
                {
                    await high.ClickAsync(new() { Timeout = 5000 }).WaitAsync(ct);
                    break;
                }
                if (await slider.CountAsync() == 1 && await slider.IsVisibleAsync())
                {
                    await slider.PressAsync("Home", new() { Timeout = 5000 }).WaitAsync(ct);
                    for (int step = 0; step < 10; step++)
                    {
                        CheckContext(ct);
                        if (await slider.GetAttributeAsync("aria-valuetext") == "High" ||
                            (await SnapshotAsync(batch, "", 0, ct)).Thinking == "High") break;
                        await slider.PressAsync("ArrowRight", new() { Timeout = 5000 }).WaitAsync(ct);
                    }
                    break;
                }
                await Task.Delay(250, ct);
            }
        }
        finally { if (!_page.IsClosed) await _page.Keyboard.PressAsync("Escape"); }
        ui = await WaitAsync(batch, "", 0, state => state.Found && state.Thinking == "High", TimeSpan.FromSeconds(5), ct);
        if (!ui.Empty) throw new InvalidOperationException("The composer changed while setting High. Review it before continuing.");
        return ui;
    }

    private async Task<(UiState State, IFileChooser? Chooser)> FindFileTargetAsync(
        UiState ui, IReadOnlyList<AutomaticClip> batch, CancellationToken ct)
    {
        if (ui.Menu is null) throw new InvalidOperationException("No upload control found. Open the attachment menu in ChatGPT, then start again.");
        var chooserReady = new TaskCompletionSource<IFileChooser>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<IFileChooser> onChooser = (_, chooser) => chooserReady.TrySetResult(chooser);
        _page.FileChooser += onChooser;
        try
        {
            await _page.Locator(ui.Menu).ClickAsync(new() { Timeout = 5000 }).WaitAsync(ct);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                CheckContext(ct);
                if (chooserReady.Task.IsCompletedSuccessfully) return (ui, await chooserReady.Task);
                ui = await SnapshotAsync(batch, "", 0, ct);
                if (ui.Input is not null) return (ui, null);
                var uploadName = new Regex(
                    @"^(Add photos (&|and) files( Upload from computer)?|Upload (from computer|files)|Add files|Upload file)$", RegexOptions.IgnoreCase);
                var menu = _page.GetByRole(AriaRole.Menuitem, new() { NameRegex = uploadName })
                    .Or(_page.GetByRole(AriaRole.Button, new() { NameRegex = uploadName }));
                if (await menu.CountAsync() == 1 && await menu.IsVisibleAsync())
                {
                    await menu.ClickAsync(new() { Timeout = 5000 }).WaitAsync(ct);
                    return (ui, await chooserReady.Task.WaitAsync(TimeSpan.FromSeconds(10), ct));
                }
                await Task.Delay(250, ct);
            }
            throw new InvalidOperationException("The attachment menu did not expose a file input. No clips were claimed.");
        }
        finally { _page.FileChooser -= onChooser; }
    }

    private string? CheckContext(CancellationToken ct, bool allowNewConversation = false)
    {
        ct.ThrowIfCancellationRequested();
        if (_page.IsClosed) throw new InvalidOperationException("The automation tab was closed.");
        if (_dialog) throw new InvalidOperationException("A browser dialog needs your attention. Resolve it and start again.");
        string current;
        try { current = AutomaticUploadQueue.BrowserConversationUrl(_page.Url); }
        catch (ArgumentException) when (allowNewConversation &&
            Uri.TryCreate(_page.Url, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
            uri.Host == "chatgpt.com" && uri.IsDefaultPort && uri.UserInfo.Length == 0)
        {
            DebugLog?.RoutingWait();
            return null;
        }
        if (current != _conversation && !(allowNewConversation && _conversation == "https://chatgpt.com" &&
                current.StartsWith("https://chatgpt.com/c/", StringComparison.Ordinal)))
            throw new InvalidOperationException("The conversation changed. Automation stopped.");
        return current;
    }

    private async Task<UiState> SnapshotAsync(IReadOnlyList<AutomaticClip> batch, string message, int usersBefore,
        CancellationToken ct, bool allowNewConversation = false)
    {
        string? before = CheckContext(ct, allowNewConversation);
        if (before is null) return new UiState();
        UiState state;
        try { state = await _page.EvaluateAsync<UiState>(Script, new
        {
            token = _token,
            message,
            usersBefore,
            items = batch.Select(clip => new { name = clip.Name }).ToArray()
        }).WaitAsync(ct); }
        catch (PlaywrightException ex) when (allowNewConversation &&
            ex.Message.Contains("Execution context was destroyed", StringComparison.Ordinal))
        {
            CheckContext(ct, allowNewConversation);
            return new UiState();
        }
        string? after = CheckContext(ct, allowNewConversation);
        if (after is null || before != after) return new UiState();
        state.Conversation = after;
        DebugLog?.Observe(state.Found, state.Names, state.Pending, state.SendEnabled, state.TextMatches,
            state.Thinking, state.Responding, state.Confirmed, state.Empty, state.Users, state.Assistants, _page.Url, _conversation,
            state.Focused, state.VisiblePage);
        if (!string.IsNullOrWhiteSpace(state.Error)) throw new InvalidOperationException(state.Error);
        return state;
    }

    private async Task<UiState> WaitAsync(IReadOnlyList<AutomaticClip> batch, string message, int usersBefore,
        Func<UiState, bool> predicate, TimeSpan timeout, CancellationToken ct, bool stable = false, bool allowNewConversation = false)
    {
        var timer = Stopwatch.StartNew();
        TimeSpan? readySince = null;
        while (timer.Elapsed < timeout)
        {
            UiState state = await SnapshotAsync(batch, message, usersBefore, ct, allowNewConversation);
            if (predicate(state))
            {
                readySince ??= timer.Elapsed;
                if (!stable || timer.Elapsed - readySince.Value >= TimeSpan.FromMilliseconds(1500)) return state;
            }
            else readySince = null;
            await Task.Delay(250, ct);
        }
        throw new TimeoutException("ChatGPT did not reach the expected state in time. Review the current batch; it will not be repeated automatically.");
    }

    private sealed class UiState
    {
        [JsonPropertyName("focused")] public bool Focused { get; set; }
        [JsonPropertyName("visiblePage")] public bool VisiblePage { get; set; }
        public string Conversation { get; set; } = "";
        [JsonPropertyName("names")] public bool Names { get; set; }
        [JsonPropertyName("pending")] public bool Pending { get; set; }
        [JsonPropertyName("sendEnabled")] public bool SendEnabled { get; set; }
        [JsonPropertyName("textMatches")] public bool TextMatches { get; set; }
        [JsonPropertyName("found")] public bool Found { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("empty")] public bool Empty { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("ready")] public bool Ready { get; set; }
        [JsonPropertyName("thinking")] public string Thinking { get; set; } = "";
        [JsonPropertyName("thinkingControl")] public string? ThinkingControl { get; set; }
        [JsonPropertyName("waiting")] public string Waiting { get; set; } = "";
        [JsonPropertyName("editor")] public string? Editor { get; set; }
        [JsonPropertyName("send")] public string? Send { get; set; }
        [JsonPropertyName("input")] public string? Input { get; set; }
        [JsonPropertyName("menu")] public string? Menu { get; set; }
        [JsonPropertyName("multiple")] public bool Multiple { get; set; }
        [JsonPropertyName("users")] public int Users { get; set; }
        [JsonPropertyName("assistants")] public int Assistants { get; set; }
        [JsonPropertyName("responding")] public bool Responding { get; set; }
        [JsonPropertyName("confirmed")] public bool Confirmed { get; set; }
    }
}
