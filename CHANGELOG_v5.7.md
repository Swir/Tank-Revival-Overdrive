# v5.7.0-dev — Boss Legends: Second Generation

This milestone expands boss encounters without replacing the existing BossLegendDirector, EnemyTank, Health, Projectile, ArmorSystem or weak-point authority.

## Major gameplay changes

- Added four phase-aware boss command doctrines: **Fortress Protocol**, **Predator Protocol**, **Tempest Protocol** and **Annihilator Protocol**.
- Boss doctrine selection is deterministic across the ten campaign boss tiers, creating materially different pressure profiles instead of only increasing HP.
- Added real ArmorSystem-reactive retaliation. Engine, tracks, gun and ammo-rack damage trigger different countermeasures at damaged and critical thresholds.
- Ammo-rack reactions create a short, clearly telegraphed core-vulnerability window and interrupt command pressure through the existing CombatStatus EMP path.
- Added boss doctrine/retaliation HUD telegraphs and encounter broadcasts so dangerous command patterns are readable before impact.
- Normal combat authority remains unchanged: attacks still use TankGame.SpawnProjectile, boss health remains Health-owned, weak points remain BossLegendDirector-owned and module state remains ArmorSystem-owned.

## Reliability / qualification

- Added `BossSecondGenerationCISmokeProbe` and the dedicated `Boss Generation Windows Gate`.
- The gate builds Windows x64, packages the exact build, downloads it on a fresh Windows runner and boots the packaged `TankRevivalOverdrive.exe` with `-boss-generation-smoke`.
- The runtime probe verifies all four doctrines, phase cadence/telegraph bounds, module reaction ammo mappings, safe retaliation thresholds, vulnerability-window bounds and runtime bootstrap installation.

## Safety

- v5.7 stays on `dev-v5-7` and must not be merged to `main` until standard Windows CI and the packaged boss-generation runtime gate are green.
