# Tank Revival: Orzeł Overdrive — v0.2

## Core redesign

- Replaced the generic base objective with the Orzełek defensive stronghold.
- Stronghold health persists between rounds and receives a small repair after a successful defense.
- Critical stronghold damage triggers visual warning feedback and a procedural alarm.
- Player weapon, reload, engine and special-ammo progress now persists between rounds and respawns.

## Enemy roster

- Basic: standard combat unit.
- Fast: high mobility pressure unit.
- Heavy: armored slow attacker.
- Sniper: long-range high-speed projectile unit.
- Siege: strongly biased toward attacking the Orzełek.
- Elite: aggressive high-tier combat unit with additional fire behavior.
- Supply: glowing ammunition carrier that always drops a special-ammo crate.
- Boss: one every tenth round with ten escalating special-weapon tiers.

## Special ammunition

- AP Piercing: faster, stronger penetration shell.
- HE Explosive: area damage.
- Incendiary: damage-over-time effect.
- EMP Shock: temporary enemy weapon and movement shutdown.
- Twin Shot: dual parallel projectiles.
- Plasma: rare fast high-damage multi-penetrating projectile.
- Standard: unlimited fallback ammunition.

## Presentation

- Redesigned layered 2.5D procedural tank construction.
- Segmented animated tracks and tread motion.
- Orzełek stronghold generated entirely at runtime.
- Supply-tank ammunition color identification.
- Damage smoke, critical sparks, muzzle flashes and projectile afterglow.
- Larger multi-layer explosions with smoke, flash and shock rings.
- Track marks and dust optimized to avoid excessive runtime object counts.
- Round-sector ground palettes, craters and battlefield debris.

## Audio

All current effects are synthesized at runtime; no external copyrighted sound assets are used.

- player cannon
- enemy cannon
- heavy cannon
- plasma discharge
- EMP pulse
- steel ricochet
- small and large explosions
- upgrade pickup
- ammunition pickup
- boss alarm
- Orzełek critical alarm
- round-clear cue
- procedural tank engine loop

## Campaign

- 100 rounds.
- Difficulty pressure increases every round through combinations of movement speed, projectile speed, fire cadence, aggression, enemy composition and simultaneous-enemy limits.
- Boss every 10 rounds.
- Boss special attack complexity scales through ten tiers and culminates in multi-stage radial fire on round 100.
