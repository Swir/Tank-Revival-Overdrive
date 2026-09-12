# v3.8.0 — DESTRUCTIBLE BATTLEFIELD & TACTICAL TERRAIN

## Major systems

- Added deterministic tactical terrain layouts for every campaign round.
- Added mobility-affecting Mud, Rubble, Ice and persistent Crater terrain.
- Added destructible tactical cover built from the existing `Obstacle` authority so all existing projectile, penetration and explosive rules continue to apply.
- Added armed mine lanes that can damage both player and enemy armor without introducing a parallel damage model.
- Added live vehicle binding so the player, enemies and friendly support units all react to tactical ground conditions.
- Existing artillery and mine impacts now leave craters that continue affecting movement until the bounded battlefield-destruction queue removes them.
- Added a compact tactical terrain HUD showing current layout, ground type, effective mobility, surviving cover and deployed mines.

## Gameplay impact

The battlefield is no longer only visual decoration. Routes through mud/rubble/craters are slower, cover can be deliberately destroyed to open firing lanes, mines create risk/reward corridors, and artillery permanently reshapes local movement during the round. These systems apply to both sides and integrate with the v3.7 friendly-support layer.

## Safety / authority

- `Projectile`, `Health`, `ArmorSystem` and `Obstacle` remain authoritative for combat damage.
- `Rigidbody2D` remains the movement/collision authority.
- Orzelek's immediate footprint is excluded from generated tactical terrain and mine placement.
- No stable release or `main` merge should occur until Windows x64 CI is green and runtime playtesting validates terrain pacing and vehicle movement.
