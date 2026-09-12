# Tank Revival: Orzeł Overdrive — v5.2.0-dev

## Production Art & Audio Overdrive Pass

This milestone pushes the qualified post-demo branch toward a more authored, readable production presentation without changing combat authority, collision rules, AI, damage, spawning or campaign state.

### Production audio
- Added imported runtime audio resources under `Assets/Resources/TankRevivalProduction/Audio/`.
- Added an authored Heavy Cannon layer used for Armor Piercing and Explosive fire.
- Added an authored Boss Alarm cue for boss destruction feedback.
- Added `ProductionBattleAudioDirector` with a bounded six-voice playback pool, positional stereo panning and distance attenuation.
- Authored overlays degrade with `WarfarePerformanceGovernor`: enemy heavy-cannon overlays are skipped in SURVIVAL and volume density is reduced in BALANCED/SURVIVAL.
- Existing `BattleAudio` and `EnhancedBattleAudioDirector` remain available as fallback/ambient layers.

### Enemy class readability
- Added `ProductionPresentationDirector` and `EnemyClassSignature`.
- Every existing `EnemyKind` receives a distinct visual detail package attached to the existing enemy GameObject.
- Fast: speed fins / scout mast.
- Heavy: heavy skirts / reinforced mantlet.
- Sniper: rangefinder bar / optical eyes.
- Siege: side armor plates / rear ammunition rack.
- Elite: command deck / paired antennas.
- Supply: external supply crate / medical-style cross marking.
- Boss: armored side pods / crown / core detail.
- Basic: simplified armor-band signature.
- Presentation geometry does not modify Health, EnemyTank AI, Rigidbody2D, authoritative colliders or damage resolution.

### Performance integration
- Production details listen to `WarfarePerformanceGovernor.BudgetChanged`.
- Optional silhouette details are disabled in SURVIVAL while core class readability remains.
- Enemy signature discovery cadence slows automatically under SURVIVAL load.
- Authored battle-audio density and volume scale down before gameplay systems are touched.

### Runtime qualification
- Added `ProductionPresentationCISmokeProbe`, active only with `-production-presentation-smoke`.
- The probe requires the real standalone player to load both authored WAV resources.
- The probe instantiates and configures signature packages for every `EnemyKind`, then verifies the SURVIVAL presentation path.
- `Dev Windows Build` now has a downstream Windows job which downloads the exact packaged development ZIP, boots `TankRevivalOverdrive.exe`, requires a PASS marker and rejects common crash/runtime exception signatures.

### Release safety
- Work remains isolated on `dev-v5-2`.
- `main` and the qualified v5.0 demo candidate are not modified by this milestone.
- ROADMAP deliverables stay unchecked until Windows compile/package plus the real-EXE presentation smoke gate are green.
