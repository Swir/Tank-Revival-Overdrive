from pathlib import Path

ROADMAP = Path("ROADMAP.md")
text = ROADMAP.read_text(encoding="utf-8")

marker = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
if text.count(marker) != 1:
    raise SystemExit(f"Expected exactly one {marker}, found {text.count(marker)}")

replacements = {
    "badge.svg?branch=dev-v12-5": "badge.svg?branch=dev-v12-6",
    "ROADMAP-100.0%25-brightgreen?style=for-the-badge": "ROADMAP-97.8%25-yellow?style=for-the-badge",
    "DONE-351%2F351-1f6feb?style=for-the-badge": "DONE-351%2F359-1f6feb?style=for-the-badge",
    "STATUS-V12.5%20QUALIFIED-brightgreen?style=for-the-badge": "STATUS-V12.6%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "████████████████████ 100.0%": "███████████████████░ 97.8%",
    "| **351** | **0** | **351** | **100.0%** |": "| **351** | **8** | **359** | **97.8%** |",
}

for old, new in replacements.items():
    if old not in text:
        raise SystemExit(f"Missing roadmap dashboard token: {old}")
    text = text.replace(old, new, 1)

anchor = "- [x] **v12.5 Windows x64 qualification** — source/authority contracts, Unity `6000.3.17f1` StandaloneWindows64 build and exact packaged-EXE runtime smoke are green before qualification.\n"
if anchor not in text:
    raise SystemExit("Could not find v12.5 qualification anchor")

section = """

## v12.6 — Battlefield Presentation Overdrive & Unified Tactical HUD — IN DEVELOPMENT
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

section_text = text.split("## v12.6 — Battlefield Presentation Overdrive & Unified Tactical HUD", 1)[1]
if section_text.count("- [ ]") != 8:
    raise SystemExit(f"Expected exactly eight pending v12.6 items, found {section_text.count('- [ ]')}")
if section_text.count("- [x]") != 0:
    raise SystemExit("v12.6 scope must begin with zero completed items")

ROADMAP.write_text(text, encoding="utf-8")

# Re-read the written file and enforce SWIR ROADMAP STYLE LOCK v1.
final_text = ROADMAP.read_text(encoding="utf-8")
required = [
    marker,
    "badge.svg?branch=dev-v12-6",
    "ROADMAP-97.8%25-yellow?style=for-the-badge",
    "DONE-351%2F359-1f6feb?style=for-the-badge",
    "STATUS-V12.6%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "## 📊 Overall progress",
    "███████████████████░ 97.8%",
    "| **351** | **8** | **359** | **97.8%** |",
]
for token in required:
    if token not in final_text:
        raise SystemExit(f"ROADMAP invariant missing after registration: {token}")

print("Registered v12.6 scope: 351/359 = 97.8%, status IN DEVELOPMENT")
