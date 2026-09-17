# v12.9 — Component Damage & Emergency Repair Warfare

## Gameplay
- Extends canonical `ArmorSystem` module integrity into Operational / Damaged / Critical / Disabled states for engine, tracks, gun and ammunition rack while `Health` remains the sole vehicle-life authority.
- Adds deterministic ammunition-to-subsystem profiles across Basic, Twin, Armor Piercing, Explosive, Plasma, EMP and Incendiary ammunition with Front / Side / Rear context and bounded 10–62 module severity.
- Player and enemy movement consume module mobility state; reload and shared fire-control accuracy consume gun/ammo-rack state instead of maintaining a parallel penalty system.
- Adds a finite emergency-repair loop: two charges per vehicle, 38 integrity per successful repair, 2.6-second stationary channel, 10-second cooldown, interruption on damage/movement/death and no Health healing.
- Adds deterministic component-casualty tactics for enemies: Heavy/Siege/Boss can screen when mobility-critical, vulnerable mobile units disengage, weapon-disabled units hold or withdraw, and sufficiently safe mobility kills can enter the existing finite recovery channel. `EnemyTank` remains the sole firing/movement authority and the policy only changes intent, bounded speed scale and fire eligibility.
- Existing enemy AI authority is suspended only during a committed repair channel; the v12.9 director never moves a Rigidbody2D, spawns projectiles or modifies Health/economy state.

## Presentation
- Adds a bounded component-damage presentation bridge that consumes `ArmorSystem.ModuleIntegrityChanged` only and never owns gameplay state.
- Critical/Disabled/repair transitions reuse the existing v12.7 `CinematicCombatFeedbackDirector` 16-slot pooled damage language instead of creating an independent particle system.
- Module identity is reinforced with a fixed six-slot world-glyph pool for engine, tracks, gun and ammo-rack states plus repair confirmation; cue priority can replace a lower-value cue but cannot grow the pool.
- Presentation emitters are installed only for actors already accepted by the 128-actor component/repair registry, keeping discovery and visual state bounded during late-round mass battles.

## Verification and release hardening
- `ComponentDamageRepairCISmokeProbe` covers the complete 7-ammo × 3-zone deterministic matrix, exact 70/35/12 threshold checks, handling/reload/fire-control integration and finite two-charge repair invariants including proof that Health is unchanged.
- The packaged smoke also validates six representative casualty decisions (FightThrough/Screen/Hold/Disengage/Recover), disengagement direction, recovery/hold speed bounds, weapon-disable fire suppression, monotonic component-presentation severity, the six-slot glyph cap and v12.7 cinematic-pool bridge.
- Adds a deterministic exact-candidate Windows x64 packager with unique EXE/Data discovery, external staging, reproducible ZIP ordering, SHA-256 and JSON provenance plus fixture tests for missing/duplicate runtime trees and deterministic output.
- The Windows gate builds one Unity 6000.3.17f1 candidate and runs v12.9 component/repair/casualty/presentation smoke, v12.8 integration regression and the existing round 80/90/100 soak against the same packaged EXE.
- Exact-SHA roadmap qualification keeps the eight v12.9 deliverables open until a successful matching Windows gate proves the complete milestone.

## Authority contract
`Projectile`/`ArmorSystem` remain impact and armor authorities, `Health` remains survivability authority, `PlayerTank`/`EnemyTank` + `Rigidbody2D` remain movement authority, and existing economy systems remain reward authority. v12.9 only supplies bounded module state, casualty intent, finite recovery and read-only presentation.
