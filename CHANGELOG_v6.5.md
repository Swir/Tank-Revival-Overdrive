# v6.5.0-dev — Battlefield Presentation & Visual Overdrive

## Major milestone

This milestone improves battlefield readability through visual language rather than more permanent text.

- Added persistent vehicle damage-state presentation driven only by authoritative `Health` observations.
- Vehicles below the distress threshold gain an amber hull ring; critical vehicles gain a stronger red pulse and directional chevrons.
- Boss, Elite and Siege enemies receive bounded world-space threat silhouettes so dangerous classes are recognizable during crowded fights.
- Added event-driven player-hit vignette and enemy-hit confirmation around the reticle without adding another HUD panel.
- Added short destruction-emphasis halos that complement the existing `CombatFX3DDirector` explosions and `BattlefieldSmoke3DDirector` smoke layer.
- Added hard presentation budgets and conservative scan cadence to avoid turning visual polish into late-round CPU pressure.
- Gameplay authority remains unchanged: the new layer never writes Health, armor, AI, projectile or economy state.
- Added a dedicated packaged Windows runtime gate for installation/configuration bounds and Health-authority regression testing.
