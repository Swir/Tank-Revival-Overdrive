# v6.3.0-dev — COMBINED ARMS & REINFORCEMENT WARFARE

## Major gameplay systems

- Added deterministic reinforcement-command operations beginning in the mid campaign, while excluding boss rounds and v6.1 multi-stage operations.
- Added a destructible enemy reinforcement command post using the existing `Health` authority. Destroying the node stops future relief waves, awards War Bonds and grants a bounded battlefield-support charge.
- Added three response doctrines: **Escort Screen**, **Hunter-Killer Team** and **Siege Relief Group**. Their compositions reuse existing `EnemyTank` classes and the authoritative `TankGame` enemy spawn path rather than introducing a parallel combat roster.
- Added bounded reinforcement pressure: 2–3 waves, 2–4 units per wave, live-enemy saturation protection and campaign-scaled deployment cadence.
- Added player combined-arms support: **F9 Artillery** and **F10 CAS**. Both consume earned support charges, telegraph marked targets, and fire through the existing `TankGame.SpawnProjectile` / `Projectile` damage authority.
- Added a dedicated Combined Arms HUD showing doctrine, command-post HP, deployed-wave count, support readiness and tactical status.

## Progression and integration

- Command-post destruction pays mission rewards through the existing `WarEconomyDirector.AwardMissionBonds` path.
- Reinforcement operations deliberately overlap selected convoy rounds, creating escort/interdiction pressure without replacing `ConvoyWarfareDirector` or `TankGame` round ownership.
- Reinforcement units remain normal `EnemyTank` actors with existing AI, `Health`, armor, projectile and runtime-registry behavior.

## Qualification

- Added `CombinedArmsCISmokeProbe` with `-combined-arms-smoke` runtime mode.
- Added a dedicated packaged Windows x64 gate that compiles the candidate, downloads the exact artifact on a fresh Windows runner, boots the EXE and rejects blocking runtime exception signatures.
- Smoke validation covers deterministic scheduling, doctrine rotation, health/reward/wave bounds, composition restrictions, TankGame spawn-bridge availability and authoritative `Health` damage behavior.

## Safety

- Development milestone only. Do not publish to `main` until standard Windows CI and the dedicated Combined Arms Windows Gate are green.
