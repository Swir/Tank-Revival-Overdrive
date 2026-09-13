# v6.6.0-dev — Cinematic Battlefield & Environment Reforge

## Major systems
- Added `CinematicBattlefieldDirector` as a presentation-only layer over the existing authoritative combat simulation.
- Added ten deterministic sector atmosphere identities across the 100-round campaign.
- Added comfort-bounded camera lead, threat framing and short combat impulses with hard offset/zoom limits.
- Added pooled/recycled player track marks, impact scars and destruction debris with fixed late-round budgets.
- Added a packaged Windows runtime gate that boots the exact candidate EXE and verifies v6.6 configuration plus unchanged `Health` damage/heal authority.

## Safety / authority
- No collider, AI, projectile, damage, armor, economy or round authority was moved into the presentation layer.
- Camera changes are presentation-only and execute after the legacy TankGame camera reset.
- Aftermath visuals are pre-warmed and reused rather than created without bounds during combat.
