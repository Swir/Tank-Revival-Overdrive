# Tank Revival: Orzeł Overdrive — v1.0 Operation Overdrive

## Campaign command layer

- Ten named campaign sectors spanning all 100 rounds.
- Sector-entry banners and persistent best-sector progression.
- Dynamic per-round combat contracts: Shock Action, Headhunter, Steel Discipline, Blitz Clock and Boss Breaker.
- Contract medals and persistent completion statistics stored through PlayerPrefs.
- Round debriefs with kills, completion time and contract outcome.
- Contract rewards rotate between cannon power, AP resupply, repairs, special ammunition and autoloader upgrades.
- A persistent 100-round campaign progress bar gives clear macro progression.

## Combat Momentum / Overdrive

- Fast kills build Combat Momentum instead of rewarding only passive survival.
- Kill chains accelerate momentum, with heavier and more dangerous targets granting larger gains.
- At full momentum the player enters an 8.5 second Overdrive assault state.
- Overdrive grants a temporary shield, field repair, Eagle repair and campaign-scaled special ammunition.
- Kills during Overdrive can extend the assault window up to a safe cap.
- Dedicated top-center momentum HUD communicates chain, charge and active-state duration.

## Integration goals

v1.0 is intentionally layered on top of the validated v0.9 branch rather than rewriting the existing combat stack. Real Armor, Battlefield Destruction, Tactical Warfare, War Machine, Frontline Evolution and Battlefield Command remain intact while the new systems provide a single campaign-level loop and a stronger moment-to-moment reward cadence.

## Release policy

This branch is development-only until Windows x64 CI succeeds and runtime playtesting confirms that the combined v0.4-v1.0 feature stack remains stable. Stable main is not modified by this milestone automatically.
