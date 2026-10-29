# ClipBridge — Windows Video Splitter

ClipBridge is a Windows-only C# / WPF desktop application that prepares large videos for smaller file-size uploads. Video work is performed locally using FFmpeg. The source video is left untouched. **Early tester build, v0.4.0 (preview)**.

## Features

- Drag and drop or multi-select large video files (including 15 GB+).
- **Processing progress** from FFmpeg with compression/splitting phase and overall batch percentage.
- **Disk-space preflight** with a conservative estimate before each source starts.
- **Local recovery journal:** unfinished source files are requeued after a restart; validated completed outputs can be reused with matching settings (partial transcodes must restart).
- **Opt-in Cloudflare status reporting** via the separate Worker dashboard, sending metadata only (file name, progress and clip counts). Status is retried every five seconds while enabled. No video bytes go to Cloudflare.
- **CHANGELOG.md, ROADMAP.md, version checks, and tag-based GitHub Releases** for future updates.
- Batch splitting into numbered clips, with a configurable maximum size (400 MB default).
- Fast mode: FFmpeg stream-copy, without re-encoding.
- Compression mode: H.264 video / AAC audio, with quality presets (CRF 24, 28, 32).
- One-time compression before splitting to reduce total processing, plus size validation and adaptive segment-duration retries.
- A Windows **Inno Setup installer** and portable ZIP built by GitHub Actions.
- **Zero manual FFmpeg setup:** on first launch, ClipBridge downloads FFmpeg and FFprobe automatically and installs them for your Windows account, then reuses them on later launches.
- **Normal Brave browser sign-in:** opens ChatGPT in your already-installed Brave browser with no Playwright, `--no-sandbox`, or automation profile. Sign into ChatGPT directly in Brave.
- **Manual clip-batch handoff:** **Show next 3 clips (manual)** opens your output folder, identifies the next video files, and lets you drag or select them for upload yourself. The app does not attach files or submit messages.
- Manual fallback: open ChatGPT and drag clips into the browser.

## B-roll cutter and gardening library (new preview feature)

Select **B-roll cutter & library** in the Windows app to open a video preview, mark **In** and **Out** points, and export short, reusable shots. Give each clip a name and tick one or more gardening topics such as **Planting**, **Harvesting**, **Pruning**, **Seedlings**, **Tools** or **Compost & Soil**. ClipBridge stores the MP4 in a local category folder with a small adjacent JSON tag file.

The library has a second set of **topic checkboxes for filtering saved clips**. Checking several topics shows clips matching any checked topic; no selection shows all saved clips. No AI scene recognition is claimed: you tag shots manually when saving them. The original remains untouched.

See [B-roll and gardening filters guide](docs/BRollLibrary.md) for step-by-step instructions, folder details and preview limitations.

## Install from GitHub Actions

Go to **Actions → Build and package Windows app → latest successful run → Artifacts**. Download `ClipBridge-Installer`, unzip the artifact and run `ClipBridge-Setup.exe`. Windows SmartScreen may warn that the installer is not code-signed.

Alternatively download `ClipBridge-win-x64-portable`, extract the whole directory and run `ClipBridge.exe`.

**No manual .NET or FFmpeg installation is required.** The Windows build bundles the .NET runtime. On first launch with internet access, ClipBridge downloads the pinned FFmpeg 9.0.2 Windows Essentials package directly from gyan.dev (linked by ffmpeg.org), verifies the package against a SHA-256 checksum, and extracts `ffmpeg.exe` and `ffprobe.exe` to `%LOCALAPPDATA%\\ClipBridge\\Tools`. The download is approximately 110 MB. Later launches work offline once installed. No admin permissions or PATH edits are needed.

The third-party FFmpeg package is GPLv3 licensed; it is downloaded directly by the user rather than redistributed within this installer. The pinned package's SHA-256 is recorded in `src/ClipBridge/Core/DependencyInstaller.cs`. For a different FFmpeg release, update both the URL and its verified hash.

Brave Browser must be installed to open ChatGPT through ClipBridge. There is no API key. ClipBridge locates Brave in standard Windows folders or App Paths; a custom installation may set `CLIPBRIDGE_BRAVE_PATH` to the full path of `brave.exe`. The app opens the user's **normal Brave profile** through Windows, without Playwright or any custom browser flags. You can use your existing ChatGPT login or sign in normally. Google account sign-in security checks are controlled by Google; no bypass is attempted.

The previous automated `%LOCALAPPDATA%\\ClipBridge\\ChatGptBraveProfile` folder is **no longer opened**. Your normal browser profile is not modified by ClipBridge. The old folder is left untouched; you may remove it manually after verifying you do not need any data from it.

## Design and AI coding guidance

Future Codex or other coding-agent contributions should start with [AGENTS.md](AGENTS.md) and the existing product direction in [DESIGN.md](DESIGN.md). ClipBridge vendors the [Anti Slop](https://github.com/miqdadbadjuber/anti-slop) rulebook and selected skills at a [pinned, attributed upstream revision](third_party/anti-slop/README.md).

The rules help avoid generic interface copy, decorative UI with no purpose, fake success metrics, inaccessible controls and redundant comments. They do **not** automatically redesign the Windows app or Cloudflare dashboard, change the runtime, or remove the need for actual UI and video tests.

The repository also contains [curated Impeccable and Web Quality Skills references](third_party/design-guidance/README.md), used by future coding agents alongside Anti Slop. The Cloudflare UI build runs [accessibility and responsive browser checks](cloudflare-dashboard/tests/dashboard.spec.js). This does not install the full Impeccable CLI, change Windows UI controls or automatically redesign the website.

## Development requirements

- Windows 10/11, x64
- .NET 10 SDK
- An internet connection for the one-time first launch, unless FFmpeg + FFprobe are already beside the executable or in ClipBridge's tools folder
- Brave Browser for opening ChatGPT (file attachments are manual)

```powershell
dotnet restore .\src\ClipBridge\ClipBridge.csproj
dotnet run --project .\src\ClipBridge\ClipBridge.csproj
```

Build:

```powershell
dotnet publish .\src\ClipBridge\ClipBridge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o .\publish
```

The GitHub Actions workflow handles publishing and compiling the installer.

## Cloudflare live monitoring (optional)

The separate dashboard lives under `cloudflare-dashboard/` and is deployed as the Worker `clipbridge-windows`. First configure a Cloudflare KV binding named `STATUS_STORE`, and separate 32+ character Worker secrets `INGEST_TOKEN` (Windows app only) and `VIEW_TOKEN` (dashboard viewer). See [dashboard setup](cloudflare-dashboard/README.md).

In ClipBridge Windows: tick **Enable optional Cloudflare live monitoring**, enter your HTTPS Worker URL and its **INGEST_TOKEN**. The token stays in process memory and must be entered again on restart. The Windows app sends a small status JSON POST about every five seconds while open, never original video content. In the webpage, press **Connect dashboard** and enter the distinct **VIEW_TOKEN**. The website polls about every five seconds. Cloudflare KV provides eventually consistent near-live status; real-time delivery is not guaranteed.

Cloudflare monitoring is **disabled by default**. Do not paste a Cloudflare account API token in either input. No browser authentication or ChatGPT login data is shared. If Cloudflare Access protects the API, allow an explicitly authenticated machine-to-machine route for the status writer (do not disable the API protection).

## How to test

1. Install ClipBridge and open it. On the first run, the app downloads and verifies its FFmpeg video engine automatically. If the download fails, check your connection and click **Split videos** to retry.
2. Transfer a video from your phone to your PC using any manual method.
3. Drag a small sample video into ClipBridge; set the target size (e.g. 100 MB).
4. Choose **Fast split** first. Check every numbered part plays and retains its audio.
5. Test **Compress — balanced (CRF 28)** with the same file, comparing total size and quality. Compression can be slower and may not always make already-compressed videos smaller.
6. Try a larger video, then a 15 GB original. Confirm the disk estimate, percentage updates, cancellation, and recovery on app restart. Reusing an already completed input should not re-encode it. Leave enough free disk space for intermediate compressed content and clips.
7. Click **Open ChatGPT in Brave**. Your normal Brave window opens without automation flags. Sign into ChatGPT if needed.
8. Click **Show next 3 clips (manual)** to open the output folder and see the next three file names. Drag or choose those files in the ChatGPT conversation; confirm upload acceptance and **send the message yourself**.
9. If ChatGPT cannot accept a file, verify its file size/type and use ChatGPT's normal attachment control. ClipBridge does not bypass upload restrictions.

## Limitations and privacy

- Automated browser attachment has been removed because Playwright-launched Brave can trigger the browser's insecure-flag warning and Google may reject automated sign-in. ClipBridge now opens normal Brave and provides a **manual** clip selection workflow.
- ClipBridge does not upload attachments or send ChatGPT messages. The user must select files, wait for ChatGPT to accept them, and send the message.
- File upload quotas, platform availability, and accepted video formats are controlled by ChatGPT. ClipBridge cannot bypass them.
- The previous `%LOCALAPPDATA%\ClipBridge\ChatGptBraveProfile` browser profile is no longer used or accessed by the app. Do not upload any browser profile or authentication data to GitHub.
- Sparse keyframes, unusual codecs, or abrupt bitrate spikes can cause segments to exceed the size cap even after retries; in that case the app reports failure.
- Fast mode is lossless stream-copy; compressed mode is intentionally lossy. Source files are preserved.
- On first launch, ClipBridge downloads FFmpeg directly from a third-party provider; no original videos or credentials are uploaded as part of that setup. A network connection and free local disk space are needed for the one-time download.
- Brave must be installed for the in-app **Open ChatGPT in Brave** shortcut. You may always open ChatGPT in a browser yourself and drag clips into the conversation.
- Interrupted videos are requeued, but resume **from the beginning of that video**; only completed output sets are reused. There is no checkpointed mid-video resume yet.
- Cloudflare KV and both Worker secrets must be configured by the account owner before monitoring can connect; the app can't deploy or configure these credentials itself.
- Temporary transcodes use local disk and may be sizeable. Cancellation may leave partial output clips.
- This is an unsigned early release. Windows installer smoke testing and real ChatGPT upload testing must be completed on the target PC.

## Repository safety

Do not commit recordings, auth cookies, credentials, browser profiles, or API keys. No LocalSend monitoring, cloud processing, Android app, or OpenAI API key is included.

## Tracking revisions and future updates

- [CHANGELOG.md](CHANGELOG.md): shipped changes, known limitations and unreleased work.
- [ROADMAP.md](ROADMAP.md): planned priorities with acceptance criteria.
- [docs/RELEASING.md](docs/RELEASING.md): how to version, build, smoke-test and tag a release.
- The installer and app version are checked by CI to match the changelog.
- GitHub Actions creates preview artifacts on pushes and pull requests; **GitHub Releases only on an intentional `vX.Y.Z` tag**. No automatic self-update is implemented yet.
