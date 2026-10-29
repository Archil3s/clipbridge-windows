# Changelog

All notable ClipBridge changes are recorded here. This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- Confirm FFmpeg first-run installation and 15 GB processing on a real Windows PC.
- Add true partial-job checkpoints, pause/resume and robust upload confirmation.
- Add GPU encoding, automated tests, and auto-updates.

## [0.4.0] - 2026-10-29 (preview)

### Added
- Real FFmpeg processing percentage and phase feedback for compression and splitting.
- Preflight free-space checks with a conservative estimate for large files.
- Local job journal that requeues interrupted inputs after restart and reuses validated completed output when source/settings match.
- Optional authenticated Cloudflare Worker status reporting at five-second intervals; no video or login data is sent.
- Windows fields for the Worker address and an in-memory ingest token.
- GitHub version checks, a release-tag workflow and documented future roadmap.

### Known limitations
- Interruptions mid-video restart that video from the beginning; partial encoding state is **not** resumable.
- Cloudflare reporting requires a configured Worker KV binding plus two separate secrets.
- Cloudflare KV is eventually consistent; dashboard updates are near-real-time, not guaranteed instantaneous.
- Video processing and automatic ChatGPT file attachment still require Windows smoke testing.
- No automatic app self-update yet.

## [0.3.0] - 2026-10-29

### Added
- One-time HTTPS download and SHA-256 verification of FFmpeg/FFprobe on first launch.
- Per-user local video-engine installation, without administrator privileges or .NET setup.

## [0.2.0] - 2026-10-29

### Added
- H.264/AAC compression presets.
- Experimental persistent Edge profile for user-triggered ChatGPT attachment attempts.
- Windows Inno Setup installer and portable GitHub Actions artifacts.

## [0.1.0] - 2026-10-29

### Added
- Initial C# WPF desktop app, drag-and-drop, batch fast splitting and clip-size verification.

## Release discipline

Each change belongs under **Unreleased** during development. Before a public release: move tested features into the dated version section, align `src/ClipBridge/ClipBridge.csproj` and `installer/ClipBridge.iss`, run CI and a Windows smoke test, then tag `vX.Y.Z`. GitHub Actions builds the installer and prepares an associated Release. Unverified features must be labelled preview or experimental.

[Unreleased]: https://github.com/Archil3s/clipbridge-windows/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/Archil3s/clipbridge-windows/releases/tag/v0.4.0
[0.3.0]: https://github.com/Archil3s/clipbridge-windows/commits/main
[0.2.0]: https://github.com/Archil3s/clipbridge-windows/commits/main
[0.1.0]: https://github.com/Archil3s/clipbridge-windows/commits/main
