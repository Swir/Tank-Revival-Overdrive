# v5.9.0-dev — Command Network & Fortress Siege Counterplay

## Major gameplay systems
- Added an event-driven Enemy Command Network that assembles live enemies into coordinated command cells without replacing `EnemyTank` movement/fire authority.
- Added three tactical roles: Suppressor, Escort and Fortress Breaker.
- Fortress Breaker operations are explicitly telegraphed and can be defeated by destroying the breaker before impact.
- Active AEGIS protection from the v5.8 fortress system now has direct enemy counterplay value: a live shield intercepts and disrupts the coordinated siege strike.
- Suppressors create bounded player pressure while the breaker prepares its strike; escorts receive only a small one-time durability bump rather than uncontrolled scaling.
- Added a command-threat HUD showing operation name, role composition, strike countdown and counterplay instruction.

## Balance / safety
- Coordinated operations do not begin before round 18.
- Command cells are capped at four assigned units.
- Siege volley is capped at three explosive shells.
- Telegraph, cooldown, suppression cadence and unit caps are exposed to automated verification.
- Bosses and Supply units are excluded from command-cell reassignment.

## Quality gate
- Added `EnemyCommandNetworkCISmokeProbe`.
- Added `Command Network Windows Gate`, which compiles Windows x64, packages the exact candidate, boots that exact EXE on a fresh Windows runner and verifies role catalog, bounded configuration, AEGIS interception semantics and runtime installation.

## Integration
- Uses `RuntimeBattleRegistry.EnemySnapshot`, `TankGame.SpawnProjectile`, `Health`, `CombatRoster.Eagle`, existing ammunition types, presentation helpers and the v5.8 shield state.
- No new currency, parallel health model or alternate projectile authority was introduced.
