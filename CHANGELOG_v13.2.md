# Tank Revival: Orzeł Overdrive v13.2 — Adaptive Enemy Command & Counter-Doctrine Warfare

Status: **IN DEVELOPMENT / qualification pending**

## Major gameplay systems

- Added a fixed-capacity 8-round combat-history buffer covering kill pressure, specialist/supply losses, player losses, Orzełek damage and objective outcome without unbounded campaign growth.
- Added a deterministic seven-doctrine enemy command planner: Balanced, HunterKiller, SiegePressure, FortressBreaker, Interdiction, ElasticDefense and RecoveryWindow.
- Added hysteresis, minimum hold time and repeat caps so doctrine changes are deliberate rather than per-frame or uncontrolled per-round thrashing.
- Added bounded spawn-composition intent through the existing `TankGame` spawn path only. Concurrency remains capped at ±1, spawn interval scaling at 0.92–1.08, and specialist injection uses a minimum stride of 5 while preserving Boss and Supply authority.
- Added bounded `EnemyTank` posture consumption for movement, reload, spread and player-focus intent. `EnemyTank`, `Rigidbody2D`, `Projectile` and `Health` remain the canonical movement, firing and survivability authorities.
- Added deterministic RecoveryWindow anti-snowball behavior for sustained player/Orzełek distress. It can soften future pressure but cannot heal the player, remove live enemies or grant invulnerability.
- Added adaptive-command telemetry to the existing tactical HUD/presentation stack.

## Verification and release-train hardening

- `AdaptiveEnemyCommandCISmokeProbe` exercises synthetic offensive, supply-interdiction and distress histories, fixed history capacity, all 100 round plans, doctrine determinism, hard budget bounds, specialist directives, Boss/Supply preservation and posture bounds.
- Added deterministic exact-candidate packaging with EXE/Data uniqueness checks, immutable source runtime, normalized ZIP metadata, SHA-256 and provenance.
- Added packager fixture tests for a valid player, missing `_Data`, duplicate players, output-path overlap, stale version rejection and deterministic ZIP hashing.
- Added `Adaptive Enemy Command v13.2 Windows Qualification Gate`: source/authority/progress contracts, one Unity 6000.3.17f1 Windows x64 build, then v13.2/v13.1/v13.0/v12.9/v12.8 packaged-EXE smoke plus the rounds 80/90/100 soak on that same binary.
- Added an exact-SHA roadmap finalizer that can close v13.2 only after a successful v13.2 Windows gate for the same commit.
- Integrated SWIR Progress SVG Pro v1 with deterministic roadmap-derived card/mini/template assets and CI verification while keeping release readiness separate from roadmap completion.

## Qualification policy

No v13.2 roadmap checkbox is completed merely because code exists. The milestone remains **399/407 (98.0%) — V13.2 IN DEVELOPMENT** until one exact Windows candidate passes the full gate and the exact-SHA finalizer updates the protected roadmap dashboard.
