# Tank Revival: Orzeł Overdrive — v7.7.0-dev

## EW Command Vehicles, Decoys & Counter-Countermeasures

This milestone completes the first full counterplay loop around the coordinated AI introduced in v7.2–v7.6.

### Enemy EW command vehicle
- From round 20 onward, one suitable live Elite/Heavy/Sniper/Siege enemy can be promoted into a visible EW command node.
- Promotion reuses the existing `EnemyTank` and `Health` authority and grants a bounded 1.40x command-vehicle endurance increase; it does not create a parallel spawn or damage system.
- While the command node is alive it hardens adaptive fire-control against player ECM/EMP, while squad and boss coordination are still interrupted, preserving counterplay value.
- Destroying the command node opens a 6-second tactical-superiority window that suspends adaptive, squad and boss coordination before the network can reacquire a new node.

### Smoke-aware counter-countermeasures
- Fast/Elite/Sniper responders recognize when the player is concealed by the v7.6 smoke system and receive bounded flank/reposition orders through existing `TacticalNavigationAgent` movement authority.
- AI no longer adds blind hidden-damage fire into smoke; pressure comes from movement and existing projectile authority.
- Maximum smoke responders and decision cadence are hard-capped for late-round stability.

### Player decoy
- `V` deploys a fixed electronic decoy for 6 seconds with a 20-second cooldown.
- The decoy temporarily suppresses the normal adaptive predictive-fire layer and redirects a maximum of three precision-capable enemy shots per bait beat toward the decoy using `TankGame.SpawnProjectile`.
- If the EW command vehicle is alive, it resolves the deception after 3 seconds; destroying command first lets the full decoy window remain effective.
- Decoy movement pressure is connected to existing tactical-navigation agents rather than teleporting or directly damaging units.

### Performance / authority
- No new Health, currency, collision or projectile authority was introduced.
- Command candidate scanning is bounded to 48 enemies.
- Smoke counter-response is capped at 4 actors.
- Decoy bait fire is capped at 3 shots per 1.15-second beat.

### Qualification
- Dedicated `EW Command Windows Gate` builds and packages Windows x64, downloads the exact artifact on a clean Windows runner, boots the real EXE and requires `-ew-command-smoke` PASS output.
- Standard `Dev Windows Build` remains the regression gate for compile/package and existing packaged-runtime tests.
