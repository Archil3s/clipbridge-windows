"""Check that ClipBridge's pinned Anti Slop instructions remain complete."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / "antislop.md"
REQUIRED = [
    "AGENTS.md",
    "DESIGN.md",
    "CHANGELOG.md",
    "antislop.md",
    "third_party/anti-slop/LICENSE",
    "third_party/anti-slop/README.md",
    "third_party/anti-slop/VERSION",
    ".github/pull_request_template.md",
    "skills/antislop-ui/SKILL.md",
    "skills/antislop-copywriting/SKILL.md",
    "skills/antislop-human/SKILL.md",
    "skills/antislop-layoutmobile/SKILL.md",
    "skills/antislop-code/SKILL.md",
    "skills/antislop-ui/LICENSE-web-quality-skills",
    "skills/antislop-human/LICENSE-web-quality-skills",
    "skills/antislop-human/contrast-check.py",
    "skills/antislop-ui/references/CORE-WEB-VITALS.md",
    "skills/antislop-ui/references/WEB-BASELINE.md",
    "skills/antislop-human/references/A11Y-PATTERNS.md",
    "skills/antislop-human/references/WCAG.md",
]

missing = [p for p in REQUIRED if not (ROOT / p).is_file()]
if missing:
    raise SystemExit("Missing Anti Slop files: " + ", ".join(missing))

agents = (ROOT / "AGENTS.md").read_text(encoding="utf-8")
if agents.count("<!-- antislop:start -->") != 1 or agents.count("<!-- antislop:end -->") != 1:
    raise SystemExit("AGENTS.md must contain exactly one Anti Slop pointer block")
for p in REQUIRED:
    if p.startswith("skills/") and p.endswith("SKILL.md") and p not in agents:
        raise SystemExit(f"AGENTS.md does not reference {p}")

version = (ROOT / "third_party/anti-slop/VERSION").read_text(encoding="utf-8").strip()
attribution = (ROOT / "third_party/anti-slop/README.md").read_text(encoding="utf-8")
if version != "3.2.24" or "3005235d030b4793146ad9afe8d01045efd90e1d" not in attribution:
    raise SystemExit("Pinned upstream revision metadata differs from the vendored rules")
if len(CORE.read_text(encoding="utf-8")) < 50000:
    raise SystemExit("The Anti Slop core instructions appear incomplete")
print(f"Anti Slop v{version}: files, agent pointers and attribution verified")
