# v3.0.0 — ARMORED ARSENAL & VEHICLE SPECIALIZATION

## Major gameplay systems

- Four War Garage chassis now have genuinely different active combat identities instead of only passive stat bonuses.
- New shared Arsenal Energy resource (0–100) gained from real enemy kills, heavy targets, boss kills and round progression.
- New reactor Heat system driven by actual player projectile spawns; overheating temporarily blocks arsenal secondaries and forces cooling decisions.
- New chassis-specific secondary weapon on `R`:
  - Assault: Triple HE Salvo.
  - Bastion: Siege Breaker heavy explosive shell.
  - Scout: close-range EMP Burst using the existing CombatStatus system.
  - Hunter: high-velocity Rail Lance using the existing armor-piercing projectile pipeline.
- New chassis active ability on `Left Shift`:
  - Assault: Barrage Drive resupplies offensive ammunition and vents the reactor.
  - Bastion: Iron Citadel repairs the tank and creates a short emergency invulnerability window.
  - Scout: Ghost Overboost performs an EMP jammer pulse, vents heat and grants a short evasive protection window.
  - Hunter: Predator Lock loads AP/Plasma execution ammunition and vents the gun system.
- New full-energy `G` Overdrive abilities with unique maximum-output behaviors for every chassis.
- Overdrive is gated by both energy and reactor temperature to create an actual combat-resource loop instead of a free cooldown button.

## Integration

- Secondary weapons use `TankGame.SpawnProjectile`, existing `Projectile`, `ArmorSystem`, `Health`, `CombatStatus`, ammunition colors/effects and current damage rules.
- Energy rewards subscribe to real `EnemyTank.Health.Died` events and scale by enemy class.
- Reactor heat subscribes to the existing projectile spawn event and ignores distant player-team projectiles such as fortress fire.
- Existing War Garage chassis selection remains authoritative through `TankRevival.Garage.Chassis`.
- The system survives player respawns/round rebuilds and automatically reattaches to the current player tank.

## Presentation / HUD

- New compact Arsenal HUD shows chassis, energy, heat, secondary, ability, Overdrive readiness and reactor lock state.
- New procedural 3D reactor assembly is mounted on the player's tank and visibly reacts to energy, heat, active ability and Overdrive.
- Combat announcements clearly signal activation, insufficient energy, heat lockout and maximum-output mode.

## Safety / compatibility

- Existing Rigidbody2D/Collider2D combat remains authoritative.
- Orzełek destruction and campaign-loss paths are unchanged.
- No unstable changes are merged to main; Windows development CI must pass before promotion.
