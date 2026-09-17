from pathlib import Path
import re

ROADMAP = Path("ROADMAP.md")
text = ROADMAP.read_text(encoding="utf-8")
marker = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
heading = "## v13.2 — Adaptive Enemy Command & Counter-Doctrine Warfare"

if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', text, re.M)) != 1:
    raise SystemExit("Expected exactly one standalone SWIR roadmap marker")

pending_items = [
    "**Bounded combat-history telemetry** — retain a fixed recent-round history of objective outcome, player losses, Orzełek damage and kill pressure without scene scans, unbounded allocations or a parallel persistence/economy path.",
    "**Deterministic adaptive counter-doctrine planner** — derive readable enemy command doctrine from recent combat history plus the active v13.0/v13.1 encounter/objective plans, with hysteresis and repeat caps that prevent per-frame or per-round thrashing.",
    "**Canonical spawn-composition directives** — feed tightly bounded specialist composition, concurrency and cadence adjustments back through TankGame's existing spawn path; no second spawner, hidden reinforcements or extra boss authority.",
    "**Enemy AI posture integration** — let existing EnemyTank targeting, maneuver and reload decisions consume bounded command posture multipliers while EnemyTank/Rigidbody2D/Projectile remain the only movement and firing authorities.",
    "**Fair recovery and anti-snowball policy** — detect sustained player/Orzełek distress and permit a deterministic recovery doctrine that can only soften pressure within hard limits, never heal the player, delete enemies or grant scripted invulnerability.",
    "**Adaptive command HUD telemetry** — expose current doctrine, confidence/history pressure, bounded spawn delta and specialist intent through the existing tactical presentation language with fixed refresh and text budgets.",
    "**Packaged-EXE v13.2 runtime smoke** — validate synthetic history bands, doctrine determinism/hysteresis, budget caps, specialist directives, AI posture bounds and authority contracts, then rerun v13.1/v13.0/v12.9/v12.8 regressions plus rounds 80/90/100 soak on the same executable.",
    "**Exact-SHA Windows qualification** — deterministically package one Windows x64 candidate with provenance, require the complete v13.2 gate to pass and only then finalize the SWIR roadmap dashboard/checklist.",
]

if heading in text:
    required = [
        "badge.svg?branch=dev-v13-2",
        "ROADMAP-98.0%25-yellow?style=for-the-badge",
        "DONE-399%2F407-1f6feb?style=for-the-badge",
        "STATUS-V13.2%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
        "███████████████████░ 98.0%",
        "| **399** | **8** | **407** | **98.0%** |",
    ]
    for token in required:
        if token not in text:
            raise SystemExit(f"Existing v13.2 scope is inconsistent: missing {token}")
    section = text.split(heading, 1)[1]
    if section.count("- [ ]") != 8 or section.count("- [x]") != 0:
        raise SystemExit("Existing v13.2 section must contain exactly eight pending items and zero completed items")
    print("v13.2 scope already registered and consistent")
    raise SystemExit(0)

required_before = [
    "badge.svg?branch=dev-v13-1",
    "ROADMAP-100.0%25-brightgreen?style=for-the-badge",
    "DONE-399%2F399-1f6feb?style=for-the-badge",
    "STATUS-V13.1%20QUALIFIED-brightgreen?style=for-the-badge",
    "████████████████████ 100.0%",
    "| **399** | **0** | **399** | **100.0%** |",
]
for token in required_before:
    if token not in text:
        raise SystemExit(f"Missing qualified v13.1 dashboard token: {token}")

checked_before = len(re.findall(r'^- \[x\] ', text, re.M))
pending_before = len(re.findall(r'^- \[ \] ', text, re.M))
if (checked_before, pending_before) != (399, 0):
    raise SystemExit(f"Expected qualified 399/399 baseline, got checked={checked_before}, pending={pending_before}")

replacements = {
    "badge.svg?branch=dev-v13-1": "badge.svg?branch=dev-v13-2",
    "ROADMAP-100.0%25-brightgreen?style=for-the-badge": "ROADMAP-98.0%25-yellow?style=for-the-badge",
    "DONE-399%2F399-1f6feb?style=for-the-badge": "DONE-399%2F407-1f6feb?style=for-the-badge",
    "STATUS-V13.1%20QUALIFIED-brightgreen?style=for-the-badge": "STATUS-V13.2%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "████████████████████ 100.0%": "███████████████████░ 98.0%",
    "| **399** | **0** | **399** | **100.0%** |": "| **399** | **8** | **407** | **98.0%** |",
}
for old, new in replacements.items():
    if text.count(old) < 1:
        raise SystemExit(f"Dashboard replacement source missing: {old}")
    text = text.replace(old, new, 1)

section_lines = ["", "", heading + " — IN DEVELOPMENT"] + [f"- [ ] {item}" for item in pending_items] + [""]
text = text.rstrip() + "\n" + "\n".join(section_lines)

checked = len(re.findall(r'^- \[x\] ', text, re.M))
pending = len(re.findall(r'^- \[ \] ', text, re.M))
if (checked, pending, checked + pending) != (399, 8, 407):
    raise SystemExit(f"Checklist mismatch after scope registration: checked={checked}, pending={pending}, total={checked + pending}")

section_text = text.split(heading, 1)[1]
if section_text.count("- [ ]") != 8 or section_text.count("- [x]") != 0:
    raise SystemExit("v13.2 scope must begin with exactly eight pending items and zero completed items")

ROADMAP.write_text(text, encoding="utf-8")
final_text = ROADMAP.read_text(encoding="utf-8")
required_after = [
    marker,
    "## 📊 Overall progress",
    "badge.svg?branch=dev-v13-2",
    "ROADMAP-98.0%25-yellow?style=for-the-badge",
    "DONE-399%2F407-1f6feb?style=for-the-badge",
    "STATUS-V13.2%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "███████████████████░ 98.0%",
    "| **399** | **8** | **407** | **98.0%** |",
]
for token in required_after:
    if token not in final_text:
        raise SystemExit(f"ROADMAP invariant missing after registration: {token}")
if len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', final_text, re.M)) != 1:
    raise SystemExit("Roadmap marker count changed during registration")
print("Registered v13.2 scope: 399/407 = 98.0%, status IN DEVELOPMENT")
