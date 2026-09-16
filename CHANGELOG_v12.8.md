# Tank Revival: Orzeł Overdrive — v12.8 Development Changelog

## Full-Stack Integration & Release Train Hardening

v12.8 is an integration milestone rather than another isolated combat subsystem. Its release gate builds one Windows x64 player and qualifies the accumulated v12.0–v12.7 stack against that exact binary.

### Single-binary integration sentinel
- Added `ReleaseTrainIntegrationCISmokeProbe`, dormant in normal play and activated only by `-tr-v128-smoke`.
- The probe requires exactly one live instance of every v12.0–v12.7 runtime director and rejects duplicate service ownership.
- Public `ConfigurationValid` contracts are evaluated when a director exposes them; no private gameplay state is mutated.
- Deterministic PASS/FAIL markers include a full service-count report for CI triage.

### Release-train Windows qualification
- One Unity `6000.3.17f1` StandaloneWindows64 build is packaged once and reused for every v12.0–v12.8 runtime smoke.
- The exact same EXE is relaunched under the established v12.0, v12.1, v12.2, v12.3, v12.4, v12.5, v12.6, v12.7 and v12.8 smoke flags.
- The same artifact also runs the established `DemoCISoakProbe` through rounds 80, 90 and 100 under heavy specialist pressure.
- Runtime logs are rejected on blocking crash/type/load/index/unity exception signatures and are retained as diagnostics.

### Authority and performance hardening
- Source contracts prohibit v12.8 integration code from spawning projectiles, dealing damage, moving gameplay rigidbodies, issuing tactical navigation orders or creating an alternate economy path.
- The gate checks the accumulated bounded-system constants and keeps presentation degradation separate from combat outcomes.
- ROADMAP qualification remains pinned to the exact green candidate SHA; no v12.8 deliverable is checked merely because the version number advanced.
