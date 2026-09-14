# v9.1.0-dev — Strongpoints, Field Fortifications & Territory Counteroffensives

## Major gameplay milestone

v9.1 turns the three-lane frontline introduced in v9.0 into territory that can be physically fortified, supplied and assaulted.

### Frontline strongpoints
- From round 15 onward, non-boss rounds allow the player to build one physical strongpoint in the nearest friendly-controlled lane with `F4`.
- Construction spends the existing War Bonds economy instead of introducing another resource.
- Strongpoints use authoritative `Health` and collision, have bounded round-scaled durability, and visibly reinforce the selected lane rather than applying a global passive buff.
- `F5` can reinforce the active position once per round, increasing maximum durability, restoring health and strengthening local frontline control.

### Local battlefield support
- A surviving strongpoint fires bounded real projectiles through `TankGame.SpawnProjectile` at nearby enemies.
- Up to two local field-repair pulses can restore 1 HP to a nearby damaged player tank during the round.
- Holding the position periodically adds a small amount of control only to its own lane; losing it removes control and creates an actual enemy breakthrough consequence.

### Enemy territory counteroffensives
- Existing surviving enemies are retasked through `TacticalNavigationAgent`; no parallel enemy roster or movement system is introduced.
- Counterattack size grows from two attackers in early eligible rounds to a hard maximum of four in the late campaign.
- Breaker/Suppressor roles converge on the strongpoint and fire bounded authoritative volleys through the existing projectile path.
- Boss and Supply units are excluded from strongpoint assault retasking.

### Stability and integration
- Strongpoints are recreated per round and never persist as stale scene objects.
- Boss rounds remain excluded from fortification deployment.
- Existing `TankGame`, `Health`, `Projectile`, `DynamicFrontlineTerritoryDirector`, `WarEconomyDirector` and `TacticalNavigationAgent` remain authoritative.
- Dedicated packaged Windows runtime qualification validates schedule, costs, durability, counterattack budgets, control effects and v9.1 identity.
