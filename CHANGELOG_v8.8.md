# v8.8.0-dev — Battlefield Salvage, Field Resupply & Logistics Counteroffensive

## Major gameplay milestone

v8.8 turns destroyed high-value armor and logistics into contested battlefield resources rather than one-shot rewards.

### Battlefield salvage
- Real Heavy, Sniper, Siege and Elite deaths can create bounded salvage caches, capped per round and globally to protect late-round performance.
- Destroyed v8.6/v8.7 logistics nodes create higher-value logistics-grade salvage through the existing Health death path.
- Salvage has a short lifetime and clear world-space presentation instead of becoming permanent battlefield clutter.

### FIELD vs STRATEGIC recovery
- When close to salvage, the player chooses `Z` FIELD recovery or `X` STRATEGIC extraction.
- FIELD recovery converts captured material into immediate combat help through existing PlayerTank ammunition and Health paths, with Fire Support/logistics caches also able to repair Orzełek by a bounded amount.
- STRATEGIC extraction trades immediate combat help for a larger War Bond payout and permanently denies that cache to the enemy.
- Reserve type determines recovered ammunition: Armor → AP, Fire Support → HE, Electronic Warfare → EMP.

### Enemy logistics counteroffensive
- Unsecured salvage is not free: surviving real enemies are dynamically retasked through TacticalNavigationAgent to recover it.
- Recovery pressure scales from one to three units across the campaign and never creates a parallel enemy roster.
- If an enemy recovery team reaches the cache first, the matching v8.5 strategic reserve regains one point through the existing logistics reserve bridge.
- Bosses and supply tanks are excluded from recovery duty; ordinary combat, Health, Projectile and TankGame authority remain unchanged.

### Qualification
- Dedicated packaged Windows gate builds the exact v8.8 Windows x64 candidate and boots the same artifact on a fresh Windows runner with `-battlefield-salvage-smoke`.
- Standard Dev Windows Build remains required for compile/package and established packaged runtime regressions.
- `ROADMAP.md` scope was registered before implementation at 174/178 = 97.8%; v8.8 deliverables remain unchecked until both Windows gates pass.

No unstable v8.8 code is promoted to `main` by this milestone.
