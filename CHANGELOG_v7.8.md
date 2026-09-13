# v7.8.0-dev — SIGINT, Recon Drones & Command Network Hunt

## Major gameplay systems

- Expanded the v7.7 EW command concept into a bounded relay network using existing live EnemyTank/Health actors.
- Up to three support-capable enemies can become relay nodes from round 28 onward; relays gain a modest durability bump but remain ordinary authoritative combat targets.
- Added player SIGINT scan on `G`: a cooldown-limited intelligence burst reveals the EW command vehicle and relay nodes for a temporary window rather than permanent omniscient markers.
- Added recon-drone sweep on `H`: a bounded eight-second reconnaissance window repeatedly reacquires and marks command-network targets without dealing hidden damage.
- Relay destruction causes short tactical-network disruption; eliminating the final relay while command remains online opens a six-second full network-break window that suppresses advanced adaptive/squad/boss coordination.
- Relay reacquisition is delayed after losses so destroying the network creates a real playable advantage instead of an instantly refilled target list.

## Authority and performance

- Relay nodes reuse existing EnemyTank movement, Health death events and RuntimeBattleRegistry snapshots.
- No parallel HP, projectile or damage model was added.
- Candidate scans are capped at 48 enemies, relay population at three nodes, recon sweep cadence at 0.75 s and all tactical suppression windows are bounded.
- v7.6/v7.7 smoke, ECM, EMP, decoy and command-vehicle systems remain authoritative and are respected when v7.8 restores tactical directors.

## Qualification

- Dedicated `Command Network Hunt Windows Gate` validates source bounds, builds the Windows x64 player, packages the exact candidate and boots the real EXE on a clean Windows runner with `-command-network-hunt-smoke`.
- Standard Dev Windows Build remains required before roadmap closure.
