using System.Diagnostics;

namespace ClipBridge.Core;

internal enum UploadDebugStage
{
    Idle, Starting, SignIn, Endpoint, Driver, Connect, SelectTab, BrowserReady,
    Composer, High, Attach, UploadReady, Enter, Confirm, Response, Complete, Stopped, Error
}

internal sealed class UploadDebugLog
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Queue<string> _events = new();
    private string _last = "";
    private TimeSpan _changed;
    private int _samples;
    private UploadDebugStage _stage;

    public void Stage(UploadDebugStage stage)
    {
        _stage = stage;
        Record(stage.ToString());
    }

    private void Record(string value)
    {
        _samples++;
        if (_last == value) return;
        _last = value;
        _changed = _clock.Elapsed;
        _events.Enqueue($"{_clock.Elapsed.TotalSeconds:F0}s {value}");
        while (_events.Count > 8) _events.Dequeue();
    }

    public void Observe(bool composer, bool names, bool pending, bool send, bool text,
        string thinking, bool responding, bool confirmed, bool empty, int users, int assistants,
        string url, string target)
    {
        static int B(bool value) => value ? 1 : 0;
        string effort = thinking is "High" or "Medium" or "Low" ? thinking : "unknown";
        Record($"{_stage} box={B(composer)} names={B(names)} busy={B(pending)} send={B(send)} " +
            $"text={B(text)} high={effort} reply={B(responding)} sent={B(confirmed)} empty={B(empty)} " +
            $"turns={users}/{assistants} url={Kind(url)} match={B(url.TrimEnd('/') == target.TrimEnd('/'))}");
    }

    internal static string Kind(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Host == "chatgpt.com" ? (uri.AbsolutePath.StartsWith("/c/", StringComparison.Ordinal) ? "chat" :
        uri.AbsolutePath == "/" ? "home" : "other") : "other";

    public void Failure(Exception error)
    {
        _stage = UploadDebugStage.Error;
        string kind = error switch
        {
            TimeoutException => "timeout", OperationCanceledException => "cancelled",
            System.IO.IOException => "io", InvalidOperationException => "state",
            Microsoft.Playwright.PlaywrightException => "browser", _ => "other"
        };
        Record("Error type=" + kind);
    }

    public void Tabs(int total, int matching, int chats) =>
        Record($"SelectTab total={total} matching={matching} chatPages={chats}");

    public void RoutingWait() => Record("Confirm waiting-for-stable-chat-address");

    public string Export(IEnumerable<AutomaticClip> clips, string target, bool running)
    {
        var states = clips.ToArray();
        string version = typeof(UploadDebugLog).Assembly.GetName().Version?.ToString() ?? "unknown";
        return $"ClipBridge debug v1 app={version} running={running}\n" +
            $"target={Kind(target)} ready={states.Count(c => c.State == "Ready")} " +
            $"submitted={states.Count(c => c.State == "Submitted")} " +
            $"review={states.Count(c => c.State is "Attachment attempted" or "Send attempted")}\n" +
            $"elapsed={_clock.Elapsed.TotalSeconds:F0}s unchanged={(_clock.Elapsed - _changed).TotalSeconds:F0}s samples={_samples}\n" +
            "Checks: 1=yes 0=no; turns=user/assistant\n" + string.Join('\n', _events) +
            "\nNo paths, filenames, chat text, addresses or credentials included.";
    }
}
