# v1.6.0 — SIEGE ENGINEERING & EAGLE DEFENSE

## Major gameplay systems

- Added active player-side **Field Engineering** during combat.
- Defense Parts are earned every round and can be spent without leaving the battlefield.
- `Z` deploys an anti-armor minefield on the main approach to Orzelek.
- `X` constructs durable defensive barricades that physically absorb enemy fire.
- `C` deploys an Anti-Siege turret prioritizing Siege, Heavy, Elite and Boss targets with AP ammunition.
- `V` performs an emergency engineering repair affecting Orzelek, Eagle Fortress modules and surviving player-built structures.

## Enemy siege engineering

- Added dedicated **Siege Sappers** promoted from Breacher/Siege/Fast units as campaign pressure grows.
- Sappers seek Eagle Fortress modules and player-built defenses, then plant direct demolition charges.
- Added **Mobile Siege Batteries** on Siege/Boss/late Heavy tanks with campaign-scaled multi-shell explosive barrages aimed at Orzelek.
- Added **Enemy Siege Relays** on Engineer/late Elite units which repair and briefly shield nearby enemy formations.
- Enemy siege doctrine escalates through BREACH RECON, SAPPER ADVANCE, DEMOLITION CORRIDOR, MOBILE SIEGE LINE, FORTRESS ANNIHILATION and TOTAL EAGLE SIEGE.

## Orzelek damage readability

- Added a dedicated core damage-state controller.
- Mid-damage Orzelek emits visible smoke and intermittent sparks.
- Critical HP produces rapid smoke, electrical/fire bursts and red warning pulses.
- Repairs now produce a visible green recovery pulse.
- Orzelek remains fully destructible; reaching zero HP still ends the campaign.

## Integration and balance

- All new defense systems use the existing Team/Health/Projectile/Ammo/CombatStatus systems.
- Anti-Siege turrets use real Armor Piercing projectiles rather than scripted damage.
- Mines use real damage plus short EMP disruption.
- Enemy batteries use real Explosive projectiles, allowing existing collision and fortress interception to remain authoritative.
- Player construction is resource-limited and capped where appropriate to prevent turret spam.
- New systems are runtime-installed and compose with v1.3 Eagle Fortress, v1.4 Battlefield Evolution and v1.5 Enemy Factions.

## Validation

- Windows x64 CI must pass before this milestone is considered merge-ready.
- Keep v1.6 off `main` until compile/package/artifact verification is green and runtime playtest is satisfactory.
