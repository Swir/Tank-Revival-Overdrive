#!/usr/bin/env python3
"""Source/authority contract for v13.4 Tactical Terrain & Cover Warfare."""
from pathlib import Path
import re

ROOT = Path('.')
terrain = (ROOT / 'Assets/Scripts/TacticalTerrainV134.cs').read_text(encoding='utf-8')
smoke = (ROOT / 'Assets/Scripts/TacticalTerrainCISmokeProbe.cs').read_text(encoding='utf-8')
tank = (ROOT / 'Assets/Scripts/TankGame.cs').read_text(encoding='utf-8')
enemy = (ROOT / 'Assets/Scripts/EnemyTank.cs').read_text(encoding='utf-8')
obstacle = (ROOT / 'Assets/Scripts/Obstacle.cs').read_text(encoding='utf-8')
roadmap = (ROOT / 'ROADMAP.md').read_text(encoding='utf-8')
readme = (ROOT / 'README.md').read_text(encoding='utf-8')
template = (ROOT / 'assets/readme/progress-template.svg').read_text(encoding='utf-8')
evidence_path = ROOT / 'docs/qualification/V13_4_WINDOWS_QUALIFICATION.md'


def require(cond, msg):
    if not cond:
        raise SystemExit('v13.4 contract FAIL: ' + msg)


require('public const int PlannedRounds = 100;' in terrain, '100-round planner contract missing')
require('public const int DoctrineCount = 7;' in terrain, 'seven doctrine contract missing')
require('public const int MaxCoverNodes = 12;' in terrain, 'fixed 12-cover cap missing')
require('public const int CandidateSlotCount = 24;' in terrain, 'bounded candidate slot cap missing')
require('MinSafeLaneHalfWidth = 1.55f' in terrain and 'MaxSafeLaneHalfWidth = 2.25f' in terrain, 'safe-lane bounds missing')
require('IsReservedSafeLane' in terrain and 'BreachAwareDirection' in terrain, 'safe-route / breach intent missing')
require('ReactiveCoverBreachDirector.TryFindBestBreach' in terrain, 'canonical breach memory is not consumed')
require('root.AddComponent<Obstacle>()' in terrain and 'obstacle.Initialize(kind, hp)' in terrain, 'canonical Obstacle runtime integration missing')
require('Obstacle.StructuralDamageFor' not in terrain, 'terrain runtime must not duplicate Obstacle structural-damage calculation')
for forbidden in ('Health.Damage(', 'new Projectile', 'ProjectilePool.', 'SpawnEnemy(', 'RepairEagle(', 'OnBaseDestroyed('):
    require(forbidden not in terrain, 'forbidden authority path in TacticalTerrainV134.cs: ' + forbidden)

require('public static int CandidateSlotIndex' in terrain, 'candidate slot identity helper missing')
require('public void ApplyDeterministicPlan' in terrain, 'deterministic runtime-plan entry point missing')
require('public bool ValidateActiveOverlay(out string reason)' in terrain, 'live overlay validator missing')
require('_roundRoot.SetActive(false);' in terrain and 'Destroy(_roundRoot);' in terrain,
        'same-frame rebuild must deactivate old terrain root before deferred Destroy')
require('private int CompactActiveCover()' in terrain and '_destroyedCount += CompactActiveCover();' in terrain,
        'destroyed-cover compaction/accounting missing')
require('public int ActiveCoverCount' in terrain and 'public int DestroyedCoverCount' in terrain,
        'live terrain telemetry counters missing')
require('obstacle.Kind == ObstacleKind.Water && !collider.isTrigger' in terrain,
        'runtime validator does not guard water trigger semantics')
require('obstacle.Kind != ObstacleKind.Water && collider.isTrigger' in terrain,
        'runtime validator does not guard solid collider semantics')
require('PositiveMod(Mathf.Max(0, ordinal) + plan.SlotOffset, Mathf.Max(1, plan.CoverCount))' in terrain,
        'cover-kind rank must use the quota-preserving cyclic permutation')
require('PositiveMod(ordinal * 7 + plan.SlotOffset' not in terrain,
        'cover-kind quota aliasing stride returned')

require('V13_4_TACTICAL_TERRAIN_OK.txt' in smoke and '-tr-v134-smoke' in smoke, 'packaged smoke markers missing')
require('for (int round = 1; round <= 100; round++)' in smoke, '100-round packaged smoke coverage missing')
require('Obstacle.StructuralDamageFor' in smoke, 'smoke does not guard canonical Obstacle counterplay')
require('CandidateSlotIndex(a, ordinal)' in smoke and 'duplicate/invalid tactical slot' in smoke,
        'packaged smoke does not guard deterministic unique terrain slots')
require('cover kind budget mismatch round=' in smoke,
        'packaged smoke does not enforce exact brick/steel/water planner budgets')
require('runtimeDirector.ApplyDeterministicPlan(64' in smoke and 'runtimeDirector.ApplyDeterministicPlan(65' in smoke,
        'packaged smoke does not exercise live overlay and same-frame rebuild')
require('string overlayReason = string.Empty;' in smoke,
        'packaged smoke must initialize overlay diagnostics before any short-circuit condition')
require('bool firstOverlayValid = runtimeDirector.ValidateActiveOverlay(out overlayReason);' in smoke and
        'bool rebuiltOverlayValid = runtimeDirector.ValidateActiveOverlay(out overlayReason);' in smoke,
        'packaged smoke must evaluate both live-overlay validators before diagnostic conditions')
require('|| !runtimeDirector.ValidateActiveOverlay(out overlayReason)' not in smoke,
        'packaged smoke reintroduced a definite-assignment hazard through short-circuit validation')
require('!firstOverlayValid' in smoke and '!rebuiltOverlayValid' in smoke,
        'packaged smoke validation results are not enforced')
require('same-frame tactical overlay rebuild invalid' in smoke,
        'packaged smoke lacks lifecycle regression marker')

require('TacticalTerrainDirector.EnsureInstalled().BeginRound(this, round, _encounterPlan, _objectivePlan);' in tank,
        'TankGame round integration missing')
require(tank.index('BuildArena(round);') < tank.index('TacticalTerrainDirector.EnsureInstalled().BeginRound(this, round, _encounterPlan, _objectivePlan);'),
        'terrain overlay must be built only after canonical arena construction')
require('TacticalTerrainDirector.AdjustDirection(this, transform.position, _game.PlayerPosition, desired);' in enemy,
        'EnemyTank cover-aware intent integration missing')
require('BattlefieldCohesionDirector.AdjustDirection' in enemy and
        enemy.index('BattlefieldCohesionDirector.AdjustDirection') < enemy.index('TacticalTerrainDirector.AdjustDirection'),
        'v13.4 must layer after v13.3 cohesion intent')
require('ObstacleImpactResult ResolveProjectileImpact' in obstacle and 'ReactiveCoverBreachDirector.ReportBreach' in obstacle,
        'canonical Obstacle authority/breach publication regressed')
require('public ObstacleKind Kind { get; private set; }' in obstacle,
        'canonical obstacle type introspection contract missing')

require(len(re.findall(r'^<!-- SWIR-ROADMAP-STANDARD:v1 -->$', roadmap, re.M)) == 1, 'roadmap standard marker missing/duplicated')
require(not re.search(r'(?m)^[█▓▒░]{6,}(?:\s+[0-9.]+%)?$', roadmap), 'legacy character progress meter returned to ROADMAP')
require(not re.search(r'(?m)^[█▓▒░]{6,}(?:\s+[0-9.]+%)?$', readme), 'legacy character progress meter returned to README')
require(roadmap.count('assets/readme/progress-mini.svg') == 1, 'ROADMAP must embed exactly one progress-mini.svg')
require(readme.count('assets/readme/progress-card.svg') == 1, 'README must embed exactly one progress-card.svg')
require('progress-template.svg' not in roadmap and 'progress-template.svg' not in readme, 'template must not be embedded as project progress')
require('Template' in template and 'N/A' in template, 'progress template must remain visibly marked as TEMPLATE/N/A')
require(len(re.findall(r'^<!-- SWIR-README-STANDARD:v2 -->$', readme, re.M)) == 1 and '## 🔎 Search Keywords' in readme, 'README PRO v2/Search Keywords regressed')

open_state = all(token in roadmap for token in (
    'DONE-415%2F423', 'ROADMAP-98.1%25-yellow', 'STATUS-V13.4%20IN%20DEVELOPMENT-yellow',
    '| **415** | **8** | **423** | **98.1%** |',
    '415 / 423 completed (98.1%) — V13.4 IN DEVELOPMENT'))
qualified_state = all(token in roadmap for token in (
    'DONE-423%2F423', 'ROADMAP-100.0%25-brightgreen', 'STATUS-V13.4%20QUALIFIED-brightgreen',
    '| **423** | **0** | **423** | **100.0%** |',
    '423 / 423 completed (100.0%) — V13.4 QUALIFIED'))
require(open_state != qualified_state, 'roadmap must be exactly one recognized v13.4 state')

if open_state:
    anchor = '## v13.4 — Tactical Terrain & Cover Warfare — IN DEVELOPMENT'
    require(anchor in roadmap, 'open v13.4 heading missing')
    section = roadmap[roadmap.index(anchor):]
    require(section.count('- [ ]') == 8 and section.count('- [x]') == 0, 'open v13.4 scope must contain eight pending items')
    require('415 / 423 completed (98.1%) — V13.4 IN DEVELOPMENT' in readme, 'README open-state fallback stale')
else:
    anchor = '## v13.4 — Tactical Terrain & Cover Warfare — QUALIFIED'
    require(anchor in roadmap, 'qualified v13.4 heading missing')
    section = roadmap[roadmap.index(anchor):]
    require(section.count('- [x]') == 8 and section.count('- [ ]') == 0, 'qualified v13.4 scope must contain eight completed items')
    require('423 / 423 completed (100.0%) — V13.4 QUALIFIED' in readme, 'README qualified fallback stale')
    require(evidence_path.is_file(), 'qualified state requires exact-candidate evidence file')
    evidence = evidence_path.read_text(encoding='utf-8')
    for token in (
        'ef891049f2c40bf797b4b6e7f38b12c1c25ec407',
        '35336689226',
        'd42a52ca0a6a6b26458c9362b9bcf75d1a223dd6fdd3a9989326e51d58c43d68',
        'sha256:90b986d315dc12fc26279fc6d751c2568b92b58ebee02700279b9fb0f05bdbfd',
        'success',
    ):
        require(token in evidence, 'qualification evidence missing: ' + token)

print('v13.4 tactical terrain source/authority/lifecycle/quota/compile-safety/SVG-only state contract: PASS')
