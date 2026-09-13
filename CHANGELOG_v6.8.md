# v6.8.0-dev — Weapon Impacts, Explosion & Combat VFX Reforge

## Major gameplay presentation changes
- Added ammo-aware impact signatures for all seven ammunition families while preserving Projectile/Health authority.
- Added layered flash, shockwave, spark, smoke/debris emphasis and destruction escalation for high-significance impacts.
- Added budgeted high-value projectile trail presentation with FULL/BALANCED/SURVIVAL cadence scaling.
- Added strict runtime caps for tracked projectile trails and layered FX bursts to protect late-round performance.

## Qualification
- Dedicated Windows packaged-runtime gate: `.github/workflows/combat-vfx-windows.yml`.
- Runtime smoke verifies service installation, complete ammo signature catalog, FX budgets, Projectile event authority and unchanged Health damage path.

## Authority guarantees
This milestone does not change projectile damage, penetration, status effects, fire rate, movement, collisions, armor resolution or Health authority. All additions are presentation-only observers of existing authoritative events.
