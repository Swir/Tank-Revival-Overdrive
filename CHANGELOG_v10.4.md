# Tank Revival: Orzeł Overdrive — v10.4.0-dev

## HIGH COMMAND RESERVES, FEINT OPERATIONS & COUNTER-INTELLIGENCE WAR

v10.4 extends the adaptive enemy high command into a playable deception-war layer rather than adding another isolated combat system.

### Two-axis enemy operations
- Mid/late sectors can now run a bounded masking phase followed by a real main effort.
- Every operation selects two distinct lanes from the existing three-lane Dynamic Frontline: one true main axis and one credible feint.
- Existing enemies are temporarily retasked through `HighCommandNavigationDirective` / `TacticalNavigationAgent`; no parallel navigation or enemy roster is introduced.
- The operation stays away from pre-boss and end-sector rounds.

### Strategic reserve commitment
- `StrategicReserveAttritionDirector.Current` now directly gates how much strength High Command can commit.
- ARMOR TRAP reads armor reserves, DISPERSED LOGISTICS reads EW reserves and SIEGE BREACH reads fire-support reserves.
- Commitments are capped at four combatants total with one feint element; normal v8.5 destruction trackers remain the authority that actually drains enemy reserves when Heavy/Siege/Sniper/Elite assets are destroyed.
- Fire-support reserve state also gates bounded AP/HE support, capped at two shells per beat.

### Counter-intelligence gameplay
- Existing `G` SIGINT and `H` Recon from Command Network Hunt can resolve which lane is the true main effort while an operation is active.
- Before identification both lane signals deliberately look equivalent.
- Confirmed intelligence marks main/feint separately, removes one main-effort commitment when possible and reduces the next support-fire cap.
- The system therefore rewards using already-existing reconnaissance instead of giving the player omniscient HUD information.

### Qualification
- Added `HighCommandDeceptionWarCISmokeProbe` for packaged-EXE validation.
- Added a dedicated Windows x64 build + fresh-Windows runtime smoke gate.
- ROADMAP qualification remains locked behind both the dedicated v10.4 gate and the standard Dev Windows Build.
- `main` and public Release remain untouched until qualification is green.
