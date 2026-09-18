# Tank Revival: Orzeł Overdrive — v13.4 development changelog

## Tactical Terrain & Cover Warfare

v13.4 is an in-development milestone built on the qualified v13.3 battlefield-cohesion stack. It does **not** replace the existing cover/damage model: `Obstacle` remains the sole static-cover durability authority and `ReactiveCoverBreachDirector` remains the bounded breach-memory authority.

### Implemented in the first v13.4 gameplay package

- deterministic tactical-terrain plans for rounds 1–100 with seven doctrines, bounded cover counts and signatures;
- fixed candidate-slot pool and maximum twelve planner-owned cover nodes per round;
- permanent Orzełek/player safety corridor plus protected enemy spawn egress;
- canonical Brick/Steel/Water nodes using existing `Obstacle.Initialize` and existing projectile/artillery damage paths;
- bounded breach-aware cardinal maneuver hint layered after v13.3 squad cohesion rather than replacing `EnemyTank` movement authority;
- compact `TRN` telemetry with doctrine, live cover, recent breach count, safe-lane width and signature;
- packaged-player smoke probe for 100-round determinism, doctrine coverage, safety lanes, composition budgets and canonical ammo/cover counterplay.

### Qualification state

This changelog describes development scope only. v13.4 remains **IN DEVELOPMENT** until one exact Windows x64 candidate passes the dedicated v13.4 smoke, the v13.3 regression chain and late-round soak gates. Public Release readiness is separate from roadmap completion.
