# Tank Revival: Orzeł Overdrive — v7.4.0-dev

## TERRAIN INTELLIGENCE, COVER & BREACH WARFARE

v7.4 turns the v7.2/v7.3 squad AI into terrain-aware battlefield behavior. The milestone deliberately reuses existing movement, projectile, obstacle, health and round authority rather than introducing a parallel combat simulation.

### Terrain intelligence
- Adds `TerrainIntelligenceDirector` with a bounded obstacle cache refreshed on a fixed cadence.
- Scores existing Brick and Steel obstacles as tactical cover candidates.
- Refines v7.3 formation orders without replacing `TacticalNavigationAgent`, `Rigidbody2D` or `EnemyTank` authority.
- Keeps hard limits on cached obstacles, scan cadence and breach actors for late-round stability.

### Cover doctrine
- Sniper/Suppressor roles seek protected standoff anchors behind nearby Brick/Steel cover relative to the player threat vector.
- Heavy/Escort roles screen nearby Sniper/Suppressor allies by positioning between the protected unit and the player.
- If no vulnerable ally is available, Heavy/Escort units may fall back to nearby cover anchors.

### Breach warfare
- Breaker/Siege actors evaluate the approach lane toward Orzełek for destructible Brick obstacles.
- Blocking Brick can become the current tactical navigation objective instead of causing repeated blind anti-stall detours.
- Breach fire uses the existing `TankGame.SpawnProjectile` path with enemy HE ammunition; `Projectile`/`Obstacle.Hit` remain authoritative for actual collision and damage.
- Steel and Water are never directly mutated by the new system.

### Qualification
- Adds `TerrainIntelligenceCISmokeProbe` and a dedicated Windows x64 packaged runtime gate.
- The source gate validates version identity, cover/breach bounds, HE projectile integration and v7.3 navigation wiring.
- The runtime gate boots the exact packaged EXE on a fresh Windows runner and rejects blocking runtime exception signatures.

### Compatibility
- Existing v7.2 squad-role assignment remains authoritative.
- Existing v7.3 tactical navigation remains the movement layer.
- Existing Health, Projectile, Obstacle and TankGame systems remain the only combat/damage authorities.
