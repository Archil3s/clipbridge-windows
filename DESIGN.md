# ClipBridge design direction

This is a **baseline extracted from ClipBridge's existing interfaces**, not a proposed rebrand. The user may replace it with their preferred design direction. Anti Slop removes generic choices but does not select a visual identity.

## Purpose and audience

- **Windows app:** a focused, dependable utility that converts large source videos into numbered clips. Primary flow: select files → choose split/compression → check free space → start → track progress → review outputs.
- **Cloudflare dashboard:** a read-only, phone-friendly status view when away from the PC. Primary questions: Is my PC reporting? Which job is processing? How much is complete? Are there errors?
- Users may be handling 15 GB+ recordings, slow encodes, intermittent networks and limited storage. Make actual status and failure recovery legible.

## Existing visual identity (preserve unless asked to change)

### Windows WPF

- Current dark-neutral base: application window `#111827`, panels `#1F2937`, text near `#F9FAFB`, muted text `#94A3B8`.
- Current primary action accent: blue `#0284C7` (with occasional cyan).
- Typography: Segoe UI / Windows system UI. Do not invent a second visual system for core dialogs.
- Prioritise content, real state and accessible controls over illustration.

### Cloudflare dashboard

- Current dark-charcoal base: `#0B0E12`; panels `#141920`; muted text `#85919F`.
- Existing accent: soft mint `#A4F4DC`.
- Existing system/UI sans typography and compact metrics may be refined, but avoid unrelated brand changes.
- Phone layout should put current job state and progress before decorative visuals.

## Interaction and copy rules

- **Truth before polish:** Never show invented percentages, numbers or 'Connected' status. Demo data must be labelled visible even in screenshots.
- **Specific action labels:** 'Split videos', 'Open output folder', 'Connect dashboard' rather than ambiguous promotional text.
- **Error messages:** identify the problem, affected job and next useful action; never falsely claim success.
- **Long operations:** show phase, processed percentage, cancellation behaviour, and what happens if interrupted.
- **UI states:** loading, empty, offline, error, partial completion, cancellation and success are first-class screens.
- **Accessibility:** keyboard access in WPF; visible focus, semantic labels, 4.5:1 contrast target for body text, pointer/touch sizing appropriate on phones.
- **Motion:** purposeful state change only; respect reduced-motion preferences on web.
- **Performance:** no unnecessary UI dependencies or animations during lengthy processing.

## Definition of quality

The interface should be calm and utilitarian. A new control or decoration must improve clarity, speed, accessibility, confidence or the user's actual video workflow. Prefer a simple, honest state over a visually rich fabricated one.

## Validation before release

Check at least one narrow desktop width, one phone width, keyboard focus/Tab behaviour and an offline dashboard. Review copy and statuses alongside screenshots. The automated build verifies source compilation, not usability or a full design audit.
