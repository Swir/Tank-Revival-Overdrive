from pathlib import Path
import re

ROADMAP = Path("ROADMAP.md")
MARKER = "<!-- SWIR-ROADMAP-STANDARD:v1 -->"
HEADING = "## v12.5 — Signals Intelligence Fire Support & Deception Raids — IN DEVELOPMENT"
SECTION = """

## v12.5 — Signals Intelligence Fire Support & Deception Raids — IN DEVELOPMENT
- [ ] **Dual-Relay SIGINT Triangulation** — combine both physical scout relays into bounded geometric bearing confidence instead of granting perfect map knowledge.
- [ ] **Physical True/Decoy Emitters** — deploy one real enemy fire-control emitter plus at most one deceptive transmitter using canonical `Health`, collision and kinematic `Rigidbody2D` authority.
- [ ] **False-Target Verification Loop** — require proximity/relay evidence to distinguish a true emitter from a decoy before high-confidence support can be committed.
- [ ] **Bounded SIGINT Fire-Support Window** — turn verified signal intelligence into short, finite player fire-support opportunities with telegraphing, cooldown and strict salvo caps.
- [ ] **Canonical Projectile Fire Missions** — execute fire missions only through existing `TankGame.SpawnProjectile` / `Projectile` authority; no parallel direct-damage path.
- [ ] **Deception Guard & Counter-SIGINT AI** — reuse eligible Fast/Elite/Sniper units through `TacticalNavigationAgent` for bounded emitter screens and anti-designation pressure.
- [ ] **Readable & Bounded Signal Runtime** — expose triangulation, emitter identity, deception risk and fire-support readiness in HUD while enforcing fixed asset/actor/state limits and round cleanup.
- [ ] **v12.5 Windows x64 qualification** — source/authority contracts, Unity `6000.3.17f1` StandaloneWindows64 build and exact packaged-EXE runtime smoke are green before qualification.
"""

s = ROADMAP.read_text(encoding="utf-8")
assert len(re.findall(r"^<!-- SWIR-ROADMAP-STANDARD:v1 -->$", s, re.M)) == 1
assert "<!-- ROADMAP-PROGRESS:START -->" in s
assert "<!-- ROADMAP-PROGRESS:END -->" in s
assert "## 📊 Overall progress" in s
assert "| ✅ Completed | ⏳ Remaining | 📦 Total | 🎯 Progress |" in s

if HEADING not in s:
    s = s.rstrip() + SECTION + "\n"

completed = len(re.findall(r"^- \[x\] ", s, re.M))
remaining = len(re.findall(r"^- \[ \] ", s, re.M))
total = completed + remaining
assert (completed, remaining, total) == (343, 8, 351), (completed, remaining, total)

s = re.sub(r"badge\.svg\?branch=dev-v12-4", "badge.svg?branch=dev-v12-5", s, count=1)
s = re.sub(r"ROADMAP-100\.0%25-brightgreen", "ROADMAP-97.7%25-7c3aed", s, count=1)
s = re.sub(r"DONE-343%2F343-1f6feb", "DONE-343%2F351-1f6feb", s, count=1)
s = re.sub(r"STATUS-V12\.4%20QUALIFIED-brightgreen", "STATUS-V12.5%20IN%20DEVELOPMENT-7c3aed", s, count=1)
s = re.sub(r"(?m)^[█░]{20} [0-9.]+%$", "███████████████████░ 97.7%", s, count=1)
s = re.sub(
    r"\| \*\*343\*\* \| \*\*0\*\* \| \*\*343\*\* \| \*\*100\.0%\*\* \|",
    "| **343** | **8** | **351** | **97.7%** |",
    s,
    count=1,
)

assert "ROADMAP-97.7%25-7c3aed" in s
assert "DONE-343%2F351-1f6feb" in s
assert "STATUS-V12.5%20IN%20DEVELOPMENT-7c3aed" in s
assert "███████████████████░ 97.7%" in s
assert "| **343** | **8** | **351** | **97.7%** |" in s
assert s.count(MARKER) == 1

ROADMAP.write_text(s, encoding="utf-8")
print("Registered v12.5 scope: 343/351 = 97.7%, 8 remaining, 20-segment bar verified")
