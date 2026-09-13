# Tank Revival: Orzeł Overdrive — v7.9.0-dev

## Command Network Assault & Mobile HQ Warfare

v7.9 turns the v7.8 command-network hunt into a late-round multi-stage assault instead of another isolated target marker.

### Mobile HQ operations
- Deterministic operations begin from round 36 on an eight-round cadence.
- One existing Heavy, Siege or Elite can be promoted into a visible Mobile HQ while retaining authoritative `EnemyTank` and `Health` behavior.
- HQ durability is bounded at x1.65 maximum-health promotion rather than creating a boss-only health model.
- Mobile HQ periodically relocates through the existing `TacticalNavigationAgent` and keeps a standoff position instead of sitting as a static objective.

### Escort and counterattack doctrine
- Up to five live enemies receive bounded escort positions around the HQ.
- HQ operations generate a maximum of four coordinated counterattack shots per beat through authoritative `TankGame.SpawnProjectile`.
- The operation therefore changes movement and pressure without bypassing Projectile, collision or Health authority.

### Emergency command succession
- Destroying the HQ opens a short succession delay rather than permanently deleting enemy coordination.
- A surviving Elite can become an emergency command successor with bounded x1.28 durability.
- Destroying that successor, or leaving the enemy with no eligible successor, creates an eight-second command-collapse window.
- The collapse suspends advanced adaptive/squad/boss coordination but does not disable ordinary `EnemyTank` combat.

### Integration and safety
- v7.9 reads the existing v7.8 relay network and coexists with v7.6 smoke/ECM, v7.7 tactical superiority and v7.8 network-break ownership.
- No parallel enemy roster, damage path, projectile system or movement authority was introduced.
- All late-round actor, shot, relocation and succession behavior is hard-bounded.

### Qualification target
- Standard development Windows x64 compile/package plus existing packaged regression smoke.
- Dedicated v7.9 source gate, exact candidate ZIP and real packaged Windows EXE runtime probe using `-mobile-hq-smoke`.
- ROADMAP completion remains unchecked until both Windows gates are green.
