# Tank Revival: Orzeł Overdrive — v7.5.0-dev

## Adaptive Fire Control, Suppression & Combined Maneuver

This milestone turns the role, navigation and terrain systems introduced in v7.2–v7.4 into one coordinated combat sequence.

### Predictive fire control
- Player motion is estimated from observed TankGame position samples; no player Rigidbody or input authority is modified.
- Precision-capable enemies receive bounded lead calculation up to 0.72 seconds.
- Friendly-fire lane checks reject coordinated non-HE shots when a live enemy blocks the firing line.
- All coordinated shots still use `TankGame.SpawnProjectile`; `Projectile`, `Health`, armor and collision remain authoritative.

### Suppression doctrine
- Suppressor/Sniper actors can create short-lived telegraphed fire lanes around the predicted player path.
- Suppression has no hidden damage volume: pressure comes only from real spawned projectiles.
- Maximum two active suppression lanes and five coordinated shots per sequence beat keep late-round pressure bounded.

### Combined maneuver
- The tactical sequence cycles through Screen -> Suppress -> Flank -> Breach.
- Screen phases pull Escort/Heavy actors into protection positions.
- Suppress phases hold precision units at standoff range while fire lanes are established.
- Flank phases accelerate Flanker/Hunter movement into side lanes using observed player motion.
- Breach phases push Breaker/Siege actors toward Orzełek and allow authoritative HE pressure.

### Performance and authority
- Maximum 40 managed actors.
- Navigation overrides run at a 0.34 second cadence.
- Sequence cadence remains between 4.8 and 7.2 seconds.
- Existing RuntimeBattleRegistry, TacticalNavigationAgent, TerrainIntelligence, TankGame, Projectile and Health remain authoritative.

### Qualification
`Adaptive Fire Control Windows Gate` builds the exact Windows x64 candidate, downloads it on a fresh Windows runner and boots the real packaged EXE with `-adaptive-fire-control-smoke` before roadmap completion is allowed.
