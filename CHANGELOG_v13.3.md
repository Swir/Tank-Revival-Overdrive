# Tank Revival: Orzeł Overdrive — v13.3 development changelog

## Battlefield Cohesion & Squad Command Warfare

- Added a deterministic bounded enemy squad roster with four-vehicle squads, at most six squads and at most 24 tracked actors.
- Added Leader, Wingman, Breacher and Support roles with deterministic leader promotion after casualties.
- Added Forming, Cohesive, Shocked and Regrouping squad states plus finite leader-loss shock and deterministic recovery.
- Integrated bounded formation direction, movement, reload, spread and targeting intent through the existing EnemyTank / platoon authority paths.
- Added bounded leader markers and compact squad/cohesion telemetry so command structure can be read and deliberately broken by the player.
- Added a packaged-player v13.3 smoke probe covering roster bounds, role mapping, promotion, shock/regroup behavior, formation intent and deterministic signatures.
- Hardened production authored-audio loading by indexing the real Resources folder once at startup, retaining procedural/fallback layers without weakening the two-authored-clip packaged-player gate.

## Qualification policy

v13.3 is not qualified by this changelog or version number. Qualification requires one exact Windows x64 candidate to pass the v13.3 battlefield-cohesion smoke, production-presentation authored-audio smoke, v13.2/v13.1/v13.0/v12.9/v12.8 regressions and the round 80/90/100 soak on the same packaged executable. ROADMAP items remain unchecked until that exact-SHA gate succeeds.
