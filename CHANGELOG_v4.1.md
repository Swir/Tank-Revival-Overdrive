# v4.1.0 — STRATEGIC WAR MAP & SECTOR CONSEQUENCES

## Major systems

### Strategic War Map
- New full-screen ten-sector campaign map opened with `M`.
- Displays active front, historical sector score/grade, selected v4.0 route, best eliminations, campaign completion and checkpoint position.
- Adds persistent Strategic Momentum (0–100) representing campaign initiative.

### Persistent sector outcomes
- Every completed sector receives a 1–5 grade from actual combat performance and surviving player/Orzelek health.
- Dominant/decisive sectors create next-sector logistics advantages through the existing War Bonds, ammo, Health, ArmorSystem and Orzelek repair systems.
- Contested/critical sectors can create an enemy counteroffensive: newly spawned hostile armor receives bounded extra Health, with heavier reinforcement on Heavy/Siege/Elite/Boss units.
- No parallel damage, currency or enemy-spawn model was introduced.

### Operational checkpoints
- Every sector entry creates a persistent campaign checkpoint snapshot containing sector, round, momentum and War Bonds state.
- Checkpoint resupply strength is driven by the previous sector grade plus strategic momentum.
- High momentum improves AP/Explosive/EMP/Plasma supply, hull service, armor-module repair and Orzelek sustain.

## Integration
- Builds directly on v4.0 `OverdriveSectorCampaignDirector` PlayerPrefs and existing `TankGame` round flow.
- Uses authoritative `CombatRoster`, `Health.SetMaximum`, `PlayerTank.AddAmmo`, `ArmorSystem.RepairModules`, `TankGame.RepairEagle` and `WarEconomyDirector.AwardMissionBonds` paths.
- Boss Legends, Zero Front, Tactical Terrain, Dynamic Frontlines, Friendly Support and Command Center remain intact.

## Safety
- Development-only milestone on `dev-v4-1`.
- Do not merge to stable `main` until Windows x64 CI is green and runtime playtest validates sector grading, reinforcement pressure and map readability.
