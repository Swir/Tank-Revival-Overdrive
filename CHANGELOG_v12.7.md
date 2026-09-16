# Tank Revival: Orzeł Overdrive — v12.7 Cinematic Combat Feedback & Damage Language

v12.7 is a presentation milestone focused on making every important hit and damaged vehicle readable without changing combat authority or late-wave performance guarantees.

## Gameplay-facing changes

- Projectile impacts now publish a material-aware presentation event while preserving the existing `Impact3D` contract. Organic targets, Brick, Steel and terrain-compatible styles resolve through a deterministic ammo/material matrix for Basic, Twin, AP, HE, Plasma, EMP and Incendiary ammunition.
- A fixed 16-slot cinematic combat pool reuses shockwave rings and particle emitters. Impact processing does not create per-hit GameObjects.
- Heavy ammunition receives stronger radius, density and priority language; Steel ricochets, AP penetrators, HE blasts, Plasma and EMP remain visually distinct.
- Persistent Health feedback now exposes Healthy, Damaged, Critical and Burning states. The previous per-smoke-puff GameObject path was removed and replaced with bounded pooled pulses.
- Late-wave pressure progressively lowers simultaneous cues, particles, ring segments and audio cadence while preserving explicit readability floors.
- BattleAudio gained bounded soft/hard material impact cues. The v12.7 priority/cooldown layer deliberately does not double-play Projectile-owned HE detonation or Steel ricochet sounds.

## Authority and performance contracts

- `Projectile` remains the only collision/projectile authority and `Health` remains the only survivability authority.
- `CinematicCombatFeedbackDirector` never spawns projectiles, applies damage, moves gameplay Rigidbody2D objects, changes AI/navigation or scans Physics2D.
- The combat cue pool has a hard capacity of 16 and a hard maximum of 20 particles per cue.
- Damage smoke no longer allocates/destroys one GameObject per puff.
- Packaged runtime smoke covers 35 ammo/material style cases, damage-state thresholds, monotonic adaptive budgets, pool caps and representative audio hierarchy ordering.

## Qualification

The milestone is only marked QUALIFIED after the exact candidate passes source/authority contracts, Unity 6000.3.17f1 StandaloneWindows64 build, and packaged-EXE `-tr-v127-smoke` on a fresh Windows runner.
