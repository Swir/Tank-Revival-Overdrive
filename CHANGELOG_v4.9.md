# v4.9.0 — DEMO UX, SETTINGS & FIRST-RUN POLISH

## Major milestone
This release turns the accumulated gameplay stack into a normal player-facing Windows game flow instead of a developer-first build.

### Demo Experience Shell
- New `DemoExperienceDirector` installed automatically at runtime.
- First-run onboarding explains the 100-round objective, core controls, ammunition switching and supply-tank language.
- Full main menu: Play, Settings, Controls and Exit.
- Player-facing pause overlay with Resume, Settings, Controls and Windows exit.
- Dedicated victory/defeat presentation with replay path.
- Persistent pre-demo build identity is visible without exposing developer-only controls as the primary UX.

### Real persistent settings
- Graphics preset: Auto / Quality / Balanced / Performance.
- Fullscreen/windowed switching.
- Resolution selection from the actual Windows display modes reported by Unity.
- V-Sync toggle and 60/90/120/144 FPS cap selection.
- Persistent master volume using `AudioListener.volume`.
- All settings survive restart through PlayerPrefs.
- A late startup bootstrap reapplies saved display policy after legacy `TankGame.Awake` defaults.

### Performance-governor integration
- `WarfarePerformanceGovernor` now respects the selected graphics preset as a minimum presentation-budget tier.
- Balanced and Performance may proactively reduce transient FX/track/wear density, while Auto/Quality retain the full budget when healthy.
- Dynamic frame-pressure escalation still overrides the preset when needed.
- Combat damage, enemy counts, AI decisions, campaign progression and economy are never reduced by graphics settings.
- F3 telemetry now displays the selected player preset and its budget floor.

### Demo usability
- Complete bilingual PL/EN control reference.
- Normal launch path requires no developer hotkeys or knowledge.
- Existing authoritative `TankGame`, `Health`, `Projectile`, campaign, economy and HUD systems remain intact underneath the shell.

## Release gate
- Keep v4.9 off `main` until Windows x64 CI is green.
- v5.0 is the public demo-candidate gate: launch/play/pause/settings/restart/exit smoke validation plus late-round acceptance.
