# ClipBridge Auto Attach — Brave setup and recovery

## Install and pair once

1. Download `ClipBridge-Installer` and `ClipBridge-Brave-Extension` from the same successful [Windows build](https://github.com/Archil3s/clipbridge-windows/actions/workflows/windows-build.yml). GitHub requires sign-in to download Actions artifacts. Extract both ZIPs and run `ClipBridge-Setup.exe`.
2. Keep the extracted extension folder somewhere permanent. It must contain `manifest.json`, `background.js`, `content.js`, `popup.js`, and `popup.html`.
3. Open your normally installed Brave. In `brave://extensions`, enable Developer mode, choose **Load unpacked**, and select that folder. Pin ClipBridge from Brave's extensions menu.
4. Open ChatGPT in normal Brave and sign in yourself, including Google sign-in if desired. ClipBridge supplies no browser flags, automation profile, cookies, or login automation.
5. In ClipBridge Windows, enable **Allow ClipBridge to offer prepared clips to the Brave extension**, then choose **Copy extension pairing key**.
6. In the extension popup, paste the key, check **Enable automatic attachments for the armed tab**, and choose **Save connection settings**. The enable checkbox is required; arming cannot silently enable it.
7. Open the intended ChatGPT conversation. Refresh it after first installing/updating the extension. Open ChatGPT's attachment menu if necessary so a compatible file input exists. Choose **Arm / continue in this tab** in the popup.

## Test one small video

1. Select a short, non-sensitive source in ClipBridge. Leave Cloudflare unchecked. Choose a small target size and a compression preset to produce H.264/AAC MP4 output.
2. Split the video. Confirm the output plays and the original remains unchanged.
3. Keep the armed ChatGPT tab visible and active. The extension transfers one queued clip over loopback in small chunks and attempts to supply it to the website's file input.
4. Review the composer. A reserved/attempted status is **not upload confirmation**. Check for an attachment, wait for the website's upload progress to finish, and inspect any rejection or account-limit message. Video attachment support depends on ChatGPT's current interface and your account.
5. Send the message yourself. ClipBridge never clicks Send or presses Enter for you.
6. After reviewing the first clip, re-arm to continue with the next. The PC permits at most three reserved attempts per batch. Review/send that group before choosing **Allow next 3 clips** on the PC.

## Why review between clips?

ChatGPT provides no stable extension API for attaching a local video or confirming upload acceptance. Its DOM and supported files can change. ClipBridge therefore uses a conservative extension-assisted workflow: one automatic input attempt per arming, with a maximum of three per desktop batch. It does not infer success from a timeout, a DOM change, or a file-input event. Unattended three-file upload completion is not promised.

A reservation is written on the PC **before** DOM dispatch. If the browser closes, navigation occurs, or an acknowledgement is lost at that point, the outcome remains unknown and the file is not automatically repeated. The PC retains hashed file fingerprints across restarts to avoid replaying the same prepared outputs. Review and use manual attachment for a reserved clip that was rejected or never appeared.

## Recovery

- **Bridge offline / wrong key:** enable the desktop bridge, copy its current key, save settings, then arm again. No connection state is inferred from a running server alone.
- **Interrupted transfer / browser restart:** close or disable the old extension operation, choose **Retry unattempted transfers** on the PC, then re-arm. This rotates the transfer claim; an old transfer cannot acknowledge the new attempt. Files already reserved for dispatch are excluded from this retry.
- **Input missing / unsupported file / upload limit:** review ChatGPT's error. Compress to MP4 if useful or wait for the account limit to reset. Use **Show next 3 clips (manual)** or **Open output folder** to drag/select the exact intended files. The manual queue is independent of automatic reservations; check filenames to avoid duplicates.
- **Changed conversation / reload / inactive tab:** no new transfer is permitted for another URL, document, frame, or tab. Arm the intended conversation again. Closing Brave clears arming; saved pairing settings remain.
- **Missing queued output:** the queue stops at that file rather than silently counting it as attempted. Restore it or clear and reprocess the queue.
- **Lost reservation history / damaged history file:** the bridge reports an error rather than silently resetting uncertain attempts. Back up `%LOCALAPPDATA%\ClipBridge\brave-attempts.json` before repairing it. Clearing history can cause repeats.
- **Stop:** disable the extension and uncheck the Windows bridge. The listener closes. No local transfer service runs by default.

## Security and limits

- Only `127.0.0.1:38419` is bound. Authenticated requests require a private 256-bit pairing key and an extension ID. Origin-less Chromium GETs also require browser-controlled `Sec-Fetch-Site: none`; normal webpage origins (including ChatGPT itself) are rejected.
- The MV3 service worker performs authenticated requests under its loopback host permission. Its ChatGPT-only host permission allows tab URL validation. The content script has no pairing-key access and no fetch proxy. Extension-local storage is restricted to trusted extension contexts; the key never enters the webpage DOM or page JavaScript.
- File retrieval also requires the current claim. The extension accepts transfer commands only from the armed top-level ChatGPT document. No arbitrary URL or path endpoint exists.
- Local videos are held in browser memory during an attempt. The extension caps transfers at 525 MB; this is a local safety ceiling, not a guarantee of ChatGPT's upload limit. Start with a very small clip.
- ChatGPT receives a file only through its own UI after you enable and arm. No videos, paths, keys, or browser credentials go to Cloudflare. Optional Cloudflare monitoring sends only its documented job metadata.
- This is an experimental unpacked extension, not a store-reviewed extension. Automated protocol tests are separate from a real Brave/Google/ChatGPT end-to-end test. See [validation](../docs/VALIDATION.md).

Networking uses the MV3 worker because [content-script requests remain subject to page-origin restrictions](https://developer.chrome.com/docs/extensions/develop/concepts/network-requests). Pairing storage uses Chrome's [trusted-context access restriction](https://developer.chrome.com/docs/extensions/reference/api/storage).
