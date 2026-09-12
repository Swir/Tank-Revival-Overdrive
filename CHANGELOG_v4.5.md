# v4.5.0 — PERFORMANCE & 100-ROUND WARFARE HARDENING

## Scope

This milestone focuses on late-campaign stability and production hardening rather than adding another isolated gameplay layer.

### Adaptive warfare performance governor
- Adds a shared runtime performance governor driven by rolling frame time, FPS and live enemy pressure.
- Introduces three presentation budgets: `FULL`, `BALANCED` and `SURVIVAL`.
- Downshifts quickly under load and only recovers after sustained healthy frame pacing to avoid quality thrashing.
- Exposes shared budgets for roster refresh cadence, track-mark density and battle-wear spark pressure.
- Adds an F3 telemetry panel showing FPS, frame time, active budget, living enemies, health-unit count, roster cadence and track-mark cap.

### Centralized runtime discovery
- `CombatRoster` remains the single authoritative scene cache for enemy/health discovery.
- Its expensive scene refresh cadence now adapts from 0.30 s up to 0.62 s under heavy load.
- Adds a `CombatRoster.Refreshed` event so dependent presentation systems can consume the cache instead of running their own `FindObjectsByType` scans.

### Battle-wear hardening
- `VehicleBattleWearDirector` no longer performs a second full `Health` scan every 0.55 s.
- It binds player/enemy/friendly vehicles directly from `CombatRoster.HealthUnits`.
- Track marks now use a reuse pool instead of continuously allocating and destroying every imprint.
- Track density, spacing and interval scale with the active performance budget.
- Ambient damage sparks are reduced or disabled under pressure while direct-hit sparks remain possible.
- Track-mark caps fall from 180 to 108/64 in Balanced/Survival modes to protect rounds 70–100.

## Gameplay integrity

No combat authority was replaced. `Projectile`, `Health`, `ArmorSystem`, `EnemyTank`, `TankGame`, `CombatRoster`, campaign directors and economy remain authoritative. v4.5 only changes discovery cadence and presentation cost under load.

## Validation target

Windows x64 CI must compile, verify the executable, package the build and upload the development artifact before this milestone is considered ready for review. `main` must remain untouched until that validation is green.
