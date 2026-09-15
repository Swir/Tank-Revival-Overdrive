# v11.2.0-dev — Fire Control, Stabilization & Ballistics Reforge

## Major gameplay changes
- Shared bounded fire-control model turns movement and real ArmorSystem weapon condition into shot stabilization and dispersion.
- AP is the precision round; HE carries wider dispersion; Plasma retains high energy but demands a steadier firing solution.
- Player fire now applies the computed solution before authoritative TankGame/Projectile spawning, so movement and component damage materially affect accuracy without replacing damage authority.
- Predictive lead helper is available for tactical AI integration against moving targets.

## Safety / authority
- Projectile, ArmorSystem, Health and TankGame remain authoritative.
- Accuracy floor is 0.58 and maximum dispersion is capped at 7.5 degrees.
- No additional damage model, HP pool or projectile simulation is introduced.
