---
name: clipbridge-design-review
description: Review and improve ClipBridge's desktop UI or Cloudflare dashboard using curated Impeccable playbooks, the product's existing DESIGN.md, and Anti Slop.
---

# ClipBridge design review

Apply this when the task is a **UI critique, design refinement, layout change, user flow, or component polish**.

1. Start with `AGENTS.md`, `DESIGN.md` and the relevant current source or screenshot. The user's actual brief takes precedence.
2. Load `antislop.md` and its appropriate skills to filter generic layouts, false product claims, and inaccessible controls.
3. For specific design tasks, use the pinned Impeccable references under `third_party/design-guidance/impeccable/`:
   - `operate.md`: the appropriate product mode for the Windows utility and status dashboard.
   - `critique.md`: rank UX findings, distinguishing observations from speculation.
   - `polish.md`: refine an existing screen without an unrequested redesign.
   - `audit.md`: inspect accessibility, responsiveness and measurable technical problems.
   - `craft-floor.md`: check the design quality floor before changing UI.
4. Preserve real job state, Cloudflare auth expectations, and user data boundaries. Existing ClipBridge features are more important than decorative novelty.
5. Inspect rendered desktop and narrow/mobile layouts when applicable. Run the dashboard browser tests. For Windows WPF, request/record a true Windows UI test before declaring design validation complete.
6. Deliver ranked findings and tests. Clearly separate static inspection from actual measured evidence.

This is a **project-specific curated use of Impeccable's Apache-2.0 design playbooks**, not a complete installation of the Impeccable CLI. The upstream context binary, slash commands and 62-rule detector are intentionally not shipped. Do not claim those commands or detectors work unless installed separately and verified. See `third_party/design-guidance/README.md` for pinned sources and attribution.
