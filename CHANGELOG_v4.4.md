# v4.4.0 — COMBAT PRESENTATION REFORGE

## Milestone goal
Raise moment-to-moment combat readability and impact without changing authoritative gameplay systems.

## Major systems

### Cinematic combat feedback
- Caliber/ammo-aware camera trauma layered over the existing campaign camera.
- Player recoil impulse, nearby explosion shake, heavy/boss destruction kick and critical-impact response.
- Hit confirmation, kill confirmation and short combo confirmation driven by real `Projectile.DamageResolved` events.
- Player damage flash, Orzelek damage flash and critical armor edge pressure based on real `Health` values.
- Camera layer removes its own previous-frame offset before reapplying trauma to avoid cumulative drift.

### Reactive procedural battle audio
- New runtime-generated cannon body layers for light, enemy and heavy shots.
- Distinct body impacts, heavy explosions, metallic ricochets, armor-stress transients and energy impacts.
- Directional stereo panning and distance attenuation relative to the player.
- Kill/destruction confirmation layers for normal, heavy and boss targets.
- Persistent battlefield rumble scales with live enemy/heavy-unit pressure.
- Critical-state pulse is driven by player/Orzelek integrity.
- Existing `BattleAudio` remains intact and authoritative cues continue to play.

### Vehicle battle wear
- Bounded twin-track marks from real Rigidbody2D movement for player, enemies and friendly support vehicles.
- Damage-reactive armor sparks and escalating low-health wear cues.
- Track marks are presentation-only, capped and lifetime-cleaned to protect long 100-round sessions.
- Existing `Health`, `Rigidbody2D`, movement, collision and damage systems are unchanged.

## Integration rules
- No duplicate damage, health, projectile, spawn, economy or campaign model.
- `Projectile`, `Health`, `ArmorSystem`, `TankGame`, `CombatRoster` and existing v2.5 3D FX remain authoritative.
- v4.4 is additive presentation and telemetry only.

## Validation
Keep off `main` until Windows x64 CI is green. Runtime playtest should verify camera comfort, audio headroom, track-mark budget and HUD readability during late-game heavy/boss waves.
