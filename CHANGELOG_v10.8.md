# Tank Revival: Orzeł Overdrive — v10.8.0-dev

## High Command HQ, Final Objectives & Campaign Epilogues

- Added a physical, destructible Enemy High Command HQ during rounds 97–99 using authoritative `Health` and `BoxCollider2D`.
- The HQ uses existing Dynamic Frontline lane positions and survives as one persistent final objective across the pre-boss endgame window.
- ADVANTAGE maps to **HQ ASSAULT**, CONTESTED to **COMMAND ISOLATION**, and CRISIS to **EVACUATION DENIAL**.
- Objective behavior is materially different: ADVANTAGE grants bounded AP support against priority threats, CONTESTED resolves by destroying or reducing HQ to the isolation threshold, while CRISIS allows up to two HQ evacuation relocations and adds tightly capped enemy pressure.
- Round 100 preserves the existing boss as combat authority. The HQ objective resolves before the boss battle and only modifies campaign-level outcome context.
- Added campaign epilogues derived from real campaign victory/defeat, final-objective success, and v10.7 War State: Decisive Victory, Hard-Won Victory, Pyrrhic Victory, Fighting Retreat, Command Escaped, or Defeat.
- Final-objective rewards use the existing War Bonds economy and are capped at 8 bonds.
- Added packaged Windows runtime smoke verification for objective mapping, physical HQ configuration, strict support/pressure/relocation/reward caps, v10.7 integration, round-100 boss isolation, and epilogue resolution.

## Safety bounds

- Physical HQ window: rounds 97–99 only.
- Player support: at most 2 shells per round.
- Enemy finale pressure: at most 1 shell per round.
- HQ relocations: at most 2.
- Final-objective reward: at most 8 War Bonds.
- Existing `TankGame`, boss, `Health`, `Projectile`, enemy roster, frontline and War Economy remain authoritative.
