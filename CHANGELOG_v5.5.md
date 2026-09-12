# v5.5.0-dev — Combat Balance, Difficulty & Telemetry

## Major systems
- Added a deterministic 1–100 enemy-health tuning curve with bounded class-specific modifiers.
- Added runtime pressure telemetry combining enemy density, player health pressure and Orzełek health pressure.
- Added a tightly bounded anti-spike Orzełek relief policy for sustained non-boss pressure only; relief is limited to one HP and protected by round cooldowns.
- Added a packaged Windows runtime balance gate that boots the exact ZIP candidate and verifies round 1/25/50/75/100 tuning samples.

## Gameplay impact
- Early campaign endurance is slightly softened while mid-campaign durability ramps more smoothly.
- Late campaign health growth tapers instead of producing an uncontrolled HP cliff, while Boss/Siege class identity remains intact.
- Boss rounds never receive emergency Orzełek relief from the v5.5 policy.
- Combat authority remains in the existing TankGame, EnemyTank, Health, Projectile and RuntimeBattleRegistry systems.

## Verification policy
- v5.5 roadmap items remain incomplete until Windows compile/package and packaged-EXE balance smoke are green.
- No unstable v5.5 code is merged to main automatically.
