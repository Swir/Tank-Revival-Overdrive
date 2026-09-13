# v6.9.0-dev — Destruction, Wreckage & Battlefield Damage Reforge

## Major systems
- Replaces overlapping tank-death wreck presentation with one v6.9 wreck authority path while leaving `Health`, `EnemyTank` and kill rewards authoritative.
- Adds seven class-aware wreck profiles covering Basic/Fast/Sniper/Heavy/Siege/Elite/Boss identities with distinct scale, debris density, lifetime and silhouette treatment.
- Adds staged wreck presentation: hot burning aftermath, lower-cadence smoldering smoke, then a cold wreck before bounded cleanup.
- Adds FULL/BALANCED/SURVIVAL wreck and debris budgets to keep late-round battlefield aftermath bounded.
- Connects existing `Obstacle.Hit` authority to staged brick/steel impact and collapse aftermath without introducing a second obstacle damage model.

## Authority / safety
- `DestructionAuthorityMigration` disables the older `WarzoneDestructionDirector` update observer once the new v6.9 authority is present and removes already-installed legacy destruction witnesses.
- `BattlefieldDirector` retains artillery gameplay but only creates its legacy wreck when the new authority is absent.
- New destruction presentation does not modify damage, HP, penetration, scoring, movement, collision or enemy AI outcomes.

## Verification
- Dedicated packaged Windows gate builds the exact development EXE and runs `-destruction-reforge-smoke` on a fresh Windows runner.
- Runtime smoke validates authority migration, wreck profile catalog, performance budgets, `Health` behavior and `Obstacle` HP behavior.
