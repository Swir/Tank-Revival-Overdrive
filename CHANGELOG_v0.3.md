# Tank Revival: Orzeł Overdrive — v0.3 development milestone

## Command Center

Every five secured rounds the next battlefield deployment pauses at the Field Command Center. War Bonds earned from kills and round clears can be invested in persistent campaign upgrades before resuming combat.

Upgrades:
- Cannon Calibration — higher effective shell damage and muzzle velocity.
- Autoloader — faster sustained fire.
- Engine Tuning — higher effective movement speed.
- Composite Armor — increased maximum player armor.
- Eagle Sentry Network — deploys up to three autonomous defense cannons around Orzełek.
- Logistics & Salvage — grants special ammunition each round and increases War Bond recovery.
- Field Repair — spend War Bonds to restore Orzełek during Command Center visits.

## Tactical wave doctrines

Enemy pressure is no longer described only by the round number. The tactical director selects named doctrines such as Recon Patrol, Blitz Wave, Siege Column, Marksmen, Heavy Column, Supply Raid, Elite Hunters, Crossfire, Iron Storm and Boss Protocol.

Doctrines alter reinforcement armor, coordinated extra fire, target preference, projectile velocity and salvage rewards. Higher campaign sectors unlock more dangerous doctrines.

## Eagle sentry network

A new autonomous defense system protects the stronghold when purchased. Sentries acquire nearby targets, prioritize Siege/Elite/Boss threats, rotate their guns in real time and fire player-team projectiles. Higher levels increase rate of fire, range and penetration.

## Player combat integration

Commander upgrades are separate from temporary/field power-ups, so permanent campaign improvements stack safely with cannon, rapid-fire and engine pickups without corrupting the saved loadout between rounds.

## Technical

- Added dynamic Health maximum-capacity support.
- Added doctrine augments as runtime components instead of hard-coding every modifier into EnemyTank.
- Added automatic enemy event hooking for War Bond salvage.
- Added a dedicated dev-v0.3 Windows CI branch gate.
