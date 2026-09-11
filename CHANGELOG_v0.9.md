# Tank Revival: Orzeł Overdrive — v0.9 BATTLEFIELD COMMAND

## Command Points
Combat now generates a dedicated Command Point resource. Points are earned from enemy kills, with higher rewards for Heavy, Siege, Elite, Supply and Boss targets, plus a small combat-uptime income and a round-start command bonus. The pool is capped at 100 to encourage active spending instead of hoarding.

## Player-controlled tactical support
Four battlefield support calls are available directly during combat:

- **Z — Airstrike (24 CP):** marks the densest enemy cluster, gives a visible warning pulse and executes a five-impact strike pass.
- **X — Artillery (16 CP):** fires a sequence of delayed precision shells at current enemy concentrations.
- **C — Supply Drop (12 CP):** repairs the player, grants campaign-scaled special ammunition and adds a shield in late sectors.
- **V — Allied Tank (30 CP):** deploys autonomous friendly armor near Orzełek. Allied units prioritize Boss, Siege and Elite threats and use AP ammunition.

## Tactical HUD
A new top-right command panel shows current CP, support costs, hotkeys and live status messages for successful/denied support requests.

## Integration and performance
The milestone is implemented as a self-contained runtime director. It scans active enemy tanks at a throttled interval and attaches lightweight bounty hooks to existing Health events, avoiding invasive rewrites of TankGame or EnemyTank. Area support damage uses existing Team and Health rules, while allied armor fires through TankGame.SpawnProjectile so it automatically follows the existing projectile, armor and effects pipeline.

## Balance goals
Battlefield Command gives the player a strategic answer to late-game swarms and special operations without trivializing the campaign. Expensive support calls compete for the same CP pool, allied armor is capped at two units, and support power scales gradually with campaign round.
