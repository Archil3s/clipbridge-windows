using System.Diagnostics;
using System.IO;

namespace ClipBridge.Core;

/// <summary>
/// Opens the user's normal Brave installation. No Playwright session, custom
/// Chromium flags, automation profile, or access to browser credentials.
/// </summary>
public sealed class ChatGptBrowser
{
    public void Open()
    {
        string brave = BraveBrowserLocator.FindRequired();
        var startInfo = new ProcessStartInfo
        {
            FileName = brave,
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add("https://chatgpt.com/");

        if (Process.Start(startInfo) is null)
            throw new IOException("Windows could not open Brave.");
    }
}
