# v2.6.0 — 3D BATTLEFIELD EVOLUTION

## Major milestone goals

This milestone converts the campaign's ten mechanical sectors into ten visually distinct runtime 3D theaters while preserving the proven 2D combat authority introduced before the 3D migration.

## New systems

### Sector3DBattlefieldEvolution
- Rebuilds the theater identity every ten-round sector transition.
- Adds ten unique presentation sets: watch towers, industrial stacks, ruined crossroads, lightning masts, river bridge structures, ice spears, fortress bunkers, night pylons, burning gates and Overdrive monoliths.
- Keeps all new architecture outside authoritative collision paths: no new gameplay blockers or physics regressions.
- Adds animated sector elements and a theater-change banner.
- Uses deterministic per-round procedural placement for repeatable visuals.

### TheaterAtmosphere3DDirector
- Adds dedicated sector directional and combat fill lights.
- Changes ambient/key lighting for all ten campaign theaters.
- Adds storm flashes, night breathing, fire flicker and Overdrive pulse lighting.
- Reacts to real player/Orzelek damage events with temporary combat-light pressure.
- Adds pressure-aware field-of-view changes and smooth player-motion camera leading.
- Runs after Battlefield3DDirector so it layers on the stable v2.4/v2.5 perspective pipeline rather than replacing it.

## What changes in play
- Reaching rounds 11, 21, 31, etc. now visibly moves the campaign into a new theater instead of merely changing the 2D sector decoration and ground tint.
- Sector 4 feels electrically unstable, sector 6 becomes a frozen defensive front, sector 8 is a true low-light offensive, sector 9 burns around the player and sector 10 gains a distinct Overdrive endgame identity.
- Important hits on the player or Orzelek now subtly tighten the camera and increase combat lighting pressure.
- Existing BattlefieldEvolutionDirector hazards, Orzelek destruction rules, Rigidbody2D/Collider2D simulation, AI, armor, ammunition, campaign progression and v2.5 combat FX remain authoritative and unchanged.

## Stability policy
- Development only on dev-v2-6.
- Do not merge to main until Windows x64 CI is green and runtime playtesting confirms camera comfort, sector readability and late-round performance.
