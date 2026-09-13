# v7.1.0-dev — Frontend, Menu & HUD Art Reforge

## Major player-facing changes
- Rebuilds the player-facing menu, pause and end-of-run shell into a stronger game-first presentation layer.
- Replaces the old combat text block with compact armor, Orzełek, round, threat and score cards.
- Adds bar-first armor and Orzełek health presentation driven only by existing authoritative `Health` / `TankGame` state.
- Adds a seven-slot ammunition chip strip covering STD, AP, HE, FIRE, EMP, TWIN and PLASMA, including selection state and remaining counts.
- Keeps ordinary pickup/status chatter off the center of the battlefield; only critical warnings are promoted to the main alert area.
- Preserves F1 Minimal/Focus/Standard tactical HUD modes and Demo 2 first-ten-round coaching.

## Authority and safety
- `FrontendHudArtDirector` is presentation-only and observes existing `TankGame`, `PlayerTank`, `Health`, `AmmoDatabase` and tactical HUD state.
- It does not modify damage, movement, collision, enemy AI, ammunition consumption, spawning, scoring or campaign progression.
- The old `TankGame` GUI is visually occluded by bounded opaque presentation surfaces rather than disabling the gameplay component.

## Qualification
- Standard Dev Windows Build regression is required.
- Dedicated Frontend HUD Windows Gate builds and packages the exact candidate, then boots the packaged `TankRevivalOverdrive.exe` on a fresh Windows runner with `-frontend-hud-smoke`.
