# ClipBridge Brave batch uploads (extension 0.3.0)

Requires ClipBridge Windows 0.5.0 or later. Install the Windows app and extension from the same successful build. This uses your normal signed-in Brave browser, with no login automation or sandbox changes.

## Update and pair

1. Close ClipBridge when no local job is running, then install the new Windows build. Your source videos, saved B-roll metadata and pairing key are preserved.
2. Extract the extension ZIP. If updating an existing unpacked extension, replace the files in its existing folder and click **Reload** at `brave://extensions`. Refresh the ChatGPT tab afterwards. For a first installation, enable Developer mode and **Load unpacked** from the folder containing `manifest.json`, `background.js`, `composer.js`, `content.js`, `popup.js`, and `popup.html`.
3. In ClipBridge, enable the optional local bridge and copy its pairing key. Paste it into the extension popup and check **Enable automatic attachments**. Sign in to ChatGPT yourself in normal Brave.
4. Choose **Clips per message**, initially 3. The allowed setting is 1–10; this is ClipBridge's local cap, **not a discovered ChatGPT account limit**. ChatGPT may enforce a lower limit. A batch is also capped at 1 GB total browser-memory payload, so large clips can reduce the number offered together.
5. Leave **Automatically send ready batches and continue the queue in this chat** unchecked for manual sending. Check it explicitly if you want the extension to send ready batches and continue through prepared clips.
6. Edit the optional per-batch message if desired. The default asks ChatGPT to retain gardening B-roll for planning reels and wait for your instructions.
7. Save settings. Open the intended conversation, send/remove any existing composer attachments or drafts, then click **Arm / continue in this tab**. Keep that tab active and visible.

## Faster preparation and transfer

New Windows jobs default to 100 MB targets and **Compress — smaller file (CRF 32)**. This reduces upload data at a quality/encoding-time cost; you can choose higher quality or fast stream-copy instead. Existing prepared clips are not recompressed automatically. Splitting alone does not reduce total upload data.

The extension downloads two clips locally in parallel and uses 2 MiB chunks, rather than the previous 192 KiB chunks. It supplies the complete batch in one file-input change event so ChatGPT can begin processing the files together. This reduces local message/request overhead; it cannot accelerate your internet connection or ChatGPT's servers.

## Manual-send mode

The extension offers one batch of prepared clips together and pauses. Check the actual composer and website upload results, then send manually. Re-arm to offer the next batch. The Windows **Allow next 3 clips** control remains for the earlier single-file extension protocol; the new extension chooses its batch size directly.

## Optional automatic Send

Auto-send is off by default. When you explicitly enable it and arm the tab, the extension:

1. Requires an empty, identifiable composer and a compatible file input.
2. Transfers the selected clips locally, reserving all attempts on the PC atomically before dispatch.
3. Adds the batch message without overwriting your draft.
4. Waits for every expected filename in the composer, no recognized upload/progress indicator, and a visible enabled Send button, stable for 1.5 seconds. Any visible alert, modal, edited message, hidden tab or changed conversation stops the run.
5. Records a send reservation before clicking the composer Send button **once**. This is the semantic Send action rather than an Enter key that might go to the wrong field.
6. Requires a new user turn containing the batch message and a cleared composer before considering that submission observed.
7. If more clips remain, waits for a new assistant turn and no visible Stop button before offering the next batch in the same armed conversation.

A website readiness state or a new user message is **UI evidence**, not an official upload-completion API or proof that ChatGPT can analyze the video's contents. The DOM can change. Unknown readiness, upload limits, errors or timeouts pause instead of guessing or repeatedly sending.

Starting from a new-chat page can change the URL after the first send. This stops arming. Open the newly created conversation and arm it again to continue there; ClipBridge never follows an arbitrary conversation change automatically. Existing `/c/...` conversations can continue through multiple batches while their URL/document remains the same.

## Test safely

Start with three small, non-sensitive MP4 clips. First leave auto-send off and verify all three appear together. Send or remove that test batch yourself. Then enable auto-send, arm the intended conversation, and verify one ready batch becomes a user message exactly once. Watch the first run, including any website errors or account limits. The synthetic browser tests do not establish live ChatGPT acceptance.

## Stop and recover

- **Disable auto attach** in the popup stops further transfers/sends. A request already submitted cannot be recalled. Uncheck the Windows bridge to close the local listener.
- For interrupted transfers that were never reserved for DOM dispatch: stop the browser operation, click **Retry unattempted transfers** on the PC, then re-arm.
- For a reserved/offered batch or an uncertain send: inspect the composer and conversation before continuing. Those clips are never silently repeated. Use manual attachment for missing/rejected clips.
- For limits or unsupported files: reduce **Clips per message**, compress to smaller MP4s if useful, or wait for the website's limit. ClipBridge does not bypass limits or retry limit errors in a loop.
- If an input, filename or Send button cannot be identified reliably, use **Show next 3 clips (manual)** or **Open output folder**.

## Privacy and security

The bridge is disabled by default and binds only `127.0.0.1:38419`. Every transfer requires the private 256-bit pairing key and extension ID. Normal webpage origins are rejected; origin-less Chromium GETs additionally require browser-controlled `Sec-Fetch-Site: none`. Files require the current claim. No arbitrary file-path or URL proxy exists.

Networking and pairing credentials stay in the MV3 worker and trusted extension storage, never page JavaScript. Clip transfers are authorized only for the armed top-level ChatGPT tab/document/URL. No cookies, browser profile, login credentials or video bytes are sent to Cloudflare. B-roll and local processing remain independent.

Attempt reservations persist as hashed file fingerprints under `%LOCALAPPDATA%\ClipBridge\brave-attempts.json`. A damaged history fails closed; back it up before repairs because clearing it can replay clips. Per-send reservations survive worker suspension in browser-session storage; browser restart clears arming and requires a new explicit arm.

This is an experimental unpacked extension. [Validation scope](https://github.com/Archil3s/clipbridge-windows/blob/main/docs/VALIDATION.md) distinguishes synthetic tests from live-site testing.

To reuse existing outputs after an app upgrade/restart, choose **Add prepared clips** in Windows and select the saved MP4/MOV/M4V/WebM clips. This adds them to the attachment queue without reprocessing or changing them. Missing, empty and over-525-MB files are rejected; durable reservations still prevent silent replay of earlier attempts.
