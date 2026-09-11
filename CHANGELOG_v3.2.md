# v3.2.0 — WEAPON FAMILIES & COMBAT MASTERY

## Major systems

- Added four persistent primary weapon families that replace only the unlimited Basic shell path:
  - **Vulcan Autocannon** — burst-oriented high rate of fire, escort rounds and Assault synergy.
  - **Siege Heavy Cannon** — slower HE breakthrough fire, high direct damage and Bastion synergy.
  - **Lancer Railgun** — extreme velocity AP penetration with Hunter/Breach Core synergy.
  - **Arc Plasma Repeater** — multi-penetration plasma pressure with Scout synergy and reactor heat tradeoffs.
- Added persistent per-family **Combat Mastery** (levels 0–5) driven by real resolved projectile damage and kills.
- Mastery rewards progressively alter cadence, damage, burst behavior and family-specific projectile patterns instead of granting cosmetic badges only.
- Added chassis/loadout synergies so v3.0 Arsenal and v3.1 Garage choices now meaningfully interact with the selected primary weapon family.
- Added replacement-fire routing that preserves special ammo inventory and continues to use `TankGame.SpawnProjectile`, `Projectile`, `Health`, `ArmorSystem`, ricochets, penetration and existing 3D combat FX.
- Added new `Projectile.DamageResolved` telemetry so progression is awarded from authoritative damage resolution, including explosion splash kills.
- Added family HUD/garage selector (`F12`, `Shift+F12`) with mastery progress and combat role.
- Added procedural 3D weapon-family hardware so Autocannon, Heavy Cannon, Railgun and Plasma Repeater are visually readable on the tank.

## Compatibility / safety

- Existing special ammo keys 1–7 remain unchanged.
- Existing Orzełek destruction and campaign-loss logic are untouched.
- Existing Rigidbody2D/Collider2D simulation remains authoritative.
- No unstable changes are merged to `main`; this milestone remains on `dev-v3-2` pending Windows CI and runtime playtest.
