# v2.3.0 — WAR ECONOMY & CAMPAIGN VARIANTS

## Major systems

### Persistent War Economy
- New persistent **War Bonds** account stored across runs.
- Surviving rounds pays War Bonds; sector/act milestones pay larger bonuses.
- High-risk campaign variants multiply War Bond income.
- Career progress improves a four-tier logistics ladder using Command Rank and lifetime bonds.
- Three immediately playable battlefield purchases:
  - **F1 Field Service** — repairs hull/modules and restores Orzelek HP.
  - **F2 Ordnance Drop** — AP/HE plus EMP/Plasma/Twin packages at higher logistics tiers.
  - **F3 Overdrive Insurance** — short emergency protection window for player and Orzelek.
- Higher logistics tiers reduce prices and improve support packages.

### Campaign Variants
Every 10-round sector now carries a deterministic combat modifier after the opening sector:
- **Armored Spearhead** — Heavy/Elite/Boss armor reinforced with AP hunter volleys.
- **Blackout Raid** — Sniper/Elite/Siege/Boss formations deploy EMP disruption fire.
- **Blitz Overrun** — Basic/Fast/Elite assault elements gain reinforcement and burst pressure.
- **Fortress Breaker** — Heavy/Siege/Boss units receive dedicated AP breach fire against Orzelek.

Each modifier uses real enemies from the active TankGame wave and is tied to a visible War Bond risk premium. No parallel fake wave or alternate health authority is introduced.

### CombatRoster v2.3
- Shared snapshot now caches living enemy count and per-`EnemyKind` composition.
- Adds priority-armor query for future campaign/AI directors.
- New v2.3 systems consume the existing shared roster instead of introducing extra scene-wide scans.

## Gameplay impact
- Campaign sectors now have stronger tactical identities and a visible risk/reward contract.
- Command/meta progression now feeds a spendable battlefield economy instead of only passive deployment bonuses.
- Players can decide whether to bank resources for later rounds or spend them to recover from a dangerous breakthrough.
- Orzelek remains fully destructible; emergency support is limited, paid, and temporary.

## Safety / integration
- Existing TankGame, Health, ArmorSystem, AmmoType, Operations, Strategic Directives, Boss Legends and Orzelek rules remain authoritative.
- No changes are published to stable `main` until the Windows x64 development build is green.
