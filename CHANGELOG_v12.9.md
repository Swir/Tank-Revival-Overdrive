# v12.9 — Component Damage & Emergency Repair Warfare

## Gameplay
- Extends canonical `ArmorSystem` module integrity into Operational / Damaged / Critical / Disabled states for engine, tracks, gun and ammunition rack while `Health` remains the sole vehicle-life authority.
- Adds deterministic ammunition-to-subsystem profiles across Basic, Twin, Armor Piercing, Explosive, Plasma, EMP and Incendiary ammunition with Front / Side / Rear context and bounded 10–62 module severity.
- Player and enemy movement consume module mobility state; reload and shared fire-control accuracy consume gun/ammo-rack state instead of maintaining a parallel penalty system.
- Adds a finite emergency-repair loop: two charges per vehicle, 38 integrity per successful repair, 2.6-second stationary channel, 10-second cooldown, interruption on damage/movement/death and no Health healing.
- Existing enemy AI authority is suspended only during a committed repair channel; the v12.9 director never moves a Rigidbody2D, spawns projectiles or modifies Health/economy state.

## Verification and release hardening
- Adds `ComponentDamageRepairCISmokeProbe` with the complete 7-ammo × 3-zone deterministic matrix, exact 70/35/12 threshold checks, handling/reload/fire-control integration and finite two-charge repair invariants including proof that Health is unchanged.
- Adds a deterministic exact-candidate Windows x64 packager with unique EXE/Data discovery, external staging, reproducible ZIP ordering, SHA-256 and JSON provenance plus fixture tests for missing/duplicate runtime trees and deterministic output.
- Adds a Windows gate that builds one Unity 6000.3.17f1 candidate and runs v12.9 component/repair smoke, v12.8 integration regression and the existing round 80/90/100 soak against the same packaged EXE.
- Adds exact-SHA roadmap qualification so the eight v12.9 deliverables can only become complete after a successful matching Windows gate.

## Authority contract
`Projectile`/`ArmorSystem` remain impact and armor authorities, `Health` remains survivability authority, `PlayerTank`/`EnemyTank` + `Rigidbody2D` remain movement authority, and existing economy systems remain reward authority. v12.9 only supplies bounded module state and finite recovery.
