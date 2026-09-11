# v1.2.0 — WAR GARAGE

## Major systems
- Added a persistent pre-campaign War Garage.
- Added four functional player chassis: Orzel Mk-I Assault, Bastion Heavy, Wicher Scout and Hunter TD.
- Chassis unlock from persistent campaign achievements rather than temporary round state.
- Added Garage Marks economy derived from completed contracts, defeated Boss Legends and best reached sector.
- Added a permanent four-branch upgrade tree: Cannon, Autoloader, Engine and Armor.
- Garage choices are saved with PlayerPrefs and automatically re-applied whenever the player tank respawns.

## Gameplay impact
- Assault starts with cannon/loader specialization and AP support.
- Bastion trades mobility focus for much higher survivability and explosive ammunition.
- Scout favors speed/reload and receives EMP ammunition.
- Hunter is a tank-destroyer profile with maximum cannon bias and extra AP ammunition.
- Permanent upgrades stack with chassis identity through the existing commander upgrade system.
- Special ammunition is granted once per round, preventing death/respawn farming.

## Integration
- Added GarageProgressBridge to migrate the v1.1 Boss Legends progress key into the garage economy without invalidating old saves.
- Existing movement, armor, ammo, Overdrive, Battlefield Command and boss systems remain intact.
- War Garage is intentionally isolated from main until Windows x64 CI is green.
