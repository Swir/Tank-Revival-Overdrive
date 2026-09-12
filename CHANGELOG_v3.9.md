# v3.9.0 — ENEMY TERRAIN INTELLIGENCE & DYNAMIC FRONTLINES

## Major systems

- Enemy movement now evaluates tactical terrain ahead instead of blindly accepting the previous movement vector.
- Steering is class-aware: Fast/Elite units strongly avoid mud and mines, Heavy/Siege/Boss armor tolerates rubble and craters better, Snipers favor stand-off control positions, and Siege units bias toward Orzelek when frontline pressure is favorable.
- Mine and obstacle proximity are part of route scoring, so enemy armor can reroute around known danger instead of treating every corridor equally.
- Three dynamic frontline control nodes are generated on normal campaign rounds (boss rounds remain dedicated encounters).
- Frontline control is driven by real PlayerTank, FriendlySupportUnit and EnemyTank presence in the capture radius.
- Enemy AI objectives react to node ownership and unit role; fast armor contests open nodes, heavy armor attacks player-held positions, snipers seek stand-off positions, and siege units exploit favorable pressure.
- Capturing a node for the first time per round pays War Bonds through the existing WarEconomyDirector.AwardMissionBonds path.
- A compact HUD exposes BLUE/RED node control and current frontline pressure.

## Integration / safety

- Existing EnemyTank, EnemyBattleGroupDirector, Rigidbody2D, Health, ArmorSystem, Projectile, TacticalTerrainMap and War Economy remain authoritative.
- Steering is applied by TacticalTerrainMotor after the existing EnemyTank movement pass, preserving the existing combat/AI firing systems while correcting movement through terrain.
- No boss-round objective is injected; every 10th round remains reserved for Boss Legends.
- No new currency, damage model or duplicate health system was introduced.
