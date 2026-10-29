"""Verify pinned third-party design references are present and discoverable.

This is an integrity check, not a substitute for an actual UI audit.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = [
    "skills/clipbridge-design-review/SKILL.md",
    "third_party/design-guidance/README.md",
    "third_party/design-guidance/impeccable/LICENSE",
    "third_party/design-guidance/impeccable/NOTICE.md",
    "third_party/design-guidance/impeccable/critique.md",
    "third_party/design-guidance/impeccable/polish.md",
    "third_party/design-guidance/impeccable/audit.md",
    "third_party/design-guidance/impeccable/operate.md",
    "third_party/design-guidance/impeccable/craft-floor.md",
    "third_party/design-guidance/web-quality/LICENSE",
    "skills/web-quality-audit/SKILL.md",
    "skills/web-quality-audit/scripts/analyze.sh",
    "skills/web-accessibility/SKILL.md",
    "skills/web-accessibility/references/WCAG.md",
    "skills/web-accessibility/references/A11Y-PATTERNS.md",
    "skills/performance/references/MEASUREMENT.md"
]
missing = [path for path in REQUIRED if not (ROOT / path).is_file()]
if missing:
    raise SystemExit("Missing design guidance: " + ", ".join(missing))

agents = (ROOT / "AGENTS.md").read_text(encoding="utf-8")
for path in [
    "skills/clipbridge-design-review/SKILL.md",
    "skills/web-quality-audit/SKILL.md",
    "skills/web-accessibility/SKILL.md",
]:
    if path not in agents:
        raise SystemExit("Missing AGENTS.md pointer to " + path)

source = (ROOT / "third_party/design-guidance/README.md").read_text(encoding="utf-8")
for revision in [
    "5e745ace7e151feb2a93d360d0281fe39448b857",
    "afa8da942115f2961fdbfa80807ea0b232ff6c00",
]:
    if revision not in source:
        raise SystemExit("Missing pinned source revision " + revision)

print("Curated design guidance integrity passed (2 pinned upstream repositories).")
