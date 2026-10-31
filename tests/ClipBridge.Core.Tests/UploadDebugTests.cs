using ClipBridge.Core;

internal static class UploadDebugTests
{
    public static void Run()
    {
        const string secret = "PRIVATE-do-not-copy";
        var log = new UploadDebugLog();
        log.Stage(UploadDebugStage.UploadReady);
        for (int i = 0; i < 10000; i++)
            log.Observe(true, false, true, false, true, secret, false, false, false, 0, 0,
                "https://chatgpt.com/c/" + secret, "https://chatgpt.com");
        var clips = new[] { new AutomaticClip(secret, secret, "Attachment attempted") };
        string report = log.Export(clips, "https://chatgpt.com/c/" + secret, true);
        Check(report.Length < 2000 && report.Split('\n').Length < 15, "debug report remains compact after repeated polling");
        Check(report.Contains("names=0 busy=1 send=0") && report.Contains("url=chat match=0") &&
            report.Contains("review=1"), "debug report identifies the blocked readiness checks and conversation mismatch");
        log.Failure(new InvalidOperationException(secret));
        report = log.Export(clips, "https://chatgpt.com/c/" + secret, false);
        Check(!report.Contains(secret) && report.Contains("Error type=state"), "debug export excludes raw exception text, URLs, clip paths and unknown page labels");
        for (int i = 0; i < 100; i++)
            log.Observe(true, true, false, true, true, "High", false, true, true, i, i,
                "https://chatgpt.com/c/" + secret, "https://chatgpt.com/c/" + secret);
        Check(log.Export(clips, "https://chatgpt.com", false).Length < 2000,
            "debug history stays bounded across changing states");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
}
