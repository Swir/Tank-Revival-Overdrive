#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sensor = (ROOT / "Assets/Scripts/BattlefieldSensorFusionV136.cs").read_text(encoding="utf-8")
smoke = (ROOT / "Assets/Scripts/BattlefieldSensorFusionCISmokeProbe.cs").read_text(encoding="utf-8")
roadmap = (ROOT / "ROADMAP.md").read_text(encoding="utf-8")
readme = (ROOT / "README.md").read_text(encoding="utf-8")
version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()

def need(text: str, token: str, label: str) -> None:
    if token not in text:
        raise SystemExit(f"v13.6 verification FAILED: missing {label}: {token}")

for token, label in (
    ("public enum SensorContactStateV136", "contact state enum"),
    ("Unknown = 0", "unknown state"),
    ("Detected = 1", "detected state"),
    ("Tracked = 2", "tracked state"),
    ("Verified = 3", "verified state"),
    ("public static class BattlefieldSensorFusionPlannerV136", "pure planner"),
    ("public sealed class BattlefieldSensorFusionDirector", "runtime director"),
    ("MaxTrackedContacts = 24", "fixed contact cap"),
    ("MaxWorldMarkers = 8", "world marker cap"),
    ("SweepCooldownSeconds = 12.0f", "sweep cooldown"),
    ("SweepDurationSeconds = 2.60f", "sweep duration"),
    ("SweepRadius = 11.50f", "sweep radius"),
    ("CounterJamWindowSeconds = 3.0f", "counter-jam bridge"),
    ("BattlefieldWeatherDirector.Instance.CurrentPlan", "weather read-only integration"),
    ("TacticalTerrainMap.TerrainAt", "terrain read-only integration"),
    ("ReconElectronicWarfareDirector.Instance", "Recon/EW read-only integration"),
    ("recon.ApplyCounterJamming", "bounded existing EW bridge"),
    ("RuntimeBattleRegistry.EnemySnapshot", "event-backed enemy registry"),
    ("Input.GetKeyDown(KeyCode.C)", "active sweep control"),
    ("ContactMemorySeconds", "contact memory"),
    ("BuildMarkerSelection", "bounded marker prioritization"),
    ("FindAnyObjectByType<TankGame>()", "runtime game lookup"),
):
    need(sensor, token, label)

for forbidden, label in (
    (".Damage(", "direct damage"),
    ("SpawnProjectile(", "projectile spawning"),
    ("MovePosition(", "movement authority"),
    ("WarEconomyDirector.", "economy mutation"),
    ("Instantiate(", "unbounded runtime instantiation"),
    ("FindObjectsByType<EnemyTank>", "enemy scene scan"),
    ("new List<", "unbounded list allocation"),
    ("new Dictionary<", "unbounded dictionary allocation"),
):
    if forbidden in sensor:
        raise SystemExit(f"v13.6 verification FAILED: sensor layer contains forbidden {label}: {forbidden}")

# Arrays are fixed-cap fields; reject per-evaluation retained-array regressions.
if re.search(r"EvaluateContacts\(\).*?new bool\[", sensor, re.S):
    raise SystemExit("v13.6 verification FAILED: EvaluateContacts must not allocate retained arrays per tick")

for token, label in (
    ("-tr-v136-smoke", "smoke command flag"),
    ("V13_6_SENSOR_FUSION_OK.txt", "PASS marker"),
    ("V13_6_SENSOR_FUSION_FAIL.txt", "FAIL marker"),
    ("plans != 800", "100-round x enemy-kind matrix"),
    ("near > mid && mid > far", "distance monotonicity"),
    ("clear > storm && clear > crater", "weather/terrain monotonicity"),
    ("highRecon > lowRecon", "Recon/EW monotonicity"),
    ("bool sweepActive =", "loop sweep activation variable"),
    ("float sweepConfidence =", "sweep confidence variable"),
    ("sweepConfidence > noSweep", "active sweep benefit"),
    ("closeFloor < BattlefieldSensorFusionPlannerV136.TrackingThreshold", "close-range fairness floor"),
    ("bossFloor < BattlefieldSensorFusionPlannerV136.DetectionThreshold", "boss fairness floor"),
):
    need(smoke, token, label)

# Prevent the CS0136 regression that previously stopped the exact Windows candidate.
if re.search(r"\bbool\s+sweep\s*=", smoke) or re.search(r"\bfloat\s+sweep\s*=", smoke):
    raise SystemExit("v13.6 verification FAILED: ambiguous local 'sweep' reintroduces CS0136 shadowing risk")

if version != "v13.6.0-dev":
    raise SystemExit(f"v13.6 verification FAILED: VERSION is {version!r}, expected 'v13.6.0-dev'")

if "<!-- SWIR-ROADMAP-STANDARD:v1 -->" not in roadmap:
    raise SystemExit("v13.6 verification FAILED: SWIR roadmap marker missing")
if "<!-- SWIR-README-STANDARD:v2 -->" not in readme:
    raise SystemExit("v13.6 verification FAILED: SWIR README v2 marker missing")
if "## 🔎 Search Keywords" not in readme:
    raise SystemExit("v13.6 verification FAILED: Search Keywords missing")

done = len(re.findall(r"^- \[x\] ", roadmap, re.M))
open_ = len(re.findall(r"^- \[ \] ", roadmap, re.M))
total = done + open_
if (done, open_, total) != (431, 8, 439):
    raise SystemExit(f"v13.6 verification FAILED: roadmap counts {(done, open_, total)} != (431, 8, 439)")
if "| **431** | **8** | **439** | **98.2%** |" not in roadmap:
    raise SystemExit("v13.6 verification FAILED: roadmap numeric dashboard mismatch")
if "V13.6%20IN%20DEVELOPMENT" not in roadmap:
    raise SystemExit("v13.6 verification FAILED: roadmap status badge is not open v13.6")
section = roadmap[roadmap.index("## v13.6 — Battlefield Sensor Fusion & Contact Warfare — IN DEVELOPMENT"):]
if section.count("- [ ]") != 8 or section.count("- [x]") != 0:
    raise SystemExit("v13.6 verification FAILED: v13.6 checklist must remain 8 open / 0 completed before Windows qualification")
if roadmap.count("assets/readme/progress-mini.svg") != 1:
    raise SystemExit("v13.6 verification FAILED: roadmap must embed exactly one progress-mini.svg")
if readme.count("assets/readme/progress-card.svg") != 1:
    raise SystemExit("v13.6 verification FAILED: README must embed exactly one progress-card.svg")

print("v13.6 sensor fusion source/authority contract: PASS")
print(f"roadmap: {done}/{total} ({done/total*100:.1f}%) open={open_}")
