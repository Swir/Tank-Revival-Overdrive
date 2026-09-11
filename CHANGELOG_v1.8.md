# Tank Revival: Orzeł Overdrive v1.8.0 — COMBAT PRESENTATION & TACTICAL HUD

## Milestone goals
v1.8 focuses on battlefield readability and combat feedback after the large mechanical expansion of v1.3–v1.7. The game now exposes the information required to make fast decisions during late campaign rounds without changing the authoritative combat, Orzełek, faction, siege or armor systems.

## Eagle Threat Assessment
- New `TacticalThreatDirector` continuously scores live enemies by class, commander status, distance to Orzełek and current combat condition.
- Produces a 0–100 Eagle Threat value with CLEAR / CONTACT / PRESSURE / HEAVY SIEGE / EAGLE BREACH IMMINENT states.
- Tracks critical enemies already inside the close defense zone.
- Selects a live priority target for the player based on actual strategic danger.
- Critical threat state triggers bounded Eagle alarms and visual warning pulses.

## Combat Presentation
- New `CombatPresentationDirector` subscribes to existing `Health.Damaged` events instead of replacing damage logic.
- Adds floating damage callouts for normal hits, rear hits, critical module hits and direct Orzełek damage.
- Adds world-space markers for the current priority target, War Commanders, Bosses, Siege and Elite units, plus enemies already threatening the Eagle line.
- Important enemies receive compact health bars and damaged-module integrity readouts.
- Presentation lists are hard-capped and battlefield scans are throttled to protect rounds 70–100 performance.

## Tactical Combat HUD
- New bottom-center tactical command strip consolidates the information most relevant during active combat.
- Shows round, current enemy faction, Eagle Threat, active ammunition and current priority target.
- Integrates the v1.7 Engine / Tracks / Gun / Ammo Rack model directly into the HUD.
- Reports COMBAT READY, FIELD REPAIR ADVISED, MOBILITY CRITICAL, WEAPON SYSTEM CRITICAL or VEHICLE CRITICAL states.
- Existing HUDs remain authoritative; this layer is a compact tactical overview rather than a replacement for existing specialist panels.

## Stability and integration
- No changes to projectile ownership, Orzełek destruction rules, enemy spawning, armor damage authority or campaign win/loss conditions.
- Uses existing `Health`, `ArmorSystem`, `EnemyTank`, `WarCommander`, `EnemyFactionDirector`, `AmmoDatabase` and `BattleAudio` APIs.
- v1.8 remains off stable `main` until Windows x64 CI is green and runtime playtest is satisfactory.
