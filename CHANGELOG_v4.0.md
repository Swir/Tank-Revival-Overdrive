# v4.0.0 — OVERDRIVE CAMPAIGN REFORGE

## Major systems

### Strategic 10-sector campaign
- Reframes the 100-round run into ten named sectors, one per 10-round block.
- Each sector now has its own operational identity and persistent completion record.
- Sector transitions award War Bonds through the existing War Economy rather than adding a new currency.
- Sector performance tracks real enemy kills, heavy targets and special targets.

### Route decisions
At the start of each sector the player chooses one of three routes:
- **Spearhead** — offensive AP/Explosive/Plasma sustain and kill-chain ordnance.
- **Bulwark** — player sustain, Orzelek repairs, armor servicing and boss-entry protection.
- **Recon** — EMP reserves, intelligence payouts and bonus War Bonds for priority targets.

Selections persist per sector and use existing PlayerTank, Health, ArmorSystem, AmmoType, CombatRoster and WarEconomyDirector systems.

### Zero Front finale
Rounds 91-100 now form a four-stage endgame operation layered on top of the existing boss, terrain, frontline, fortress and encounter systems:
1. Breach the Outer Ring
2. Fortress Interdiction
3. Overdrive Assault
4. Destroy Overdrive Zero

The finale adds phase resupplies, combat-charge support, elite-target rewards, emergency field recovery and a persistent completion flag. It does not replace the existing final boss logic.

## Integration rules
- Existing TankGame spawning and round flow remain authoritative.
- Existing Boss Legends remain authoritative on boss rounds.
- Existing Dynamic Frontline, Tactical Terrain, Friendly Support, Fortress Command and Mission Objectives remain active.
- Existing War Bonds remain the only mission/economy reward currency.
- Existing Health/ArmorSystem/Projectile combat paths are unchanged.

## Validation target
Keep this branch off `main` until Windows x64 CI is green and runtime playtesting confirms route pacing, sector rewards, endgame difficulty and UI readability.
