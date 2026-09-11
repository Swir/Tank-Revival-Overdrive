# v3.3.0 — BOSS LEGENDS & MULTI-PHASE WAR MACHINES

## Major gameplay systems

- Ten named boss identities tied to rounds 10/20/.../100: Iron Jackal, Twin Viper, Ash Warden, Storm Ram, Crimson Bastion, Black Hydra, Frost Reaper, Night Tyrant, Burning Crown and Overdrive Zero.
- Every boss now has four health-driven combat phases (100–75%, 75–50%, 50–25%, critical) with escalating cadence, hazards and presentation.
- Boss weak points are real trigger colliders integrated directly into `Projectile` damage resolution. Hitting them amplifies player damage and still emits `Projectile.DamageResolved`, preserving Combat Mastery and kill telemetry.
- Weak points unlock progressively by phase and later bosses expose additional drive/command-core targets.
- Each legendary boss receives a distinct attack identity using the existing authoritative projectile/ammo systems: fan barrages, twin-lane salvos, incendiary volleys, EMP patterns, explosive siege fire, radial Hydra bursts, AP Reaper volleys, plasma rings and Overdrive Zero hybrid attacks.
- Phase 2+ bosses gain telegraphed battlefield hazards that pressure either the player or Orzełek while respecting existing `Health` and campaign rules.

## Presentation

- New persistent boss HUD with legend name, boss HP, phase and active weak-point count.
- Distinct procedural 3D silhouette package for all ten legendary bosses: fangs, twin rails, ash stacks, ram blade, fortress armor, Hydra barrels, scythes, void nodes, crown spikes and the Overdrive Zero cross/core array.
- Boss 3D cores pulse harder through later phases and phase transitions produce readable shock/ring feedback and camera response.

## Integration and safety

- Existing `EnemyTank`, `BossWeaponController`, `ArmorSystem`, `CombatStatus`, `CampaignEncounterDirector`, `TankGame.SpawnProjectile`, Rigidbody2D/Collider2D and Orzełek loss paths remain authoritative.
- v3.3 is additive over green v3.2 and remains off `main` until Windows x64 CI passes and runtime balance/playtest is complete.
