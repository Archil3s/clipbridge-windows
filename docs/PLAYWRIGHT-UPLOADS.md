# Automatic uploads with Playwright (Windows 0.6.13)

This workflow uploads prepared clips and sends each batch automatically after you start it. It does not require the Brave extension or its local file-transfer bridge. Playwright supplies the local file paths directly to the browser file input, avoiding the extension's base64 chunk transfers.

## Start

1. Close ClipBridge and its separate automation Brave windows, install Windows 0.6.13, then open **Automatic uploads (Playwright)**. Your normal Brave windows can stay open.
2. Check the **Clip folder** path, initially the main window's Output folder, and click **Load folder** to add its finished MP4/MOV/M4V/WebM clips directly. Alternatively use **Choose clip folder** or **Add clips**. The folder picker displays folders only: choose the directory and click Select Folder. The import result above the queue shows discovered, added and duplicate counts or an error. Only files directly in the selected folder are imported. The saved queue is separate from source videos waiting to be split. Clips already prepared by the current ClipBridge job are imported when this window opens.
3. Leave the conversation link at `https://chatgpt.com/` for a new chat, or paste an existing `https://chatgpt.com/c/...` conversation link.
4. Choose clips per message and the message to send with each batch. Start with 3. The maximum 10 is a local setting, not a detected account limit; the batch also has a 1 GB cap. Large clips can reduce the count.
5. Click **Open browser / sign in**. Sign in and complete any verification yourself. This launch has no debugging flags and no Playwright connection. If an earlier automation Brave window is open, close that separate window first. Your usual Brave login is not copied.
6. Once the chat loads, close all windows belonging to this separate sign-in profile. Then click **Start automatic uploads + Send**. ClipBridge reopens the same saved profile with your selected conversation and connects Playwright. Starting authorizes the displayed queue and batch message. You only need to repeat manual sign-in if the site requests it again.

When resuming an existing automation window, Start uses its matching conversation tab without reloading or navigating it. If no matching tab is open, open the chosen chat yourself or copy its address into ClipBridge. Close duplicate tabs with the same address before resuming.

If verification appears during a run, ClipBridge pauses and disconnects Playwright. Finish verification yourself before starting again. Unattempted clips stay ready; attachments or sends already attempted require review before retrying. If verification keeps looping while automation is stopped, leave the run paused and use the site's verification troubleshooting or support. These changes prevent ClipBridge from connecting during sign-in or changing browser preferences; they do not guarantee that Google, ChatGPT or Cloudflare will accept the session. See [Cloudflare's challenge troubleshooting](https://developers.cloudflare.com/cloudflare-challenges/troubleshooting/challenge-solve-issues/).

The uploader selects and verifies **High** thinking before attaching a batch. It waits for every filename, unchanged batch text and an enabled Send button with no upload indicators, then presses **Enter once**. Numbered display names such as `Part_001(3).mp4` can match `Part_001.mp4`; missing files or incorrect counts still pause the run. The status explains whether it is waiting for filenames, upload progress or an enabled Send button.

After the new user message appears and the composer clears, ClipBridge saves the submitted batch and its conversation address in one atomic update. It waits for the response to finish, attaches the next set to that same conversation, and repeats until no ready clips remain. If a new chat has not acquired its conversation address, the next batch does not start. An unconfirmed Enter is never repeated or followed by a fallback Send click.

If you already sent a batch manually, paste that chat's full `https://chatgpt.com/c/...` address into ClipBridge before continuing. Do not retry earlier attempted clips unless you have checked they were not sent. The update preserves these uncertainty records instead of silently replaying them. If thinking effort cannot be identified, select High yourself in ChatGPT and restart; unsupported controls pause before attachment.

The browser must stay open. Start brings the chosen tab to the front. Avoid editing the active upload conversation while the queue runs. A stopped or completed run disconnects Playwright and leaves Brave open.

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

If the debug report stops at **Endpoint**, no browser attachment has started. A separate sign-in window left open does not gain automation merely because Start launches Brave again: the existing browser may receive the launch and continue without a debugging endpoint. Close every window of that separate ClipBridge profile, then click Start. The saved login remains. Version 0.6.13 reports an exited launch earlier, includes `launchExited` and `connected` booleans in diagnostics, and keeps the recovery message and Start/Stop buttons visible while settings scroll.

Human-verification screens still require manual action. A repeating Cloudflare challenge is not proof of a file or queue problem. Leave automation stopped if verification keeps repeating; this release does not bypass or guarantee acceptance of that check.

The installer bundles the Microsoft.Playwright .NET driver. It uses your installed Brave executable with a dedicated `%LOCALAPPDATA%\ClipBridge\PlaywrightBraveProfile` directory and an automatically selected loopback debugging port. It connects through CDP. The production launch does not add `--no-sandbox`, stealth scripts, anti-detection flags or automatic login steps. The debugging endpoint is local to this PC; the profile and its browser-owned sign-in data stay local. Close the automation Brave window when finished.

This profile is separate from both your normal Brave profile and the retired earlier `ChatGptBraveProfile`. ClipBridge never copies cookies from either profile. Normal Brave windows, local video processing, B-roll metadata and optional Cloudflare monitoring remain independent.

## Validation

The test suite uses synthetic clips and pages in isolated Chromium profiles. It checks durable queue reservations, corrupt-state handling, file changes, URL restrictions, multi-file disk attachments, a real loopback CDP connection, automatic batches, dynamic file pickers, new-chat navigation, disabled Send, missing filenames, pending uploads, errors, edited drafts, cancellation and uncertain sends. Live Google sign-in, real ChatGPT video acceptance and account-specific limits require manual verification.

Implementation references: [Playwright file uploads](https://playwright.dev/dotnet/docs/input#upload-files), [CDP connection API](https://playwright.dev/dotnet/docs/api/class-browsertype#browser-type-connect-over-cdp), and [Chromium's separate-profile guidance for remote debugging](https://developer.chrome.com/blog/remote-debugging-port).

## Debugging a stalled upload

Click **Copy debug report** while the run is stuck or paused, then paste the report into Codex. The button stays enabled during uploads. The report keeps only eight recent state changes in memory, rather than a growing transcript. It includes stage timing, queue totals, attachment-name recognition, upload activity, Send readiness, text matching, thinking effort, sent-message confirmation, response state and conversation-match counts. Repeated unchanged polling is counted rather than repeated in the report.

The report excludes clip names and paths, video data, prompt/chat text, full URLs, raw exception messages, cookies, pairing keys and credentials. It is copied only when you click the button; nothing is sent automatically. Copying does not dismiss or overwrite the upload status. A clipboard-busy message lets you retry copying.

## Working with Brave minimized

The automation browser uses background-upload settings and CDP page-focus emulation while connected. You can minimize its window or work in other apps. Start does not bring the browser window to the front. Keep ClipBridge, that Brave window and the PC running; minimizing does not make uploads continue during sleep or after closing the browser. Reopen the separate automation browser after installing this update so its launch settings take effect.

Diagnostics include focus and visible-page booleans, and distinguish the attachment-menu/file-picker phase from High selection. These page flags describe emulated page activity, not whether the Windows window is foregrounded.

Implementation references: [Chromium tool-launch flags](https://github.com/GoogleChrome/chrome-launcher/blob/main/docs/chrome-flags-for-tools.md) and [CDP focus emulation](https://github.com/ChromeDevTools/devtools-protocol/blob/master/pdl/domains/Emulation.pdl). Live ChatGPT background uploads are not guaranteed by synthetic testing.
