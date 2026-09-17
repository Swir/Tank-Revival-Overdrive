# Tank Revival: Orzeł Overdrive — v13.0 development changelog

## 100-Round Encounter Director & Boss Phase Warfare

### Deterministic encounter planning foundation

- Added `EncounterPlannerV130`, a pure bounded planner that deterministically describes all 100 campaign rounds.
- The campaign is divided into five 20-round acts: Mobilization, Breakthrough, Siege, Counteroffensive and Overdrive.
- Eight doctrines are represented across the campaign: Armored Assault, Hunter-Killer, Artillery Siege, Logistics Interdiction, Electronic Suppression, Route Breakthrough, Combined Arms and Boss Gauntlet.
- Every plan carries bounded enemy/concurrency/spawn budgets plus Eagle, route, logistics, EW and SIGINT pressure telemetry.
- Boss cadence remains every tenth round and therefore remains compatible with the existing `TankGame` round authority.
- `EncounterWarfareDirector` is a bounded read-mostly runtime service. It tracks at most the current 100-round plan and two boss-phase slots and does not spawn, damage, move or award economy.

### Multi-phase boss warfare

- Upgraded the existing `BossWeaponController` instead of adding a second boss/projectile authority.
- Bosses now have 3 phases in early boss rounds and 4 phases from round 50 onward.
- Health breakpoints at 70%, 42% and 18% escalate firing cadence and pattern tier. Critical mobility or a disabled weapon can force an earlier phase transition, tying v13.0 directly into v12.9 component warfare.
- EMP and disabled-weapon state still suppress boss fire through the existing status/armor authorities.
- Phase escalation changes only weapon cadence/pattern selection; projectile creation still routes through `TankGame.SpawnProjectile`.
- Boss transitions publish bounded runtime telemetry through `EncounterWarfareDirector` and receive a concise pooled-style ring/audio escalation cue.

### Qualification foundation

- Added `EncounterWarfareCISmokeProbe` (`-tr-v130-smoke`).
- The smoke validates all 100 deterministic plans, exactly ten boss rounds, all eight doctrines, five campaign acts, per-10-round doctrine diversity, hard encounter budgets, boss phase thresholds and component-driven phase escalation.
- PASS/FAIL markers are `V13_0_ENCOUNTER_WARFARE_OK.txt` and `V13_0_ENCOUNTER_WARFARE_FAIL.txt`.

## Qualification status

v13.0 remains **IN DEVELOPMENT**. The current implementation intentionally does not mark any v13.0 ROADMAP deliverable complete yet. Live `TankGame` spawn/round consumption, Orzeł defense escalation, cross-director doctrine adapters, HUD telemetry, the exact Windows single-binary gate and exact-SHA ROADMAP finalizer still require qualification before 391/391 can be claimed.

## Live encounter consumption
- `TankGame` consumes v13.0 plan budgets, deterministic doctrine composition and Orzelek pressure fortification; HUD exposes exact plan telemetry.
