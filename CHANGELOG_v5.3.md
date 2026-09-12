# Tank Revival: Orzeł Overdrive — v5.3.0-dev

## Expanded Campaign, Boss Contracts & Challenge Modes

### Sector Operations
- Added four replayable sector operation packages: **Crossfire Grid**, **Siege Lock**, **EMP Storm** and **Attrition Front**.
- Every 10-round sector receives a run-seeded operation package layered on top of the existing campaign variant/route systems.
- Operation agents attach only to eligible enemies that already exist in the authoritative `TankGame` / `RuntimeBattleRegistry` flow.
- Operations add bounded class-specific armor pressure and AP / explosive / EMP / incendiary support fire without creating a parallel spawn or damage model.

### Optional Challenge Contracts
- Added four optional challenge types: **Eagle Guard**, **Blitz Clock**, **Armor Quarry** and **Specialist Purge**.
- Contracts appear on eligible fifth rounds outside boss rounds and track actual Eagle damage, round time or real enemy-class kills.
- Successful contracts award War Bonds through the existing `WarEconomyDirector.AwardMissionBonds` path.
- Missing a contract never blocks campaign progression.

### Boss Overdrive Contracts
- Every boss round receives one of three run-seeded modifiers: **Iron Oath**, **Storm Cage** or **Last Stand Protocol**.
- Boss contracts increase endurance and add distinct AP crossfire, EMP cage or incendiary last-stand pressure.
- Existing `BossLegendDirector` phases, weak points, `Health`, `Projectile` and boss weapon authority remain intact.

### Runtime Qualification
- Added `CampaignExpansionCISmokeProbe` behind `-campaign-expansion-smoke`.
- Dev Windows CI now boots the exact packaged EXE on a fresh Windows runner, verifies the v5.3 catalog, instantiates a real boss actor and confirms the boss-contract health mutation.
- Runtime gate rejects crash, null-reference, missing-method, type-load and index-range exception signatures.

## Safety / integration constraints
- No unstable build is merged to `main`.
- No duplicate health, projectile, War Bond, round or boss-phase authority was introduced.
- v5.3 roadmap deliverables stay unchecked until compile/package and packaged runtime verification are green.
