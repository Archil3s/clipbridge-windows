# Changelog

All notable ClipBridge changes are recorded here. This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Windows 0.6.8 stable post-send routing
- During confirmation only, wait through unsupported same-origin routes and destroyed execution contexts until a supported stable chat address appears. Keep origin restrictions and the bounded confirmation timeout.
- Bind submission to the canonical address captured with the confirmation snapshot instead of re-reading a moving browser URL in the predicate or after the wait.
- Record address-settling waits without private URL values. Add a 38-file regression with a delayed unsupported route and missing author attributes, requiring thirteen batches in the same chat.

### Windows 0.6.7 post-send continuation
- Match observed browser addresses by validated origin and conversation path, ignoring routing query/fragment values without persisting them. Keep configured URL validation strict.
- Confirm a unique rendered full batch message outside the composer when author attributes are unavailable. Require the cleared composer and conversation address as before.
- Observe a response start/end cycle as an alternative to author-based assistant counters; do not advance while responding.
- Test query-bearing first-send routing and conversations without author-role attributes.

### Windows 0.6.6 attachment readiness
- Recognize visible filenames without hidden or SVG-title text. Check a bounded surrounding composer area for sibling attachment cards and upload progress, excluding conversation history and other visible editors.
- Use the same attachment area for empty-draft and upload-completion checks. Keep name multiplicity, enabled Send, unchanged text and upload-progress guards.
- Add synthetic regressions for adjacent cards, decorated filenames, adjacent pending uploads with enabled Send and history-only cards.

### Windows 0.6.5 upload diagnostics
- Add Copy debug report during running and paused uploads. Include bounded phase history, readiness checks, queue counts, elapsed waiting time and home/chat mismatch indicators.
- Exclude raw exceptions, filenames, paths, chat text, full addresses and credentials. Keep reports in memory and copy only on user request.
- Add bounded-history and privacy regression tests.

### Windows 0.6.4 High control detection
- Recognize visible High labels on semantic buttons, nested labels and adjacent composer toolbars. Ignore hidden labels and SVG titles; keep ambiguous controls paused before attachment.
- Add isolated browser regressions for these three layouts. Live ChatGPT acceptance remains unverified.

### Windows 0.6.3 same-chat batch submission
- Recognize numbered attachment display names such as `Part_001(3).mp4` and filenames split across nested elements. Keep filename counts distinct and reject missing or mismatched files.
- Select and verify High thinking using supported semantic menu or slider controls before attachment; pause when the control is unknown or the setting changes during upload.
- Press Enter once after all attachment names, upload indicators, unchanged batch text and enabled Send agree. Do not retry Enter or fall back to clicking Send when confirmation is uncertain.
- Save the new conversation address and submitted batch together in one atomic queue update. Continue remaining batches in that same chat after each response finishes; retain uncertain attempts for review.
- Recognize article-based conversation roles and show specific waiting reasons. Test three consecutive batches (3+3+1) in one new chat, renamed/nested filenames, High selection, persistent recovery and failed-send guards with synthetic pages and clips.

### Windows 0.6.2 manual sign-in handoff
- Open the dedicated Brave profile for manual sign-in without debugging flags or Playwright. After the user closes that window, Start reopens the saved profile for uploads. Preserve browser preferences with CDP `NoDefaults`; reuse an already-open automation conversation without navigation or reload.
- On resume, stop on a missing or ambiguous conversation instead of creating another tab. Leave the browser open and disconnect after every completed, stopped or failed run.
- Detect visible verification pages and pause for the user. Preserve unattempted clips and existing uncertain reservations; never interact with verification controls.
- Add manual-launch argument coverage, synthetic verification regressions and real CDP tests for the production session, retained page state, browser preferences and missing/ambiguous-target recovery. This does not establish that Cloudflare will accept a particular browser session.
- Bound test-profile cleanup retries while Chromium child processes release their files on Windows. Assertions and persistent cleanup failures still fail the test run.

### Windows 0.6.1 / extension 0.3.2 composer compatibility
- Recognize the live ChatGPT attachment and Send button labels when IDs and test IDs are absent. Keep ambiguous editors and conversation containers excluded.
- Handle the attachment popover's ordinary upload button as well as menu items, including its subtitle. Preserve the file-chooser path when multiple hidden inputs are ambiguous.
- Recognize labelled Stop controls and test that the next batch waits for response completion. Add a synthetic scenario based on the inspected live control structure; no live clips were submitted during inspection.

### Windows 0.6.0 Playwright uploads
- Add a dedicated automatic upload window with a saved prepared-clip queue, folder import, conversation link, batch size, per-batch message and Stop control.
- Attach prepared files directly from disk through Microsoft.Playwright 1.64.1. Automatically wait for visible upload readiness, send once, confirm the new user message and wait for the response before continuing.
- Use the installed Brave browser with a separate saved profile and loopback CDP connection. Keep the sandbox enabled and sign-in user-controlled. The extension is optional and its bridge is stopped when the Playwright window opens.
- Persist attachment/send reservations before browser actions, preserve uncertain attempts after interruption, and provide explicit reviewed retries. Continue from a new-chat URL only after the first sent message is confirmed.
- Add saved-queue and real Playwright/CDP regression tests using synthetic files/pages. Bundle the Playwright driver in the Windows installer.

### Extension 0.3.1
- Fix composer detection when the first editor is hidden or the visible message box uses a rich editor without a form. Recognize the composer submit button ID and keep sibling attachment chips within the detected container.
- Wait up to 30 seconds for a visible composer before claiming clips. Reject ambiguous editors and fields inside conversation messages or dialogs.
- Show the loaded version in the extension popup to make incomplete upgrades visible.
- Add 18 synthetic composer-layout checks, a rich-editor readiness check, and full batch regressions for formless and delayed composers. These are not live ChatGPT acceptance tests.

### Batch upload changes
- Add **Add prepared clips** to rebuild the attachment queue from saved outputs after an upgrade/restart without reprocessing the source.
- Add configurable multi-file batches (1–10 clips, capped at 1 GB total locally) and two concurrent local transfers using 2 MiB chunks. All files are offered together.
- Add explicit optional auto-send, off by default. Check all filenames, upload indicators, composer text and enabled Send state before recording and performing one send attempt. Continue only after observing the new user turn and assistant completion.
- Pause on limits, errors, missing readiness, draft edits, hidden tabs, changed conversations and uncertain sends. Preserve durable no-replay reservations and manual fallback.
- Default new Windows jobs to 100 MB targets and smaller-file CRF 32 compression; keep all existing outputs and B-roll functionality.
- Add batch protocol/atomic-reservation tests and nine sandboxed Chromium scenarios covering manual mode, multi-batch auto-send, pending uploads, disabled Send, missing files, errors, edits, navigation and existing drafts.

## [0.6.8] - 2026-10-31 (preview)

- Wait for a stable chat address after Enter and confirm against the captured snapshot. Test a full 38-file queue through the first-message route transition. Live continuation remains unverified.

## [0.6.7] - 2026-10-31 (preview)

- Fix post-send URL handling and message confirmation for unsupported author attributes. Live ChatGPT continuation remains unverified.

## [0.6.6] - 2026-10-31 (preview)

- Fix attachment-name readiness for adjacent cards and visible labels; preserve upload and history boundaries. Live ChatGPT acceptance remains unverified.

## [0.6.5] - 2026-10-31 (preview)

- Add compact local upload diagnostics and a Copy debug report button for diagnosing stalled batches.

## [0.6.4] - 2026-10-31 (preview)

- Expand High-control detection to semantic buttons, nested visible labels and adjacent composer toolbars. Test these layouts with synthetic uploads; live site acceptance remains unverified.

## [0.6.3] - 2026-10-31 (preview)

Adds High thinking selection and one automatic Enter per ready batch, handles numbered attachment display names, and durably binds all remaining batches to the same conversation. Live ChatGPT acceptance still requires verification.

## [0.6.2] - 2026-10-30 (preview)

Separates manual sign-in from the Playwright connection, preserves the open chat and browser preferences, and pauses on verification pages. Live Cloudflare and ChatGPT acceptance remain unverified.

## [0.6.1] - 2026-10-30 (preview)

Fixes detection of the current labelled ChatGPT composer and attachment popover in the Playwright uploader. Includes extension 0.3.2 with shared composer recognition improvements.

## [0.6.0] - 2026-10-30 (preview)

Adds the Playwright uploader described above: direct multi-file attachment, automatic Send, a saved queue and a separate Brave profile with user-controlled sign-in. This is an experimental website integration; live ChatGPT acceptance remains unverified.

## [0.5.0] - 2026-10-29 (preview)

Windows 0.5.0 and extension 0.3.0 add multi-clip batches and explicitly authorized auto-send. The single-clip/manual-only restrictions in the earlier experimental notes below are superseded. Live ChatGPT DOM behavior and account limits still require manual verification.


### Reliability fixes
- Move authenticated localhost networking into an MV3 service worker; restrict pairing storage to trusted extension contexts and reject webpage origins at the bridge.
- Restrict transfers to one explicitly armed top-level ChatGPT document. Transfer in bounded chunks with per-attempt claims and reject stale acknowledgements.
- Reserve attempts durably before DOM dispatch; pause for review after each clip and retain a three-attempt PC batch ceiling. Interrupted transfers can be explicitly retried without replaying reserved attempts.
- Invalid optional Cloudflare settings now disable monitoring without aborting local video processing. Remove the prefilled personal Worker URL.
- Remember custom B-roll library destinations, preserve existing tags/filtering and resolve nullable library warnings.
- Make the main Windows layout scrollable and add an explicit unattempted-transfer retry control.
- Add dependency-free .NET bridge/monitoring/library checks and Node extension tests to Windows CI; include the service worker in downloadable extension artifacts.


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

[Unreleased]: https://github.com/Archil3s/clipbridge-windows/compare/v0.5.0...HEAD
[0.5.0]: https://github.com/Archil3s/clipbridge-windows/releases/tag/v0.5.0
[0.4.0]: https://github.com/Archil3s/clipbridge-windows/releases/tag/v0.4.0
[0.3.0]: https://github.com/Archil3s/clipbridge-windows/commits/main
[0.2.0]: https://github.com/Archil3s/clipbridge-windows/commits/main
[0.1.0]: https://github.com/Archil3s/clipbridge-windows/commits/main
