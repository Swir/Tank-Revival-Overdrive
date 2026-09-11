# v2.5.0-dev — 3D COMBAT & VEHICLE MOTION

## Vehicle motion 3D
- Added `VehicleMotion3DDirector` and `TankMotion3DAnimator`.
- Procedural tanks now react to real simulation movement with visual suspension pitch/roll, engine bob and damage jolt.
- Road wheels rotate from travelled distance and now have visible spokes.
- Track belts gained moving pad details driven by actual vehicle travel.
- Firing a real projectile now resolves the nearest firing tank and drives physical-looking barrel/muzzle recoil.
- Moving tanks create lightweight mesh-based track dust.
- No Rigidbody3D/Collider3D authority was introduced; gameplay stays on the proven Rigidbody2D/Collider2D simulation.

## Combat FX 3D
- `Projectile` now exposes presentation-only shot and impact events. Existing damage, armor, penetration, status and explosive rules remain authoritative and unchanged.
- Added mesh-based muzzle flashes aligned to the real firing direction.
- Added projectile trails for all ammo types with stronger Plasma/Explosive presentation.
- Added distinct 3D impact treatment for normal hits, explosive hits and steel ricochets.
- Added bounded spark/fracture effects and expanding shockwaves.
- Added an explicit transient FX budget to prevent late-campaign firefights from spawning unlimited visual objects.

## Damage, smoke and wrecks 3D
- Persistent `BattlefieldDestruction` impact scars now receive true 3D crater depth, raised rim debris and temporary hot centers.
- Existing wreck gameplay colliders/lifetimes are preserved while wreck visuals are upgraded to burnt hulls, displaced turrets, broken barrels and embers.
- Damaged living tanks emit mesh-based smoke/fire based on actual health ratio.
- Wrecks emit smoke and embers without changing their existing decay rules.

## Orzelek destruction
- The Eagle remains fully destructible.
- Core death still follows the existing campaign-loss path.
- v2.5 adds a dedicated 3D destruction event: multiple shockwaves, armor/gold fragments and a temporary broken Eagle wreck after the real `Health.Died` event fires.
- No permanent invulnerability or fallback protection was added.

## Architecture
- v2.5 continues the staged migration started in v2.4: proven 2D combat simulation stays authoritative while the visible battlefield is progressively replaced by 3D presentation.
- New systems are runtime-installed and remain compatible with the existing 100-round campaign, War Garage chassis, enemy classes, Eagle Fortress, bosses and artillery systems.
