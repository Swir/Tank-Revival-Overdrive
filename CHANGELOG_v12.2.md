# Tank Revival: Orzeł Overdrive — v12.2 development milestone

## Route Intelligence, Ambush & Decoy Warfare

- Adds a bounded three-plan logistics route-intelligence layer on top of the v12.1 operational sustainment column. The new system changes only the existing sustainment lane intent; the v12.1 column and its Rigidbody2D remain the movement authority.
- Adds threat-aware rerouting with a hard cap of two reroutes per operation. Player pressure, enemy concentrations and fresh breach/counter-breach state influence route choice without creating a second terrain or economy model.
- Adds one optional physical enemy logistics decoy contact. It uses ordinary Health, collider and kinematic Rigidbody2D, carries no fuel/ammunition/repair manifest and grants no sustainment effect or special reward.
- Adds progressive route intelligence: enemy logistics begins as unknown, becomes a contact through proximity and is verified by close reconnaissance or destroying the false contact.
- Adds bounded route screens and ambush cells using existing Fast/Elite/Sniper combatants through TacticalNavigationAgent. No new projectile, direct-damage or EnemyTank movement authority is introduced.
- Adds a dedicated v12.2 HUD/telemetry surface for route lane, reroutes, decoy state and route-cell strength.
- Adds packaged Windows x64 source, build and runtime qualification with an exact-candidate smoke marker and blocking-exception scan.

## Safety / authority contracts

The milestone deliberately reuses `OperationalSustainmentDirector`, `CombinedArmsMobileFrontDirector`, `ReactiveCoverBreachDirector`, `Health`, `RuntimeBattleRegistry` and `TacticalNavigationAgent`. `TankGame`, `Projectile`, `Health`, `EnemyTank` and the existing sustainment Rigidbody2D retain their previous authoritative responsibilities.
