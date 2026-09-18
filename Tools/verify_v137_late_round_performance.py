#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / 'Assets/Scripts/LateRoundPerformanceV137.cs'
SMOKE = ROOT / 'Assets/Scripts/LateRoundPerformanceCISmokeProbe.cs'
FX = ROOT / 'Assets/Scripts/MassBattleFxBudget.cs'
ROADMAP = ROOT / 'ROADMAP.md'
README = ROOT / 'README.md'
VERSION = ROOT / 'VERSION'


def require(cond: bool, message: str):
    if not cond:
        raise SystemExit('v13.7 verifier FAILED: ' + message)


def main() -> int:
    core = CORE.read_text(encoding='utf-8')
    smoke = SMOKE.read_text(encoding='utf-8')
    fx = FX.read_text(encoding='utf-8')
    roadmap = ROADMAP.read_text(encoding='utf-8')
    readme = README.read_text(encoding='utf-8')

    require(VERSION.read_text(encoding='utf-8').strip() == 'v13.7.0-dev', 'VERSION must be v13.7.0-dev')
    require('LateRoundPressureBandV137' in core and 'Normal = 0' in core and 'Dense = 1' in core and 'Critical = 2' in core, 'pressure bands missing')
    require('SampleCadenceSeconds = 0.50f' in core and 'RecoveryHoldSeconds = 4.00f' in core, 'bounded sample/hysteresis constants missing')
    require('GC.GetTotalMemory(false)' in core and 'GC.CollectionCount(0)' in core, 'allocation-aware telemetry missing')
    require('RuntimeBattleRegistry.RegisteredHealthCount' in core and 'CombatRoster.LivingEnemyCount' in core, 'event-backed density inputs missing')
    require('MassBattleFxBudget.ActiveExplosions' in core and 'WarfarePerformanceGovernor.Tier' in core, 'canonical pressure inputs missing')
    require('LateRoundPerformanceDirector.CurrentProfile' in fx, 'MassBattleFxBudget not integrated with v13.7')
    require('_trailTokens = profile.TrailTokens' in fx and '_microTokens = profile.MicroTokens' in fx and '_tacticalTokens = profile.TacticalTokens' in fx, 'profile token budgets not consumed')
    require('profile.ExplosionSparkCap' in fx and 'profile.ExplosionSmokeCap' in fx, 'explosion detail caps not consumed')
    require('-tr-v137-smoke' in smoke and 'V13_7_LATE_ROUND_PERFORMANCE_OK.txt' in smoke, 'packaged smoke contract missing')
    require('ProjectilePool.ValidateIntegrity' in smoke, 'pool-preserving smoke assertion missing')

    forbidden = ('SpawnEnemy(', '.Damage(', 'Destroy(enemy', 'SetActive(false)', 'Physics2D.', 'FindObjectsByType<')
    for token in forbidden:
        require(token not in core, f'performance director must not own gameplay/scene-scan path: {token}')

    profiles = re.findall(r'new LateRoundPerformanceProfileV137\(band, (\d+), (\d+), (\d+), (\d+), (\d+), ([0-9.]+)f\)', core)
    require(len(profiles) == 3, f'expected 3 fixed performance profiles, got {len(profiles)}')
    numeric = [tuple(float(x) for x in row) for row in profiles]
    critical, dense, normal = numeric[0], numeric[1], numeric[2]
    require(all(normal[i] > dense[i] > critical[i] for i in range(6)), 'Normal > Dense > Critical budgets must be strictly monotonic')
    require(critical[0] >= 2 and critical[1] >= 3 and critical[2] >= 4, 'critical mode cannot erase priority presentation')

    done = len(re.findall(r'^- \[x\] ', roadmap, re.M)); open_ = len(re.findall(r'^- \[ \] ', roadmap, re.M))
    require((done, open_, done + open_) == (439, 8, 447), f'roadmap must remain open at 439/447 before qualification, got {done}/{done+open_}')
    require('| **439** | **8** | **447** | **98.2%** |' in roadmap, 'roadmap numeric dashboard mismatch')
    require('V13.7%20IN%20DEVELOPMENT' in roadmap, 'roadmap status must remain V13.7 IN DEVELOPMENT')
    section = roadmap[roadmap.index('## v13.7 — Late-Round Performance & Battle Density Reforge — IN DEVELOPMENT'):]
    require(section.count('- [ ]') == 8 and section.count('- [x]') == 0, 'v13.7 items must remain unchecked before Windows qualification')
    require('<!-- SWIR-README-STANDARD:v2 -->' in readme and '## 🔎 Search Keywords' in readme, 'README PRO v2/Search Keywords missing')
    require(readme.count('assets/readme/progress-card.svg') == 1 and roadmap.count('assets/readme/progress-mini.svg') == 1, 'SVG embedding contract mismatch')

    print('v13.7 late-round performance source contract: PASS')
    print('roadmap: 439/447 (98.2%) V13.7 IN DEVELOPMENT')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
