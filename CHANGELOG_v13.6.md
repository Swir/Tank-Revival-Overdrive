# Tank Revival: Orzeł Overdrive — v13.6

## Battlefield Sensor Fusion & Contact Warfare

v13.6 connects the existing v12.3 Recon/EW network, v13.4 tactical terrain and v13.5 weather stack into a battlefield-wide, bounded contact-confidence layer.

### Gameplay
- Adds four readable contact states: **Unknown**, **Detected**, **Tracked** and **Verified**.
- Contact confidence reacts to enemy class signature, range, current weather visibility, canonical `TacticalTerrainMap` concealment and the existing Recon/EW signal-quality service.
- Adds a player-triggered **C active sensor sweep** with a finite 12 s cooldown, 2.6 s active window and 11.5 m range.
- A successful sweep during a live Recon/EW operation reuses the existing counter-jamming bridge for a tightly bounded 3 s window; it does not create a second EW authority.
- Poor visibility reduces information quality but never despawns or visually hides enemies. Close-range contacts and bosses retain explicit fairness floors.
- Short bounded contact memory prevents noisy flicker while still allowing information to decay after a target leaves favorable sensing conditions.

### Presentation and performance
- Tracks at most 24 live registered enemies using fixed-cap arrays and the event-backed `RuntimeBattleRegistry`.
- Shows at most eight prioritized Tracked/Verified world markers.
- Compact sensor telemetry reports Detected / Tracked / Verified counts and active-sweep readiness.
- No per-tick enemy scene scans, unbounded lists/dictionaries, direct damage, projectile spawning, movement authority or new reward currency.

### Qualification
- `BattlefieldSensorFusionCISmokeProbe` covers 100 campaign rounds × all eight enemy kinds.
- Runtime smoke validates deterministic samples, weather/terrain/EW monotonicity, active-sweep range, confidence thresholds and anti-blindness floors.
- Exact-candidate Windows gate builds one Unity 6000.3.17f1 Windows x64 binary, verifies package provenance, then runs v13.6 plus v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak on that same EXE.
- `ROADMAP v13.6 Qualification Finalizer` may close the eight roadmap deliverables only after the exact current branch SHA completes the Windows gate successfully.

Public GitHub Release readiness remains separate from milestone qualification.
