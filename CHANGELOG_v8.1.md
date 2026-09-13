# v8.1.0-dev — Campaign Pacing, Encounter Composition & 100-Round Director Reforge

## Major gameplay changes

- Added a deterministic 100-round `CampaignPacingDirector` with six campaign beats: Recovery, Skirmish, Offensive, Special Operation, Escalation and Boss Climax.
- TankGame remains authoritative, while the pacing bridge applies bounded per-round wave size, simultaneous-enemy and spawn-tempo envelopes after the legacy round setup completes.
- `CampaignEncounterDirector` profiles are refined before combatants are configured so encounter archetype, pressure, support and strategic-strike intensity follow the same campaign beat.
- Every post-boss sector opening (11/21/.../91) is a real recovery window; every tenth round remains a boss climax.
- Mobile HQ cadence and the v8.0 onboarding thresholds are preserved as special-operation beats instead of accidentally colliding with generic maximum-pressure rounds.
- Added a short top-of-screen pacing banner so players can read when the campaign is regrouping, escalating or entering a priority operation.

## Quality and safety

- Pressure modifiers are hard-bounded: wave 0.76–1.16x, spawn delay 0.86–1.16x and encounter endurance 0.92–1.08x.
- Legacy private round counters are accessed through cached reflection fields once per runtime type, avoiding repeated member discovery and preserving the existing TankGame spawn/damage authority.
- Added `CampaignPacingCISmokeProbe` validating all rounds 1–100, boss cadence, post-boss recovery windows, operation checkpoints, tuning bounds and runtime installation.
- Added a dedicated packaged Windows gate that builds the exact candidate and boots the real Windows EXE with `-campaign-pacing-smoke`.

## Version

`v8.1.0-dev`