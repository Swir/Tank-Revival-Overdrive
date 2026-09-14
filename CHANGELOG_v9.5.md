# v9.5.0-dev — Fire Mission Networks, Decoy Batteries & Counter-Surveillance

## Major gameplay changes
- Existing `G` SIGINT and `H` recon now provide artillery target intelligence: late siege batteries require a bounded target-lock window for reliable friendly counter-battery fire.
- Enemy siege doctrine deploys one decoy battery in mid campaign and at most two in late campaign. Unverified counter-battery cycles can hit false positions, while a valid intelligence reveal identifies and clears the decoy screen.
- A live v9.4 fire-control spotter actively shortens friendly target-lock duration and can redeploy one false emitter after reconnaissance fades, creating bounded counter-surveillance without hidden damage or invulnerability.
- Real v9.4 battery displacement now decays the active target lock, so mobile batteries must be reacquired instead of remaining permanently tracked.
- Destroying siege supply or eliminating the fire-control spotter increases the usable lock window, connecting logistics interdiction directly to counter-battery effectiveness.

## Authority and safety
- Existing `TankGame`, `Projectile`, `Health`, `CommandNetworkHuntDirector`, `SiegeLineWarfareDirector` and `SiegeLogisticsFireControlDirector` remain authoritative.
- Decoys have no damage authority, no enemy AI and no hidden attack capability.
- Maximum decoys: 2. Maximum counter-surveillance redeploys: 1 per qualifying round.
- Boss rounds remain excluded through the existing siege/logistics scheduler.
