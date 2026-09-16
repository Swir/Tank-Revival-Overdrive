from pathlib import Path

ROADMAP = Path("ROADMAP.md")
text = ROADMAP.read_text(encoding="utf-8")

marker = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
marker_line = f"\n{marker}\n"
if text.count(marker_line) != 1:
    raise SystemExit(f"Expected exactly one roadmap marker comment line {marker}, found {text.count(marker_line)}")

replacements = {
    "badge.svg?branch=dev-v12-6": "badge.svg?branch=dev-v12-7",
    "ROADMAP-100.0%25-brightgreen?style=for-the-badge": "ROADMAP-97.8%25-yellow?style=for-the-badge",
    "DONE-359%2F359-1f6feb?style=for-the-badge": "DONE-359%2F367-1f6feb?style=for-the-badge",
    "STATUS-V12.6%20QUALIFIED-brightgreen?style=for-the-badge": "STATUS-V12.7%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "████████████████████ 100.0%": "███████████████████░ 97.8%",
    "| **359** | **0** | **359** | **100.0%** |": "| **359** | **8** | **367** | **97.8%** |",
}
for old, new in replacements.items():
    if old not in text:
        raise SystemExit(f"Missing roadmap dashboard token: {old}")
    text = text.replace(old, new, 1)

anchor = "- [x] Dedicated Windows x64 v12.6 gate builds the exact candidate, runs packaged-EXE presentation smoke on a fresh Windows runner and records a passing runtime marker before qualification.\n"
if anchor not in text:
    raise SystemExit("Could not find v12.6 qualification anchor")

section = """

## v12.7 — Cinematic Combat Feedback & Damage Language — IN DEVELOPMENT
- [ ] Pooled material-and-ammunition impact language differentiates Organic, Brick, Steel and Terrain hits across Basic, AP, HE, Plasma and EMP while Projectile remains the sole collision/damage authority.
- [ ] Layered combat cues add bounded shockwave rings, sparks, fragments and impact flashes whose scale and lifetime communicate ammunition weight without per-impact GameObject churn.
- [ ] Persistent vehicle/base damage language exposes Healthy, Damaged, Critical and Burning visual states from read-only Health ratio using pooled smoke/spark/fire pulses without modifying survivability.
- [ ] Adaptive combat-FX budget reduces active impact cues, particle density and refresh cadence under late-wave pressure while retaining minimum readability floors and fixed hard caps.
- [ ] Combat audio hierarchy maps impact material/ammunition significance onto existing BattleAudio cues with cooldown/priority limits so dense firefights remain readable instead of becoming louder spam.
- [ ] v12.7 integration consumes existing Projectile Fired/Impacted events plus Health state only; it does not spawn projectiles, call Damage, move gameplay Rigidbody2D objects or replace AI/navigation authority.
- [ ] Packaged runtime smoke validates the ammo/material style matrix, damage-state thresholds, budget monotonicity, pool caps, audio priority and authority boundaries across representative late-game pressure.
- [ ] Dedicated Windows x64 v12.7 gate builds the exact candidate, runs packaged-EXE cinematic-combat smoke on a fresh Windows runner and records a passing runtime marker before qualification.
"""
if "## v12.7 — Cinematic Combat Feedback & Damage Language" in text:
    raise SystemExit("v12.7 roadmap section already exists")
text = text.replace(anchor, anchor + section, 1)

section_text = text.split("## v12.7 — Cinematic Combat Feedback & Damage Language", 1)[1]
if section_text.count("- [ ]") != 8 or section_text.count("- [x]") != 0:
    raise SystemExit("v12.7 scope must begin with exactly eight pending items and zero completed items")

ROADMAP.write_text(text, encoding="utf-8")

final_text = ROADMAP.read_text(encoding="utf-8")
required = [
    marker_line,
    "badge.svg?branch=dev-v12-7",
    "ROADMAP-97.8%25-yellow?style=for-the-badge",
    "DONE-359%2F367-1f6feb?style=for-the-badge",
    "STATUS-V12.7%20IN%20DEVELOPMENT-yellow?style=for-the-badge",
    "## 📊 Overall progress",
    "███████████████████░ 97.8%",
    "| **359** | **8** | **367** | **97.8%** |",
]
for token in required:
    if token not in final_text:
        raise SystemExit(f"ROADMAP invariant missing after registration: {token}")

print("Registered v12.7 scope: 359/367 = 97.8%, status IN DEVELOPMENT")
