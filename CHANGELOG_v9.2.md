# Tank Revival: Orzeł Overdrive — v9.2.0-dev

## Fortification Networks, Artillery Positions & Breakthrough Operations

### Major gameplay systems
- Extends v9.1 strongpoints into a multi-node frontline fortification network.
- `F6` deploys a bounded artillery position in another friendly lane while an active strongpoint anchors the network.
- `F7` deploys a repair post in a separate friendly lane, forcing the player to hold territory instead of stacking every support asset in one location.
- Artillery uses authoritative `TankGame.SpawnProjectile` HE fire against priority Heavy/Siege/Elite threats.
- Repair posts provide a strictly capped local recovery service through the existing player `Health` authority.
- Connected fortifications provide a small bounded frontline-control hold pulse rather than global invulnerability.

### Enemy breakthrough doctrine
- Existing enemies are retasked through `TacticalNavigationAgent`; no parallel enemy roster or movement system is introduced.
- Heavy, Siege and Elite units are preferred as breakthrough actors, with ordinary units used only as fallback pressure.
- Breakthrough teams attack the weakest surviving fortification node with a hard cap of five actors and three authoritative shots per volley.
- Destroyed artillery/repair positions apply a real local frontline-control penalty.

### Stability and authority
- Boss and Supply units are excluded from breakthrough retasking.
- Auxiliary nodes are recreated safely on round changes and cleaned on run end.
- `DynamicFrontlineTerritoryDirector`, `StrongpointTerritoryWarfareDirector`, `TankGame`, `Health`, `Projectile` and `TacticalNavigationAgent` remain authoritative.
- Dedicated packaged Windows runtime smoke validates schedule, safety bounds, installation and exact v9.2 build identity.
