# ClipBridge — Windows Video Splitter

A **Windows-only** C# / WPF desktop app for splitting large videos (including 15 GB+ files) into smaller numbered clips. Processing runs locally with FFmpeg. No Android app, cloud backend, LocalSend integration, or OpenAI API key is required.

## Status

**Initial development version.** Source code is available for building and testing; a Windows build has not yet been verified. This is not a production release.

### Included
- Drag-and-drop and multi-select video queue.
- FFmpeg stream-copy splitting (fast mode; no transcoding).
- Adjustable output limit of 10–500 MB; default **400 MB**.
- Automatic numbering and separate output folder per input.
- Automatic retries with shorter segments if a part exceeds the size limit.
- Output size validation and cancellation.
- Opens ChatGPT in your default browser to allow manual attachment.
- GitHub Actions workflow for an unsigned portable Windows build.

### Not yet implemented
- Automatic file uploads or browser automation for ChatGPT.
- Compression/transcoding mode, resumable jobs, per-video percentage progress.
- Signed installer or bundled FFmpeg binaries.

## Requirements

Windows 10 or 11 (64-bit), .NET 10 SDK for local development, and **FFmpeg + FFprobe** installed on PATH or placed next to the published ClipBridge.exe.

FFmpeg: https://ffmpeg.org/download.html

## Run from source

```powershell
dotnet restore .\src\ClipBridge\ClipBridge.csproj
dotnet run --project .\src\ClipBridge\ClipBridge.csproj
```

## Publish portable app

```powershell
dotnet publish .\src\ClipBridge\ClipBridge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

Run `publish\ClipBridge.exe`. Install FFmpeg on PATH or place `ffmpeg.exe` and `ffprobe.exe` beside the executable. The GitHub Actions workflow publishes a downloadable build artifact when successful.

## Usage

1. Copy videos from your phone to your PC by any method you prefer.
2. Drag files into ClipBridge or select them using **Select videos**.
3. Set max clip size (default 400 MB) and an output directory.
4. Press **Split videos**.
5. Open the output directory and manually attach clips in ChatGPT.

## Important limitations

Fast mode uses FFmpeg's stream-copy segment muxer. Segments are cut at available keyframes, so very long keyframe intervals may prevent strict size compliance. The app retries with shorter durations and reports an error rather than claiming an oversized output is successful. Unsupported input codecs may require a future transcoding mode.

Leave adequate free disk space (potentially similar to the source size). Partial outputs may remain after cancellation. ChatGPT upload limits and video support vary by interface; this app does not bypass login or quotas.

**Security:** Never commit videos, authentication cookies, browser profiles, or passwords to GitHub.
