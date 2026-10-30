# Automatic uploads with Playwright (Windows 0.6.1)

This workflow uploads prepared clips and sends each batch automatically after you start it. It does not require the Brave extension or its local file-transfer bridge. Playwright supplies the local file paths directly to the browser file input, avoiding the extension's base64 chunk transfers.

## Start

1. Install Windows 0.6.1 and open **Automatic uploads (Playwright)**. Version 0.6.1 recognizes the current labelled message box and attachment popover, fixing the indefinite-looking wait in 0.6.0.
2. Choose **Choose clip folder** and select the folder containing your finished MP4/MOV/M4V/WebM clips. The saved queue is separate from source videos waiting to be split. Clips already prepared by the current ClipBridge job are imported when this window opens.
3. Leave the conversation link at `https://chatgpt.com/` for a new chat, or paste an existing `https://chatgpt.com/c/...` conversation link.
4. Choose clips per message and the message to send with each batch. Start with 3. The maximum 10 is a local setting, not a detected account limit; the batch also has a 1 GB cap. Large clips can reduce the count.
5. Click **Open automation browser / sign in**. Sign in yourself in that Brave window. This is a separate saved profile; your usual Brave login is not copied. If Google or ChatGPT requires verification or rejects sign-in, resolve it yourself using the options the site provides. ClipBridge does not automate authentication or bypass checks.
6. Return to ClipBridge and click **Start automatic uploads + Send**. Starting authorizes the displayed queue and batch message in the selected conversation.

The uploader attaches a batch, waits for all filenames and a ready Send button with no upload indicators, sends once, confirms the new user message, waits for the response to finish, and proceeds to the next batch. A home-page chat can receive its new conversation URL after the first confirmed message; that URL is saved for later runs.

The browser must stay open. You can use other windows; this workflow does not require its tab to stay focused. Avoid editing the active upload conversation while the queue runs.

## Queue and recovery

The queue, message, batch size and conversation link are saved locally in `%LOCALAPPDATA%\ClipBridge\automatic-uploads.json`. Selecting clips does not start uploads. Ready clips remain ready across restarts. Only one uploader can open the saved queue at a time.

- **Ready:** no attachment attempt has been recorded.
- **Attachment attempted:** the attempt was recorded before the browser attachment call. Its result may be uncertain.
- **Send attempted:** Send was reserved; the result is not yet confirmed.
- **Submitted:** a new user message and a cleared composer were observed. This does not prove the model can analyze the video or produce an edited reel.

**Stop** prevents subsequent automation actions. It cannot recall a file upload or message already submitted. An interrupted attempt is not repeated automatically. Check the chat, select the affected entries and use **Retry reviewed clips** only when another attempt is appropriate. This explicit retry can duplicate a file or message already present. **Remove selected** removes queue entries, never the video files.

Limits, visible errors, edited drafts, changed conversations, unsupported page layouts and closed tabs pause the run. ChatGPT's website can change; this is an experimental UI integration, not an official upload API. Direct disk attachment removes the extension transfer overhead, but internet uploads and ChatGPT processing still take time.

The old extension remains available as an alternative. Opening the Playwright uploader disables ClipBridge's extension bridge to avoid running both delivery methods at once. Previous extension reservations are not imported into this separate queue: choose only clips you intend to submit to this conversation.

## Browser and installation details

The installer bundles the Microsoft.Playwright .NET driver. It uses your installed Brave executable with a dedicated `%LOCALAPPDATA%\ClipBridge\PlaywrightBraveProfile` directory and an automatically selected loopback debugging port. It connects through CDP. The production launch does not add `--no-sandbox`, stealth scripts, anti-detection flags or automatic login steps. The debugging endpoint is local to this PC; the profile and its browser-owned sign-in data stay local. Close the automation Brave window when finished.

This profile is separate from both your normal Brave profile and the retired earlier `ChatGptBraveProfile`. ClipBridge never copies cookies from either profile. Normal Brave windows, local video processing, B-roll metadata and optional Cloudflare monitoring remain independent.

## Validation

The test suite uses synthetic clips and pages in isolated Chromium profiles. It checks durable queue reservations, corrupt-state handling, file changes, URL restrictions, multi-file disk attachments, a real loopback CDP connection, automatic batches, dynamic file pickers, new-chat navigation, disabled Send, missing filenames, pending uploads, errors, edited drafts, cancellation and uncertain sends. Live Google sign-in, real ChatGPT video acceptance and account-specific limits require manual verification.

Implementation references: [Playwright file uploads](https://playwright.dev/dotnet/docs/input#upload-files), [CDP connection API](https://playwright.dev/dotnet/docs/api/class-browsertype#browser-type-connect-over-cdp), and [Chromium's separate-profile guidance for remote debugging](https://developer.chrome.com/blog/remote-debugging-port).
