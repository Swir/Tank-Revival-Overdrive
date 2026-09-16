from pathlib import Path
import re

ROADMAP = Path("ROADMAP.md")
text = ROADMAP.read_text(encoding="utf-8")

marker = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', text, re.M)) != 1:
    raise SystemExit("Expected exactly one standalone SWIR roadmap marker")

# Idempotent verifier if scope was already registered by a previous run.
if "## v12.8 — Full-Stack Integration & Release Train Hardening" in text:
    required = [
        "badge.svg?branch=dev-v12-8",
        "ROADMAP-97.9%25-yellow?style=for-the-badge",
        "DONE-367%2F375-1f6feb?style=for-the-badge",
        "STATUS-V12.8%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
        "███████████████████░ 97.9%",
        "| **367** | **8** | **375** | **97.9%** |",
    ]
    for token in required:
        if token not in text:
            raise SystemExit(f"Existing v12.8 scope is inconsistent: missing {token}")
    section = text.split("## v12.8 — Full-Stack Integration & Release Train Hardening", 1)[1]
    if section.count("- [ ]") != 8 or section.count("- [x]") != 0:
        raise SystemExit("Existing v12.8 section must contain exactly eight pending items and zero completed items")
    print("v12.8 scope already registered and consistent")
    raise SystemExit(0)

replacements = {
    "badge.svg?branch=dev-v12-7": "badge.svg?branch=dev-v12-8",
    "ROADMAP-100.0%25-brightgreen?style=for-the-badge": "ROADMAP-97.9%25-yellow?style=for-the-badge",
    "DONE-367%2F367-1f6feb?style=for-the-badge": "DONE-367%2F375-1f6feb?style=for-the-badge",
    "STATUS-V12.7%20QUALIFIED-brightgreen?style=for-the-badge": "STATUS-V12.8%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "████████████████████ 100.0%": "███████████████████░ 97.9%",
    "| **367** | **0** | **367** | **100.0%** |": "| **367** | **8** | **375** | **97.9%** |",
}
for old, new in replacements.items():
    if old not in text:
        raise SystemExit(f"Missing roadmap dashboard token: {old}")
    text = text.replace(old, new, 1)

anchor = "- [x] Dedicated Windows x64 v12.7 gate builds the exact candidate, runs packaged-EXE cinematic-combat smoke on a fresh Windows runner and records a passing runtime marker before qualification.\n"
if anchor not in text:
    raise SystemExit("Could not find v12.7 qualification anchor")

section = """

## v12.8 — Full-Stack Integration & Release Train Hardening — IN DEVELOPMENT
- [ ] **Unified release-train integration harness** — exercise the complete v12.0–v12.7 stack from one packaged executable instead of treating milestone gates as isolated binaries.
- [ ] **Round 80/90/100 full-stack soak** — verify Mobile Front, sustainment, route intelligence, Recon/EW, Mobile Signal, SIGINT, unified HUD and cinematic FX together under representative late-wave pressure.
- [ ] **Gameplay-authority audit** — prove Projectile, Health, movement/navigation and War Bond/economy ownership remain canonical with no duplicate damage, motion or reward paths introduced by integration.
- [ ] **Presentation-conflict audit** — verify unified HUD, world telegraphs, signal markers and cinematic combat feedback remain readable, bounded and non-overlapping when multiple operational systems are simultaneously active.
- [ ] **Bounded resource/performance contract** — enforce hard caps for integration pools, fixed buffers, world cues, AI helper groups and presentation density, including monotonic late-wave degradation without gameplay changes.
- [ ] **Deterministic integration diagnostics** — emit exact per-round subsystem/cap/authority results and stable PASS/FAIL markers suitable for CI triage and release-candidate attribution.
- [ ] **Single-binary Windows smoke matrix** — build one Windows x64 candidate and run every v12.0–v12.8 smoke probe against that exact packaged EXE on a fresh Windows runner.
- [ ] **Exact-SHA qualification finalizer** — qualify v12.8 only after the release-train gate is green for the pinned candidate SHA and the SWIR Roadmap Standard v1 dashboard/checklist is revalidated.
"""
text = text.replace(anchor, anchor + section, 1)

section_text = text.split("## v12.8 — Full-Stack Integration & Release Train Hardening", 1)[1]
if section_text.count("- [ ]") != 8 or section_text.count("- [x]") != 0:
    raise SystemExit("v12.8 scope must begin with exactly eight pending items and zero completed items")

ROADMAP.write_text(text, encoding="utf-8")

final_text = ROADMAP.read_text(encoding="utf-8")
required = [
    "<!-- SWIR-ROADMAP-STANDARD:v1 -->",
    "badge.svg?branch=dev-v12-8",
    "ROADMAP-97.9%25-yellow?style=for-the-badge",
    "DONE-367%2F375-1f6feb?style=for-the-badge",
    "STATUS-V12.8%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "## 📊 Overall progress",
    "███████████████████░ 97.9%",
    "| **367** | **8** | **375** | **97.9%** |",
]
for token in required:
    if token not in final_text:
        raise SystemExit(f"ROADMAP invariant missing after registration: {token}")
if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', final_text, re.M)) != 1:
    raise SystemExit("Roadmap marker count changed during registration")

print("Registered v12.8 scope: 367/375 = 97.9%, status IN DEVELOPMENT")
