# ClipBridge Auto Attach — Brave extension (experimental)

This extension **attempts to attach** locally prepared ClipBridge videos to a ChatGPT conversation in your **normal Brave browser**, without Playwright, `--no-sandbox`, Google-login automation, or a separate automated browser profile.

## Install once on Windows

1. Install the new **ClipBridge Windows** build. Get the `ClipBridge-Brave-Extension` artifact from the **same GitHub Actions build** as the installer.
2. Extract the extension ZIP into a folder. The folder must contain `manifest.json`, `content.js`, `popup.js`, and `popup.html`.
3. In normal Brave, open `brave://extensions`. Enable **Developer mode** and select **Load unpacked**. Choose the folder containing `manifest.json`.
4. In ClipBridge, tick **Allow ClipBridge to offer prepared clips to the Brave extension**. Then select **Copy extension pairing key**.
5. Pin the ClipBridge extension in Brave and open its popup. Paste the key and select **Save connection settings**.
6. Sign in to ChatGPT normally in Brave. Open the **intended conversation** and select the extension's **Arm this ChatGPT tab** button. You may need to refresh the ChatGPT page after installing the extension.
7. Split a video in ClipBridge. While the Windows app remains open and the tab is visible, up to **three clips** will be offered to ChatGPT's file picker.
8. **Verify all attachments finished uploading in ChatGPT. Send the message yourself.** After sending, select **Allow next 3 clips** in ClipBridge for another group.

**The extension cannot guarantee uploads were accepted.** It records an attachment *attempt*, not a successful upload. It never sends messages. If ChatGPT changes its file picker or blocks videos, use **Show next 3 clips (manual)** instead.

## Privacy and current limits

- ClipBridge's optional local transfer service listens on **127.0.0.1:38419** only. It is off by default and closes when you exit ClipBridge or uncheck the option.
- A random 256-bit key pairs the extension to that service. Keep the key private. The desktop key is stored in your Windows user profile; the extension key is stored in Brave extension-local storage. Neither is sent to Cloudflare.
- The extension is limited to `https://chatgpt.com/*`; it cannot read your Google login. It does not need passwords or browser cookies.
- It can fetch only queued video files exposed by the local service; no arbitrary file-path API exists.
- The extension opens source video bytes locally in browser memory and presents them to ChatGPT's file input. ChatGPT's own upload, security, account, and file-size policies still apply; there is no bypass.
- The current version does not monitor confirmed upload completion and does not handle complex site-specific attachment workflows reliably. **Always check the ChatGPT composer manually** before sending.
- Do not change conversations within an armed tab; a newly loaded page requires re-arming.
- This is an experimental user-installed **unpacked extension**, not a published or reviewed Brave Web Store extension.
- Use ClipBridge's manual attachment flow when the extension fails.
