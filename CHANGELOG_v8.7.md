# Tank Revival: Orzeł Overdrive — v8.7.0-dev

## Supply Routes, Escort Doctrine & Counter-Interdiction Warfare

v8.7 turns the physical logistics targets introduced in v8.6 into defended battlefield operations instead of passive bonus objects.

### Major gameplay systems
- Enemy logistics nodes now receive a bounded escort screen made from real existing enemy tanks. Heavy and Elite units are preferred, with Fast and standard units filling gaps. No parallel spawn authority is introduced.
- Escort orders use the existing `TacticalNavigationAgent` and `SquadTacticalRole.Escort`, so logistics defense shares the same obstacle avoidance, separation and movement authority as the rest of the tactical AI.
- Mobile supply convoys react to sustained damage with limited emergency reroutes. A reroute reverses travel direction and shifts the route vertically inside safe battlefield bounds instead of teleporting the target.
- Nearby living escorts can perform bounded field repairs on damaged logistics nodes. Repairs use authoritative `Health.Heal`, have a fixed cadence and a strict per-node cap.
- Destroyed logistics yield a small captured-supplies reward through existing systems: War Bonds plus a limited player or Orzełek repair depending on the node type.
- Surviving logistics remain connected to v8.5/v8.6 reserve replenishment, so escorting and repairing them materially improves enemy strategic recovery.

### Balance / authority rules
- Maximum escort screen: 4 units.
- Repairs: +1 HP, maximum 4 repairs per node, only with a live escort inside repair range.
- Convoy reroutes: maximum 2 per convoy, damage-threshold and cooldown gated.
- Captured supplies: +2 War Bonds and at most +1 HP to the relevant existing health target.
- Bosses and supply-class enemies are never repurposed as logistics escorts.
- No hidden damage, invulnerability, duplicate health model, duplicate projectile model or parallel currency was added.

### Qualification
- Dedicated packaged-Windows gate: `supply-routes-windows.yml`.
- The exact Windows x64 development EXE is booted on a fresh Windows runner with `-supply-routes-smoke`.
- Runtime validation checks escort scheduling/progression, reroute restrictions, repair caps/range, captured-supply mapping, runtime installation and v8.7 version identity.
