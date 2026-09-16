from pathlib import Path

ROADMAP = Path("ROADMAP.md")
text = ROADMAP.read_text(encoding="utf-8")

marker = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
if text.count(marker) != 1:
    raise SystemExit(f"Expected exactly one {marker}, found {text.count(marker)}")

replacements = {
    "branch-dev--v12--5-blue": "branch-dev--v12--6-blue",
    "tree/dev-v12-5": "tree/dev-v12-6",
    "ROADMAP-100.0%25-brightgreen": "ROADMAP-97.8%25-yellow",
    "DONE-351%2F351-brightgreen": "DONE-351%2F359-yellow",
    "STATUS-V12.5%20QUALIFIED-brightgreen": "STATUS-V12.6%20IN%20DEVELOPMENT-yellow",
    "`████████████████████` **100.0%**": "`███████████████████░` **97.8%**",
    "| Completed | 351 |\n| Remaining | 0 |\n| Total | 351 |\n| Progress | 100.0% |": "| Completed | 351 |\n| Remaining | 8 |\n| Total | 359 |\n| Progress | 97.8% |",
}

for old, new in replacements.items():
    if old not in text:
        raise SystemExit(f"Missing roadmap dashboard token: {old}")
    text = text.replace(old, new, 1)

anchor = "**Qualification:** exact v12.5 Windows x64 candidate passed packaged-EXE runtime smoke before roadmap finalization.\n"
if anchor not in text:
    raise SystemExit("Could not find v12.5 qualification anchor")

section = """

## v12.6 — Battlefield Presentation Overdrive & Unified Tactical HUD

**Status:** IN DEVELOPMENT

- [ ] Unified tactical command HUD consolidates Mobile Front, Sustainment, Route Intelligence, Recon/EW, Mobile Signal Warfare and SIGINT/fire-support states without creating a second gameplay authority.
- [ ] Context-priority presentation model promotes only the highest-value active alerts/objectives and collapses stale panels to reduce late-wave HUD clutter.
- [ ] Bounded world-space telegraph layer visualizes operational objectives, signal emitters, breach/counter-breach opportunities and fire-support danger/solution windows with deterministic hard caps.
- [ ] 2.5D objective presentation adds layered rings, route vectors, pulses and state-aware emphasis while preserving existing physics, Health, Projectile and navigation authority.
- [ ] Adaptive presentation budget scales HUD refresh cadence, telegraph count and pulse density against battle pressure/late rounds without unbounded per-frame allocations or scene scans.
- [ ] v12.0–v12.5 directors expose bounded read-only presentation snapshots/events needed by the unified HUD instead of duplicating gameplay state.
- [ ] Presentation runtime smoke validates priority ordering, budget monotonicity, caps, snapshot freshness and canonical authority boundaries across representative late-game rounds.
- [ ] Dedicated Windows x64 v12.6 gate builds the exact candidate, runs packaged-EXE presentation smoke on a fresh Windows runner and records a passing runtime marker before qualification.
"""

if "## v12.6 — Battlefield Presentation Overdrive & Unified Tactical HUD" in text:
    raise SystemExit("v12.6 roadmap section already exists")
text = text.replace(anchor, anchor + section, 1)

if text.count("- [ ]") < 8:
    raise SystemExit("Expected at least eight pending roadmap items after v12.6 registration")
if text.count("## v12.6 — Battlefield Presentation Overdrive & Unified Tactical HUD") != 1:
    raise SystemExit("Expected exactly one v12.6 section")

ROADMAP.write_text(text, encoding="utf-8")

# Final invariant checks for SWIR ROADMAP STYLE LOCK v1.
final_text = ROADMAP.read_text(encoding="utf-8")
required = [
    marker,
    "![CI](https://img.shields.io/badge/CI-branch-dev--v12--6-blue)",
    "![ROADMAP](https://img.shields.io/badge/ROADMAP-97.8%25-yellow)",
    "![DONE](https://img.shields.io/badge/DONE-351%2F359-yellow)",
    "![STATUS](https://img.shields.io/badge/STATUS-V12.6%20IN%20DEVELOPMENT-yellow)",
    "## 📊 Overall progress",
    "`███████████████████░` **97.8%**",
    "| Completed | 351 |",
    "| Remaining | 8 |",
    "| Total | 359 |",
    "| Progress | 97.8% |",
]
for token in required:
    if token not in final_text:
        raise SystemExit(f"ROADMAP invariant missing after registration: {token}")

print("Registered v12.6 scope: 351/359 = 97.8%, status IN DEVELOPMENT")
