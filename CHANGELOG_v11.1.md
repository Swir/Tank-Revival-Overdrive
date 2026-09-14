# Tank Revival: Orzeł Overdrive — v11.1.0-dev

## ARMOR FACINGS, COMPONENT DAMAGE & MOBILITY KILLS REFORGE

v11.1 deepens minute-to-minute armored combat on top of the qualified v11.0 platoon layer. The milestone keeps `Health`, `Projectile`, `ArmorSystem`, `TankGame` and `TacticalNavigationAgent` authoritative while making hit direction, ammunition choice and persistent component damage materially change battlefield behavior.

### Directional armor and ammunition interaction
- Front, side and rear exposure now feed explicit penetration resistance and overmatch behavior instead of only a flat damage multiplier.
- AP receives strong penetration and greatly reduced ricochet under overmatch.
- Plasma has the highest penetration and no ricochet, with strong component pressure.
- HE is less efficient against frontal armor but can damage side modules/tracks.
- EMP trades raw penetration/damage for forced component pressure.
- Incendiary ammunition becomes more dangerous against rear engine/ammo-rack exposure.

### Component operational states
- Engine, Tracks, Gun and Ammo Rack now expose `Operational`, `Damaged`, `Critical` and `Disabled` conditions.
- Critical/disabled engine or tracks can create a real mobility kill while Health remains the only destruction authority.
- Critical/disabled gun or ammo rack produces stronger reload and weapon-function penalties.
- Ammo-rack failure creates bounded extra lethality without adding a second health pool.
- Existing field-repair salvage remains the recovery authority.

### Damage-aware tactical AI
- New `ComponentDamageTacticsDirector` observes authoritative component states and issues bounded orders through existing `TacticalNavigationAgent` instances.
- Mobility-killed Heavy/Siege/Elite vehicles can become protected anchors while nearby healthy units screen them.
- Weapon-disabled units withdraw/reposition instead of behaving as fully functional attackers.
- Recent side/rear exposure on the player's tank can be exploited by at most two healthy Fast/Elite/Sniper flankers.
- Existing v11.0 platoon and High Command layers remain authoritative; no parallel movement system is introduced.

### Anti-armor hunter reforge
- Existing Armor Hunters now choose AP/HE/EMP/Plasma according to player component damage and recent armor exposure.
- Exploitation of a badly crippled player receives an additional telegraph delay to preserve counterplay.

### Readability
- Armored Warfare damage-control HUD now shows module condition labels, last hit armor zone, ammunition type, overmatch state and explicit mobility/weapon critical warnings.

### Qualification
- Dedicated `Armor Component Reforge v11.1 Windows Gate` builds and packages the exact Windows x64 candidate, then boots that exact EXE on a fresh Windows runner using `-armor-component-reforge-smoke`.
- Standard `Dev Windows Build` remains required in parallel, including established packaged-EXE regression coverage.
- ROADMAP v11.1 may be locked to complete only after both Windows gates are green.
