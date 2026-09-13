# v6.4.0-dev — Combat Readability & Adaptive HUD

## Major gameplay-facing changes

- Added a single adaptive Tactical HUD that consolidates simultaneous fortress, command-network, objective, multi-stage operation, convoy and combined-arms status into a bounded priority feed.
- Added three live HUD density modes cycled with `F1`: Minimal, Focus and Standard. Critical warnings remain visible while lower-priority text is aggressively limited.
- Added critical-state prioritization for ambush, siege, inbound, under-fire, breaker, destroy and failed states so danger displaces routine status instead of stacking more permanent text.
- Added render-only legacy panel suppression. Existing gameplay directors still execute their normal `Update()` logic and remain authoritative; their old IMGUI panels are disabled only after gameplay updates and restored after the frame is rendered.
- Added cached reflection snapshots at 5 Hz rather than per-frame scene scans for private tactical status strings.
- Added a dedicated packaged Windows runtime gate for HUD installation, density bounds, legacy-panel consolidation and unchanged authoritative Health behavior.

## Player impact

The battlefield should remain substantially more visible during complex late-round scenarios. Instead of multiple large independent information boxes occupying different corners, the player gets one compact prioritized tactical panel with an optional minimal mode.

## Safety / authority

This milestone does not replace `TankGame`, `Health`, `Projectile`, enemy AI, convoy logic, fortress logic or mission progression. Presentation may be consolidated; combat outcomes remain owned by the existing gameplay systems.
