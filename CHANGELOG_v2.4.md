# v2.4.0 — 3D OVERDRIVE FOUNDATION

This milestone begins the controlled migration of **Tank Revival: Orzeł Overdrive** from the established 2.5D presentation into a real 3D battlefield without discarding the proven 100-round campaign, AI, armor, siege, garage, progression or Orzełek defense rules.

## Hybrid 3D architecture

- Existing `Rigidbody2D`, `Collider2D`, projectile, armor and AI simulation remains authoritative for gameplay.
- New runtime 3D meshes contain no gameplay colliders, preventing double-physics regressions during migration.
- The presentation layer can therefore evolve independently while v2.x gameplay remains testable.
- A reusable `Runtime3DFactory` creates lit runtime meshes and cached materials without requiring external asset packs.

## Perspective battlefield

- Added an oblique perspective camera with preserved combat camera shake.
- Mouse aiming now raycasts onto the real gameplay plane, so turret control remains accurate with a perspective camera.
- Added directional lighting, ambient lighting and soft shadows.
- The ten campaign sectors retain their existing palettes but now render as a real 3D battlefield slab with depth, rims, procedural craters, debris and sector pylons.
- Legacy ground sprites are hidden only after their 3D replacement is active.

## Procedural 3D armored vehicles

- Player and all enemy classes now receive runtime 3D tanks built from hull, upper armor, glacis, engine deck, tracks, road wheels, turret ring, turret, hatch, mantlet, barrel and muzzle brake pieces.
- Heavy, Siege and Boss units receive additional side armor and stronger silhouettes.
- Sniper/Siege/Boss classes receive longer guns.
- Bosses receive a unique command crown.
- War Garage chassis now have distinct 3D proportions:
  - Orzeł Mk-I Assault — balanced profile.
  - Bastion Heavy — wider/heavier body and turret.
  - Wicher Scout — lighter, narrower silhouette.
  - Hunter TD — long-gun tank destroyer silhouette.
- Independent turret aiming continues to use the existing `TankTurretRig` aim solution.
- Damaged tanks gain a pulsing 3D warning beacon below 50% health.

## Projectiles and battlefield objects

- All ammunition receives a 3D shell/energy presentation while retaining the original projectile collision and status-effect logic.
- Brick, steel and water obstacles now have real depth and material differences.
- Ammo crates and power-ups receive animated 3D presentations while retaining their original 2D collection triggers.

## Orzełek and Eagle Fortress in 3D

- `ORZELEK_DEFENSE_CORE` now receives a 3D armored plinth, core and golden Eagle structure.
- Critical core health produces a pulsing 3D warning element and visible stress animation.
- Eagle Fortress armor modules, sentry turrets and repair relay receive dedicated 3D structures.
- Sentry heads visually track enemies using the shared `CombatRoster` cache.
- **Orzełek remains fully destructible.** The existing `Health` component and `OnBaseDestroyed -> LoseCampaign("ORZEŁEK DESTROYED")` path remain authoritative.

## Migration safety

This release intentionally does **not** replace the mature physics/AI stack with experimental 3D physics yet. The goal is a playable 3D vertical slice first. True 3D vehicle motion, projectile height/ballistics, richer 3D VFX and more advanced terrain interaction can now be added in later milestones on top of a stable compatibility layer.
