# Tank Revival: Orzeł Overdrive — v0.6 Tactical Warfare

## Tactical War Director

A new runtime tactical director upgrades enemy behavior as the 100-round campaign escalates. It scans active enemies, assigns specialized tactical components by class and round, and layers new behavior on top of the proven base EnemyTank AI without replacing the stable combat core.

## Flanking Combat AI

Fast, Elite and Sniper tanks gain lateral maneuvering, evasive repositioning and simple cover awareness. The system reacts to line-of-sight and close-range danger, alternates flank direction, and avoids pushing directly into solid cover. Late-game mobile enemies now pressure the player from angles instead of only driving straight toward objectives.

## Formation Pressure

From round 55 onward, Heavy and Siege tanks can coordinate nearby allies into support volleys. The formation leader chooses the current objective and nearby tanks add synchronized fire, with larger late-game formations at the highest campaign tiers.

## Multi-phase bosses

Bosses now have four health-driven phases. Each phase changes the cadence and attack pattern:

- Phase 1: telegraphed five-shell targeted fan.
- Phase 2: radial burst pressure.
- Phase 3: alternating player/base crossfire sequences.
- Phase 4: desperation chain combining radial barrages and targeted salvos.

Phase transitions are clearly telegraphed with rings, camera feedback and a short transition window so difficulty rises without becoming unreadable.

## Tactical strike battlefield pressure

Starting at round 30, the tactical director marks delayed strike zones around the player and contested defensive space. Strikes can damage both armies, creating risk/reward positioning and making movement matter more. The Orzełek core is excluded from direct strike damage to avoid unfair campaign losses.

## Technical design

The v0.6 systems are additive runtime components. They rely on public TankGame, EnemyTank and Health APIs, minimize changes to proven core scripts, and can be individually disabled or rebalanced without rewriting the base AI.
