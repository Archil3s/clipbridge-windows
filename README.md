# ClipBridge — Windows Video Splitter

ClipBridge is a Windows-only C# / WPF desktop application that prepares large videos for smaller file-size uploads. Video work is performed locally using FFmpeg. The source video is left untouched. **Early tester build, v0.2.0**.

## Features

- Drag and drop or multi-select large video files (including 15 GB+).
- Batch splitting into numbered clips, with a configurable maximum size (400 MB default).
- Fast mode: FFmpeg stream-copy, without re-encoding.
- Compression mode: H.264 video / AAC audio, with quality presets (CRF 24, 28, 32).
- One-time compression before splitting to reduce total processing, plus size validation and adaptive segment-duration retries.
- A Windows **Inno Setup installer** and portable ZIP built by GitHub Actions.
- **Experimental** Playwright browser-assisted ChatGPT attachment: starts Microsoft Edge with a separate persistent local profile, which you sign into once yourself. Attempts to attach up to three files per action. **You must check attachment status and send manually**.
- Manual fallback: open ChatGPT and drag clips into the browser.

## Install from GitHub Actions

Go to **Actions → Build and package Windows app → latest successful run → Artifacts**. Download `ClipBridge-Installer`, unzip the artifact and run `ClipBridge-Setup.exe`. Windows SmartScreen may warn that the installer is not code-signed.

Alternatively download `ClipBridge-win-x64-portable`, extract the whole directory and run `ClipBridge.exe`.

**FFmpeg + FFprobe are still required separately.** Install them on your PATH, or put `ffmpeg.exe` and `ffprobe.exe` beside `ClipBridge.exe` in the installed app directory. See https://ffmpeg.org/download.html.

Microsoft Edge is required only for optional experimental browser-assisted attachments. There is no API key.

## Development requirements

- Windows 10/11, x64
- .NET 10 SDK
- FFmpeg + FFprobe available on PATH or next to the executable
- Microsoft Edge for browser-assisted attachment

```powershell
dotnet restore .\src\ClipBridge\ClipBridge.csproj
dotnet run --project .\src\ClipBridge\ClipBridge.csproj
```

Build:

```powershell
dotnet publish .\src\ClipBridge\ClipBridge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o .\publish
```

The GitHub Actions workflow handles publishing and compiling the installer.

## How to test

1. Transfer a video from your phone to your PC using any manual method.
2. Drag a small sample video into ClipBridge; set the target size (e.g. 100 MB).
3. Choose **Fast split** first. Check every numbered part plays and retains its audio.
4. Test **Compress — balanced (CRF 28)** with the same file, comparing total size and quality. Compression can be slower and may not always make already-compressed videos smaller.
5. Try a larger video, then a 15 GB original. Leave enough free disk space for intermediate compressed content and clips.
6. Click **Sign in to ChatGPT (Edge)**; sign in normally inside the Edge window and open the intended conversation.
7. Click **Attach next 3 clips (experimental)**. Check that ChatGPT accepted them, and **send the message manually**.
8. If that fails or ChatGPT blocks the file type, click **Manual ChatGPT** and use the normal browser attachment control.

## Limitations and privacy

- Browser automation is **not an OpenAI-supported integration** and may break with a ChatGPT UI change. Calling the file input does **not confirm the upload succeeded**; inspect the actual ChatGPT page.
- No background upload and no automatic Send. Each batch requires a user click.
- File upload quotas, platform availability, and accepted video formats are controlled by ChatGPT. ClipBridge cannot bypass them.
- A separate Edge browser profile is stored only on the PC at `%LOCALAPPDATA%\ClipBridge\ChatGptEdgeProfile`. Never upload this folder or authentication data to GitHub.
- Sparse keyframes, unusual codecs, or abrupt bitrate spikes can cause segments to exceed the size cap even after retries; in that case the app reports failure.
- Fast mode is lossless stream-copy; compressed mode is intentionally lossy. Source files are preserved.
- Temporary transcodes use local disk and may be sizeable. Cancellation may leave partial output clips.
- This is an unsigned early release. Windows installer smoke testing and real ChatGPT upload testing must be completed on the target PC.

## Repository safety

Do not commit recordings, auth cookies, credentials, browser profiles, or API keys. No LocalSend monitoring, cloud processing, Android app, or OpenAI API key is included.
