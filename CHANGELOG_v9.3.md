# v9.3.0-dev — Siege Lines, Counter-Battery Warfare & Breach Operations

## Major gameplay systems
- Physical enemy siege batteries appear on selected non-boss rounds from the mid-campaign onward and use authoritative `Health`, collision and `TankGame.SpawnProjectile` paths.
- Friendly artillery from the v9.2 fortification network gains bounded counter-battery fire against live siege positions.
- Enemy siege volleys create short suppression windows around fortified lanes instead of permanent hidden debuffs.
- Existing Heavy/Siege/Elite enemies are retasked through `TacticalNavigationAgent` into bounded breach teams while the siege line remains alive.
- Late campaign can field two batteries, but battery count, breach actors, projectile cadence and suppression duration all remain hard-capped.

## Safety / integration
- Boss rounds are excluded from siege-line scheduling.
- No parallel damage, projectile, enemy roster, movement or health authority was added.
- Existing `TankGame`, `Health`, `Projectile`, `FortificationNetworkDirector`, `StrongpointTerritoryWarfareDirector`, `DynamicFrontlineTerritoryDirector` and `TacticalNavigationAgent` remain authoritative.
- Destroying batteries immediately removes their siege pressure and can break the current breach suppression window.

## Qualification plan
- Dedicated Windows x64 candidate gate builds and packages the exact branch candidate.
- Fresh Windows runner boots the exact packaged EXE with `-siege-line-smoke`.
- Standard Dev Windows Build remains required in parallel.
- ROADMAP scope is registered as four unchecked deliverables before qualification; completion is not claimed until Windows gates are green.
