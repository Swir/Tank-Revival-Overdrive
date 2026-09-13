# Tank Revival: Orzeł Overdrive — v7.2.0-dev

## ENEMY AI, SQUAD TACTICS & BOSS BEHAVIOR REFORGE

This milestone changes enemy pressure from mostly isolated tank decisions into bounded coordinated combat beats while preserving the existing `EnemyTank`, `Health`, `Projectile`, collision and round authority.

### Coordinated squad layer
- Adds `EnemySquadTacticsDirector` with six live roles: Vanguard, Flanker, Suppressor, Breaker, Escort and Hunter.
- Roles are assigned across existing enemy classes through `RuntimeBattleRegistry` rather than a second spawn roster.
- Coordinated actions use the authoritative `TankGame.SpawnProjectile` path; they do not create a parallel damage model.
- Flankers bias crossfire around the player, Suppressors create short bounded pressure, Breakers deliberately threaten Orzełek, Hunters prioritize the player, and Vanguard/Escort roles keep mixed formations dangerous.
- Pressure grows across rounds 1–100 with a hard six-shot coordinated-beat cap and bounded 2.8–6.5 second beat cadence.

### Boss command tactics
- Adds `BossCommandTacticsDirector` on boss rounds.
- Existing `BossLegendDirector.Phase` transitions now drive squad-level command reactions instead of affecting only the boss itself.
- Boss escorts receive readable command pulses and bounded support volleys that shift pressure from mixed player/Orzełek targeting toward aggressive player hunting in later phases.
- Boss command support is capped at five escort shots and uses the same authoritative projectile path as normal combat.

### Runtime and CI
- Adds `EnemySquadTacticsCISmokeProbe` and the `-squad-ai-smoke` packaged runtime probe.
- Adds `Squad AI Windows Gate`, which validates source configuration, builds Windows x64, packages the exact candidate, downloads it on a fresh Windows runner, boots the real EXE and rejects blocking runtime exceptions.
- Standard `Dev Windows Build` remains a second compile/package regression gate.

### Authority guarantees
- `EnemyTank` remains authoritative for its normal movement, collision and ordinary firing loop.
- `BossLegendDirector` remains authoritative for boss weak points, legend attacks and phase state.
- `Health`, `ArmorSystem`, `Projectile`, `TankGame`, `RuntimeBattleRegistry` and existing economy/campaign systems are not replaced.
- v7.2 adds bounded coordination on top of those systems rather than duplicating them.
