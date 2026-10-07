#!/usr/bin/env python3
"""Reject rendered web pages and verify Debian license install inputs."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
LICENSES = ROOT / "licenses"
INSTALL = ROOT / "debian" / "cryptoguard-agent.install"
ALLOWED = {".txt", ".json", ".zip"}

def main() -> int:
    files = sorted(p for p in LICENSES.rglob("*") if p.is_file())
    bad = [p.relative_to(ROOT).as_posix() for p in files if p.suffix.lower() not in ALLOWED]
    install = INSTALL.read_text(encoding="utf-8")
    errors = list(bad)
    if any(line.strip().startswith("licenses/* ") for line in install.splitlines()):
        errors.append("debian/cryptoguard-agent.install uses an unrestricted licenses/* glob")
    for p in files:
        rel = p.relative_to(LICENSES).as_posix()
        if p.suffix.lower() in ALLOWED:
            if rel.startswith("windows-alpha3/"):
                expected = f"licenses/windows-alpha3/*{p.suffix}"
            else:
                expected = f"licenses/*{p.suffix}"
            if expected not in install:
                errors.append(f"license input is not explicitly packaged: {rel}")
    # This exact upstream plain-text notice is the replacement for the removed
    # GitHub-rendered HTML artifact.
    notice = LICENSES / "Mono.Posix.NETStandard-MIT.txt"
    if not notice.is_file() or b"<html" in notice.read_bytes()[:4096].lower():
        errors.append("Mono.Posix plain-text upstream notice is missing or rendered")
    if errors:
        print("DEBIAN LICENSE MATERIAL FAIL")
        for error in errors:
            print(error)
        return 1
    print(f"DEBIAN LICENSE MATERIAL PASS files={len(files)} rendered_pages=0")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
