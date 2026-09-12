# Tank Revival: Orzeł Overdrive v4.6.0-dev

## RUNTIME ARCHITECTURE & MASS-BATTLE OPTIMIZATION

This milestone hardens the 100-round campaign runtime for the heaviest late-game battles without changing combat authority, damage rules, AI decisions, economy or campaign progression.

### Event-driven battle registry
- Added `RuntimeBattleRegistry` as the shared lifecycle registry for `Health`, `EnemyTank`, `PlayerTank` and Orzełek references.
- `Health.Initialize` now registers units and `Health.OnDestroy` unregisters them automatically.
- Enemy/player roles are derived from the same authoritative GameObjects, so no duplicate gameplay state is introduced.
- Registry snapshots are rebuilt only when combat membership changes.

### CombatRoster scan elimination
- `CombatRoster` now consumes registry snapshots instead of running repeated `FindObjectsByType` scene scans every few tenths of a second.
- One safety reconciliation is kept at startup for scene-authored/legacy units.
- Spawn/despawn changes publish through the registry and update cached enemy-class counts through the existing `CombatRoster.Refreshed` event.
- Existing directors consuming `CombatRoster.Enemies`, `HealthUnits`, `Player`, `Eagle`, `Count(kind)` and `LivingEnemyCount` remain compatible.

### Mass-battle FX budget
- Added `MassBattleFxBudget` with per-frame budgets for projectile afterglow and transient impact bursts.
- FULL / BALANCED / SURVIVAL performance tiers now reduce optional visual density before gameplay systems are affected.
- Player plasma, boss weak-point hits, critical hits and kill feedback remain priority effects.
- Non-critical trails and micro FX are throttled first during high-load fights.
- Special-ammo trail cadence itself scales with the active performance tier, reducing GameObject churn in late rounds.

### Performance governor 2.0
- `WarfarePerformanceGovernor` now accounts for registered combat-unit pressure in addition to measured FPS/frame time and enemy count.
- Added a shared `BudgetChanged` event and `FxDensityMultiplier` for future directors.
- F3 telemetry now exposes registry size/revision, active roster count, track-mark cap and accepted/rejected transient FX requests.

### Gameplay safety
- `Projectile`, `Health`, `ArmorSystem`, `TankGame`, enemy AI, War Economy and campaign directors remain authoritative.
- No damage, spawn, currency, objective or progression rules were replaced.
- v4.6 is intentionally kept off `main` until Windows x64 CI is green and late-round runtime testing confirms stability.
