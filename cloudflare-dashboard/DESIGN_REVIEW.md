# Dashboard design review: status-first layout

This review applies the vendored Anti Slop v3.2.24 rules and ClipBridge's [DESIGN.md](../DESIGN.md). It records design intent and remaining validation, not a claim that the production Cloudflare deployment is already updated.

## What changed and why

| Previous behaviour | New behaviour | Purpose |
| --- | --- | --- |
| Promotional hero displaced the current job | Compact "Video processing" heading followed by the connection state and the job | See actual processing status on a phone without scrolling through an advertisement |
| Four identical metric cards duplicated job details | One primary job panel, one connection panel, one latest-update row | Hierarchy follows the most important task, not a generic dashboard template |
| Decorative circles and generic glyphs | Status lights paired with plain text and a simple video mark | Reduce visual noise without obscuring status meaning |
| Demo and real data shared similar presentation | Sample banner and repeated "Sample data" disclaimer; PC never says online in demo | Prevent invented numbers being mistaken for a real video |
| Connection errors could close the token dialog | Invalid/failed token keeps the dialog open with a specific explanation | Clear correction path |
| Phone inherited a cramped desktop layout | Mobile navigation, full-width job panel, reflowed details and touch-friendly controls | Make current job readable at narrow widths |

## Identity

- Dark charcoal surfaces remain because this is a technical monitor often viewed alongside a video editor.
- Mint is reserved for actionable links, the primary action and positive status; error and sample states use different colors with text labels.
- Segoe UI and the operating system's system sans-serif serve Windows familiarity and readability.
- No external fonts, icon libraries, image requests, trackers or animation packages.

## Data boundaries

- The browser only requests `GET /api/status` after a user supplies `VIEW_TOKEN`.
- A demo is sample data only. It is never labelled as connected.
- The website never requests original footage or accepts ChatGPT credentials.
- The Worker ingestion/read contract is unchanged; Cloudflare KV and secrets still require account-side setup.

## Anti Slop delivery gate

| Check | Result |
| --- | --- |
| Real product task defines hierarchy | PASS: job status is primary, setup secondary |
| Invented statistics, trust claims or false connection status | PASS: only an explicitly labelled sample uses example values |
| Keyboard focus and dialog affordances | PASS in code; automated browser tests cover Escape and focus |
| Contrast choices | PASS for checked text-on-panel pairs (normal text > 4.5:1 in sampled combinations) |
| Reflow at phone, tablet and desktop widths | Automated Chromium tests added; CI must pass before merging |
| Loading, empty, offline, failed and denied-token states | Implemented; browser tests cover major cases |
| No unrequested API or account changes | PASS: Worker code and Cloudflare account untouched |
| Production Cloudflare deploy | NOT VERIFIED: depends on existing GitHub integration and deployment settings |
| Real Windows to Cloudflare end-to-end status | NOT VERIFIED: requires real credentials, KV binding and PC testing |

Do not label this implementation as fully validated until its CI and Cloudflare production checks pass.
