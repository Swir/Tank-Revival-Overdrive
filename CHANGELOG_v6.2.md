# v6.2.0-dev — Convoy Warfare & Mobile Frontlines

## Major gameplay systems

- Added three deterministic mobile mission types across the 100-round campaign: **Eagle Escort**, **Enemy Interdiction**, and **Mobile Resupply**.
- Convoy vehicles use the existing authoritative `Health`/`Team` combat path, so real projectiles can destroy friendly escorts or enemy logistics.
- Friendly convoy missions feature telegraphed ambush salvos, bounded pressure cadence, route progress, critical-health states and an anti-stall route recovery policy that never grants invulnerability.
- Enemy logistics columns move toward Orzełek, repair nearby frontline enemies in bounded pulses, return screening fire, and deal real Eagle damage if they break through.
- Mobile Resupply creates a moving support opportunity: staying near the vehicle grants limited AP/EMP ammunition and field repair pulses while combat remains active.
- Eagle Escort grants a single midpoint support package when the player stays close enough to the column.
- Successful missions pay War Bonds through the existing `WarEconomyDirector`; no parallel currency was introduced.

## Readability and presentation

- Added a live Convoy Warfare HUD with route percentage, real vehicle HP, threat state, current objective status, ambush countdown and War Bond payout.
- Convoys receive distinct friendly/enemy/resupply silhouettes, cargo decks, beacons and route markers without replacing core physics or combat authority.
- Ambush warnings and logistics support pulses use existing battlefield VFX/audio helpers.

## Runtime architecture

- `ConvoyWarfareDirector` schedules missions from round 22 onward, skips boss rounds and avoids overlap with v6.1 multi-stage operations.
- Existing `TankGame`, `Health`, `Projectile`, `RuntimeBattleRegistry`, `CombatRoster` and `WarEconomyDirector` remain authoritative.
- Enemy logistics support reads live enemies from `RuntimeBattleRegistry.EnemySnapshot` instead of introducing repeated scene-wide combat scans.

## Windows qualification

- Added `ConvoyWarfareCISmokeProbe` and `Convoy Warfare Windows Gate`.
- The packaged Windows EXE is required to verify the 3-mission catalog, deterministic scheduling, route/reward/health bounds, v6.1 overlap protection, authoritative Health damage and runtime service installation.
- v6.2 remains development-only until both standard Dev Windows Build and dedicated convoy gate are green.
