# v6.7.0-dev — Vehicle Motion & Weapon Animation Reforge

## Major gameplay presentation upgrade

- Added a presentation-only vehicle motion rig driven by observed tank movement.
- Added bounded hull lean, suspension travel and animated road-wheel motion without touching Rigidbody2D, colliders or AI authority.
- Added class-aware motion language: Fast vehicles feel lighter and more reactive, while Heavy, Siege and Boss vehicles move with progressively greater visual mass.
- Added authoritative-shot observation through existing Projectile instances.
- Added cannon recoil and muzzle flash timing for player and enemy vehicles without altering fire cadence, projectile speed, damage or ammunition logic.
- Added ammo-aware recoil emphasis for AP, HE and Plasma-class shots.
- Added strict presentation bounds and projectile-observation budgets for late-round stability.
- Added a packaged Windows runtime smoke gate verifying v6.7 installation, comfort/recoil bounds and unchanged Health authority.
