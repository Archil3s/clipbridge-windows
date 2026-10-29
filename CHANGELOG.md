# Changelog

All notable ClipBridge changes are recorded here. This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Optional opt-in **Brave Auto Attach** browser extension and per-user authenticated localhost video transfer bridge (port 38419). Uses normal signed-in Brave, never Playwright, `--no-sandbox`, or Google login automation.
- Windows controls to enable/disable the bridge, copy the private pairing key, and authorise the next group of up to 3 clips.
- Extension popup for pairing, enabling/disabling and arming a specific ChatGPT tab. It attempts to supply prepared clips through the ChatGPT file input; it does not confirm successful uploads or send messages.
- GitHub Actions now packages the unpacked Brave extension with the Windows installer and checks extension JavaScript syntax.
- The manual clip-batch option remains available when attachment via the extension is unsupported or fails.

### Fixed
- Fixed Brave's `--no-sandbox` warning and Google sign-in rejection triggered by ClipBridge launching a Playwright-controlled Brave session.
- **Open ChatGPT in Brave** now launches the normal browser through Windows without Playwright, sandbox-disabling flags or a separate automation profile.
- Removed Playwright and experimental automated attachment code from the Windows app, replacing the attachment button with a truthful **Show next 3 clips (manual)** workflow that opens files for user-initiated ChatGPT upload.
- Former managed Brave profile data is left untouched but is no longer accessed. Cloudflare monitoring, B-roll cutting, and gardening tags remain unchanged.


### Changed
- Switched managed ChatGPT sign-in and experimental attachment attempts from Edge to **Brave Browser**.
- Brave is found in standard Windows per-user/Program Files installations or registered App Paths; custom installations can set `CLIPBRIDGE_BRAVE_PATH`. Missing Brave fails clearly instead of silently using Edge.
- Managed ChatGPT sessions use an isolated `%LOCALAPPDATA%\ClipBridge\ChatGptBraveProfile` without changing the existing Brave or Edge profiles; the manual ChatGPT shortcut also opens Brave.
- Updated the desktop button, status/errors and documentation. Browser-assisted uploads remain experimental and require user verification and manual sending.


### Added
- Windows B-roll cutter for selecting source footage, previewing it, marking In/Out times, exporting exact-range H.264/AAC MP4 excerpts, and saving clips into a reusable local library.
- Separate gardening-topic checkboxes for tagging new clips and filtering saved library clips (multiple selected topics match any); tags persist in adjacent local JSON files.
- Gardening categories: Planting, Harvesting, Watering, Compost & Soil, Pruning, Seedlings, Tools, Wildlife, Weeding and Garden Overview.
- A Windows-library browser with refresh, clear-filters, category folders, retroactive tagging of older saved MP4s and open-in-player actions. Existing sources are preserved; Cloudflare and ChatGPT upload functionality are unchanged.


### Changed
- Replaced the plain monitor layout with a screenshot-inspired ClipBridge processing console: charcoal workspace, vivid green status accents, compact navigation rail, live metrics, latest video row and configuration inspector.
- Added progress and completed-clip trend graphs from authenticated status updates observed during the current browser session (no fabricated CPU/GPU measurements or queue history).
- Clearly labelled sample-only charts and unavailable video preview; maintained existing Worker API and viewer-token security model.
- Reworked phone navigation into a compact, touch-friendly bottom bar and extended Playwright responsive/metric verification.

### Changed
- Rebuilt the ClipBridge Cloudflare monitor into a premium dark-and-mint, task-first responsive dashboard with compact phone header and anchored navigation.
- Added a large processing ring bound to real Cloudflare job progress, with a blank waiting state and unmistakably labelled sample preview.
- Consolidated desktop, API health, viewer access, and activity into clearer panels, preserving the existing authenticated Cloudflare status API and 5-second polling.
- Updated the Playwright browser suite for real-vs-sample progress and 320px mobile layouts.

### Added
- Cloudflare dashboard v0.3 adds a first-time connection guide with the exact Worker root, KV binding, and two secret names.
- Added an unauthenticated Worker API health check that explicitly does **not** imply storage, viewer access, or a connected PC.
- Added manual refresh for authenticated status views and a visible loading state while checking for updates.
- Improved recovery guidance for missing Worker deployments and expanded browser tests for setup and connection diagnostics.

### Added
- Pinned curated Impeccable critique/polish guidance and Web Quality Skills accessibility/audit playbooks, with upstream licences and versioned attribution.
- Added an automated axe-core accessibility check to the Cloudflare dashboard's Playwright test suite for each future UI change.
- Updated coding-agent instructions to use targeted design reviews without changing runtime UI frameworks or adding unverified upstream tools.

### Changed
- Reworked the Cloudflare monitor around the current video job rather than a promotional landing-page hero, following the repository's Anti Slop and design guidance.
- Replaced decorative icon and card repetition with a smaller set of functional panels, legible status labels, and a mobile-first processing view.
- Made the demo preview unmistakable and kept all disconnected, offline, loading, failed and token-error states separate from genuine live status.
- Improved token connection handling, keyboard/focus behaviour, and accessible progress announcements without changing the existing status API.
- Added automated Chromium desktop and mobile browser tests for layout, demo, authentication errors and live/offline states.

### Changed
- Integrated pinned Anti Slop v3.2.24 agent guidance, including UI, accessibility, responsive layout, copywriting and code-comment skills.
- Added root `AGENTS.md`, project `DESIGN.md`, upstream licence/attribution, and pull-request quality checklist for future contributions.
- No changes to Windows runtime, Cloudflare deployment or existing UI were made by this integration.

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
