"""Fail CI when the Windows app and installer versions disagree."""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
project = (ROOT / "src/ClipBridge/ClipBridge.csproj").read_text(encoding="utf-8")
installer = (ROOT / "installer/ClipBridge.iss").read_text(encoding="utf-8")
changelog = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")

app = re.search(r"<Version>([^<]+)</Version>", project)
setup = re.search(r'#define AppVersion "([^"]+)"', installer)
if not app or not setup:
    sys.exit("Missing application or installer version")
if app.group(1) != setup.group(1):
    sys.exit(f"Version mismatch: app={app.group(1)}, installer={setup.group(1)}")
if f"## [{app.group(1)}]" not in changelog:
    sys.exit(f"CHANGELOG.md is missing version {app.group(1)}")
print("Version tracking consistent:", app.group(1))
