# Design guidance sources

ClipBridge selects a small, pinned subset of design tools to avoid competing instructions or unnecessary runtime dependencies.

## Impeccable: design critique and polish

- Repository: https://github.com/pbakaus/impeccable
- Pinned commit: `5e745ace7e151feb2a93d360d0281fe39448b857`
- Source skill metadata version at that revision: `4.8.0`
- License: Apache License 2.0; original [LICENSE](impeccable/LICENSE) and [NOTICE](impeccable/NOTICE.md) are retained.
- Curated, unmodified upstream reference texts: `critique.md`, `polish.md`, `audit.md`, `operate.md` and `craft-floor.md`, all under [impeccable](impeccable/).
- [Local project wrapper](../../skills/clipbridge-design-review/SKILL.md) explains how to use those references for the Windows and Cloudflare surfaces.
- **Not included:** the executable CLI, browser hooks, detectors, full skill, or binary updater. This is design guidance, not a claim of complete Impeccable plugin installation.

## Web Quality Skills: accessibility and evidence-based web audits

- Repository: https://github.com/addyosmani/web-quality-skills
- Pinned commit: `afa8da942115f2961fdbfa80807ea0b232ff6c00`
- License: MIT; original [LICENSE](web-quality/LICENSE) retained.
- Vendored skill: `skills/web-quality-audit/SKILL.md` and its source inspection script.
- Vendored accessibility skill, relocated to `skills/web-accessibility/SKILL.md`, plus its associated references.
- Vendored referenced measurement workflow: `skills/performance/references/MEASUREMENT.md`.
- Only use evidence-led claims. Lighthouse, Chrome DevTools, or CrUX data is never assumed available. A source inspection is not a live performance benchmark.

## Precedence and scope

For ClipBridge tasks: user request > real product/data/privacy requirements > `DESIGN.md` > Anti Slop's core quality constraints > curated design critique/reference materials. When two guides disagree, follow the product brief rather than merging two contradictory styles.

The docs are loaded by coding agents only when relevant. **No new code is executed in the Windows application or Cloudflare Worker**, and neither system uploads source video data as a result of this integration.

## Updating

These are pinned dependencies, not auto-updated. Update via a reviewed pull request: confirm new upstream licences, replace all selected references from the same commit, update commit IDs here, update `AGENTS.md` if paths change, run the guidance and UI checks, and record the change in `CHANGELOG.md`.
