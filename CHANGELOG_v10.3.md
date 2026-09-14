# Tank Revival: Orzeł Overdrive — v10.3.0-dev

## Adaptive Enemy High Command, Counter-Doctrines & Sector War Plans

v10.3 makes the enemy strategic layer react to the campaign state created by v10.2 instead of replaying a fixed late-game response.

### Adaptive Enemy High Command
- Reads the current and previous two `TheaterSectorDoctrine` outcomes from the v10.2 consequence engine.
- Uses deterministic recency weighting to choose one bounded counter-doctrine for the current sector.
- Operates only on sector offsets 4–7 (rounds 5–8 inside each ten-round sector), leaving the opening v10.2 consequence window and boss/end-sector rounds untouched.
- Displays the active enemy plan and observed doctrine history in the in-game HUD.

### Three playable counter-doctrines
- **ARMOR TRAP** counters repeated BREAKTHROUGH play by temporarily retasking existing Heavy/Sniper/Elite units into flanking, hunter and suppressor positions and adding a tightly capped AP response beat.
- **DISPERSED LOGISTICS** counters SUPPLY STARVED sectors by assigning existing combat tanks as escorts around real Supply/logistics actors, granting at most one existing-Health logistics repair per round and only one AP response shell per beat.
- **SIEGE BREACH** counters PREPARED DEFENSE by retasking existing Siege/Heavy/Elite units toward Orzełek through the existing tactical-navigation layer and allowing at most two HE breach shells per response beat.

### Authority and balance
- No second movement AI: temporary `HighCommandNavigationDirective` orders are fed into the existing `TacticalNavigationAgent` and expire automatically.
- No parallel damage model: all extra fire goes through `TankGame.SpawnProjectile` using existing AP/HE ammunition and enemy team authority.
- No parallel sustain/economy: logistics sustain uses the existing `Health.Heal` contract only.
- Hard caps: four retasked combatants, two response shells, one logistics repair per round, 5.5-second counter-fire cadence.

### Qualification
- Adds a packaged Windows v10.3 smoke probe covering doctrine mapping/history weighting, all 100 round-window mappings, caps and integration with v10.2 consequence state plus tactical navigation.
- The milestone remains development-only until the dedicated Windows gate and standard Dev Windows Build both pass.
