# ClipBridge agent instructions

This file applies to the whole repository. Read it before changing code, copy or interface design.

## Product boundaries

- ClipBridge is a **Windows-only C# / WPF video utility**. FFmpeg processes files locally.
- The Cloudflare dashboard is a **separate Worker/web frontend** for opt-in status reporting, not a video hosting service.
- Never transmit original videos, paths containing private directory names, ChatGPT session cookies or credentials to a remote service. Public dashboard values must reflect actual data or be prominently marked as a **demo**.
- The ChatGPT browser attachment feature is experimental. Attempting an attachment is not proof of successful upload.
- Prefer correct file-size checks, recovery behaviour and error reporting over flashy effects or unverified claims.
- Do not introduce Android, Flutter, a new framework, or paid services without an explicit requirement.

## Design direction

Read [DESIGN.md](DESIGN.md) before changing UI or user-facing text. It documents the existing visual direction for the WPF app and Cloudflare dashboard, not permission for an unrequested redesign.

## Anti Slop — pinned project rules

<!-- antislop:start -->
Use the vendored **Anti Slop v3.2.24** guidance for every relevant contribution. It is a quality filter, not a substitute for product requirements or accessibility tests.

- Core: [antislop.md](antislop.md) — always use for user-facing changes.
- UI / visual: [skills/antislop-ui/SKILL.md](skills/antislop-ui/SKILL.md).
- Text / copywriting: [skills/antislop-copywriting/SKILL.md](skills/antislop-copywriting/SKILL.md).
- Accessibility / human: [skills/antislop-human/SKILL.md](skills/antislop-human/SKILL.md).
- Phone / responsive layouts: [skills/antislop-layoutmobile/SKILL.md](skills/antislop-layoutmobile/SKILL.md).
- Comments: [skills/antislop-code/SKILL.md](skills/antislop-code/SKILL.md).

Project workflow convention: apply the rules **during** new implementation. If asked for an audit, apply them **after** to identify and report issues before changing unrelated code. A user's explicit instructions take precedence over this convention. Finish changes with a concise pass/fail quality review, noting anything not actually tested. Do not claim a UI was tested merely because the C# project compiled.

The third-party sources are pinned and attributed in [third_party/anti-slop/README.md](third_party/anti-slop/README.md). Changes to the vendored files should happen only as an intentional, reviewed upstream update.
<!-- antislop:end -->

## Additional design and web-quality reviews

Use the curated **Impeccable** guidance and **Web Quality Skills** only when relevant, rather than loading every rulebook for every code change:

- **UI and UX critique or refinement:** [skills/clipbridge-design-review/SKILL.md](skills/clipbridge-design-review/SKILL.md), supported by pinned Impeccable critique, operate, polish, audit and craft-floor references.
- **Cloudflare web performance/accessibility and source audits:** [skills/web-quality-audit/SKILL.md](skills/web-quality-audit/SKILL.md).
- **Accessibility changes and WCAG reviews:** [skills/web-accessibility/SKILL.md](skills/web-accessibility/SKILL.md) and its references.
- **Web performance measurement context:** [skills/performance/references/MEASUREMENT.md](skills/performance/references/MEASUREMENT.md).

These are pinned and attributed under [third_party/design-guidance/README.md](third_party/design-guidance/README.md). The Impeccable material is a curated playbook subset, **not its CLI or automatic detector**. Do not claim that an upstream CLI, live Lighthouse audit or real-user measurements ran unless it actually happened.

Precedence: explicit user brief and ClipBridge security/data requirements first, then DESIGN.md and Anti Slop's hard gates, then targeted design/quality playbooks. For the private monitor, prioritize honest state, user task completion, accessibility and privacy over marketing or SEO optimization.

When the Cloudflare dashboard changes, run the existing Playwright tests, including the accessibility scan. Report unresolved violations separately from actual verified passes.

## Before merging

1. Link the change to a requirement, issue or bug and avoid unrelated refactors.
2. Check interaction states: initial, empty, loading, success, error, cancelled, offline and disabled.
3. Check contrast, keyboard navigation/focus, readable copy and responsive layout where relevant.
4. Confirm all displayed numbers, processing percentages and connectivity labels are grounded in real state; clearly mark demonstration values.
5. For backend changes, test relevant failure modes and avoid leaking private data in logs.
6. Update CHANGELOG.md under **Unreleased** for user-visible or developer-workflow changes, and update ROADMAP.md if the scope changes.
7. Run relevant CI. State explicitly when real Windows, 15 GB, ChatGPT website, or Cloudflare live testing has not occurred.
