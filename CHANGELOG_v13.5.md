# v13.5.0-dev — Battlefield Weather & Visibility Warfare

## Scope

v13.5 adds deterministic combat-weather fronts across the 100-round campaign without replacing existing movement, projectile, health, terrain, round or fire-control authorities.

## Gameplay package

- Five deterministic weather profiles: Clear, Mist, Rain, Storm and Snow.
- Campaign/sector-aware weather severity with a hard adjacent-repeat guard.
- Weather traction is applied only inside canonical PlayerTank and EnemyTank Rigidbody2D movement paths.
- Rain/Snow/Storm can couple to existing TacticalTerrainMap states, with hard mobility floors.
- Player and enemy spread pressure is applied through existing FireControlBallistics paths; enemy reload receives a bounded timing modifier.
- Armor-Piercing and Plasma ammunition retain a measured precision advantage in severe visibility conditions, giving the player deliberate counterplay without free damage or aim assist.
- Bounded screen tint and at most 24 deterministic precipitation marks provide readable weather presentation without unbounded particles or persistent scene growth.
- Compact weather telemetry exposes visibility and traction without adding a permanent large HUD panel.

## Verification policy

The v13.5 roadmap stays IN DEVELOPMENT until one exact Windows x64 candidate passes the dedicated weather smoke, v13.4/v13.3 regressions and rounds 80/90/100 soak. Roadmap completion and public Release readiness remain separate gates.
