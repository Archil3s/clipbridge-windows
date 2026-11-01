# ClipBridge roadmap

This is the forward-looking plan. Items are targets, not promises or shipped functionality. Refer to [CHANGELOG.md](CHANGELOG.md) for actual changes.

| Release target | Focus | Definition of done |
| --- | --- | --- |
| v0.4 preview | Reliability + monitoring | FFmpeg progress, disk preflight, local journal, opt-in Cloudflare heartbeat, version tracking; Windows tester feedback pending |
| v0.5 | Robust recovery and cloud dashboard | Checkpointed part-by-part processing, verified retention, Cloudflare KV + secrets configured, status visible on phone, clear offline state |
| v0.6 | ChatGPT attachment queue | Explicit user confirmation, tracking attempts vs accepted uploads, retry queue, per-conversation manual workflow; no unsupported login bypass |
| v0.7 | Preview / intelligent splitting | Thumbnail timeline, optional scene-change boundaries and trimmed clip preview |
| v0.8 | Performance | CPU/GPU benchmark, NVENC/QSV/AMF where available, codec compatibility tests, automated video fixture tests |
| v0.9 | Distribution | Updates via signed releases, installer smoke tests, rollback, diagnostics and fault reporting opt-in |
| v1.0 | Stable | End-to-end large-file test coverage, documentation, reproducible releases, security review |

## Principles

- Windows-only, native C# WPF, local FFmpeg processing.
- Do not upload original footage to Cloudflare.
- Prefer explicit opt-in and secure storage for future cloud integrations.
- Never put Cloudflare secrets or ChatGPT session information in GitHub.
- Processing correctness and verified clip sizes take priority over speed.
- Do not claim ChatGPT upload success until the platform confirms it; browser automation is experimental.
- No background work, remote PC execution, or cloud deployment without explicit authorised configuration.

## How to propose features

Open a GitHub issue with: problem, expected workflow, acceptance tests, edge cases, and whether the feature affects privacy or file handling. Mention the intended milestone. Document user-visible additions in CHANGELOG before a release.

## Brave attachment reliability

Windows 0.6.0 adds the requested Playwright workflow: a saved queue, direct disk attachment, a dedicated Brave profile, loopback CDP connection and automatic batch sends after readiness checks. The extension remains an optional alternative. See `docs/PLAYWRIGHT-UPLOADS.md` for setup, recovery and remaining live-site verification.

Implemented MV3 worker transport, explicit tab/document arming, durable atomic batch reservations, concurrent local transfers and multi-file dispatch. Optional auto-send is explicitly enabled and checks visible UI readiness/new turns; limits or unknown states pause. Remaining: real Brave/Google sign-in and account-specific ChatGPT acceptance/readiness verification. No official upload-completion API is used; manual fallback remains available.

## Folder split queue

Windows 0.6.10 adds folder selection and folder drag-and-drop, optional recursive discovery, cancellation and duplicate-path handling for the existing sequential splitter. Windows 0.6.12 collects all final clips directly in the selected output folder with unique part numbers and source-specific recovery records. Fresh Windows folder-picker/accessibility review and representative large-folder acceptance testing remain pending.

## Readability and QOL

Windows 0.6.11 adds readable dark control states and rendered WPF preview checks. Suggested next features (not implemented in this release) are recorded in docs/READABILITY-AND-QOL.md, prioritizing safe recovery, pause-after-batch, notifications and adjustable text size.

Windows 0.6.13 adds direct clip-folder loading, visible import results and fixed upload status/actions. It improves browser handoff errors and compact connection diagnostics. Manual sign-in and live-site verification remain required.
