# Tank Revival: Orzeł Overdrive — v1.4.0 BATTLEFIELD EVOLUTION

## Milestone goal
Turn the 100-round campaign into ten visibly and mechanically distinct theaters of war while keeping Orzełek defense as the primary objective.

## Major systems

### Ten-sector battlefield identity
- Campaign sectors now change every 10 rounds, not every 20 rounds.
- Each sector has its own atmospheric profile, camera background, particle language and visual accent.
- New identities: Border Dust, Iron Haze, Ash Crossroads, Storm Rain, River Mist, Frozen Spear, Fortress Dust, Night Sparks, Burning Gate and Overdrive Storm.
- Fixed particle budgets keep the atmospheric layer predictable in late-game rounds.

### Dynamic battlefield conditions
- A deterministic condition is selected for each round based on round and sector.
- Conditions include Dust Front, Artillery Weather, Electrical Storm, Whiteout, Firestorm, Blackout and Counter-Battery Hell.
- Conditions are clearly shown in a new battlefield HUD and announced at round start.
- Hazard cadence accelerates gradually across the 100-round campaign.

### Telegraphed gameplay hazards
- Heavy artillery zones deal area damage after a visible warning.
- Electrical storms apply EMP disable effects.
- Fire bursts deal impact damage and burning damage-over-time.
- Whiteout frost shocks temporarily disable affected mobile units.
- Counter-battery rounds can produce multi-strike patterns late in the campaign.
- Environmental hazards can affect player and enemy mobile units, creating tactical positioning choices.
- Environmental hazards explicitly exclude the Orzełek defense core from direct damage; enemies remain the force that must break and destroy Orzełek.

### Battlefield presentation and performance
- Sector-specific non-colliding battlefield debris and markers reinforce each theater without changing pathing.
- Runtime decorations are rebuilt once per round and kept separate from combat objects.
- Existing runtime sprites are reused; no external art dependency is introduced.
- Atmospheric particle counts are bounded per sector instead of scaling without control.

## Files
- `Assets/Scripts/BattlefieldEvolutionDirector.cs` — new sector/condition director, hazard scheduling, sector HUD and visual identity layer.
- `Assets/Scripts/BattlefieldHazard.cs` — new telegraphed environmental combat hazard system.
- `Assets/Scripts/BattlefieldAtmosphere.cs` — upgraded from five 20-round atmospheres to ten 10-round sector profiles with fixed particle budgets.
- `VERSION` — `v1.4.0-dev`.

## Safety / integration rules
- Orzełek remains destructible through enemy assault and siege systems.
- Battlefield weather never bypasses the defense game by directly damaging Orzełek.
- v1.4 remains on `dev-v1.4` until the full Windows x64 CI pipeline is green and runtime playtesting is complete.
