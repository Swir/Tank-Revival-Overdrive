# Tank Revival: Orzeł Overdrive — v12.6 Battlefield Presentation Overdrive

v12.6 turns the dense late-campaign operational stack into one readable tactical picture instead of adding another combat authority.

## Unified Tactical Command HUD
- Consolidates Mobile Front, operational sustainment, route intelligence, recon/EW, mobile signal warfare and SIGINT/fire-support into a single context-prioritized command panel.
- Critical short-lived windows (inbound HE, fire-support readiness, deep enemy breakthroughs) outrank routine telemetry.
- The HUD keeps only 3–4 highest-value rows visible depending on late-wave load.

## Battlefield 2.5D telegraphs
- A fixed pool of at most 12 reusable `LineRenderer` cues draws objective rings, route vectors, relay/jammer contacts, SIGINT emitters and breach emphasis.
- No gameplay `Health`, `Projectile`, `EnemyTank`, economy or navigation state is owned by the presentation layer.
- Ring geometry is bounded to 16–28 segments and reuses preallocated buffers/materials instead of instantiating per refresh.

## Adaptive late-wave presentation budget
- Early/light combat: up to 12 world cues, 28-segment rings, 4 alert rows and 75 ms refresh.
- Mid pressure: 10 cues, 22 segments and 105 ms refresh.
- Dense rounds / heavy operational overlap: 7 cues, 16 segments and 155 ms refresh while critical alerts retain priority.
- The budget reduces presentation work only; combat outcomes and system authority never scale down.

## Read-only operational snapshot
- `TacticalPresentationSnapshot` projects public bounded state from v12.0–v12.5 directors at the presentation cadence.
- Objective positions are derived from canonical lane/front/logistics/recon/SIGINT geometry helpers; no parallel route or objective state is introduced.
- Snapshot age is explicitly bounded and qualification tests reject stale presentation windows.

## Qualification
- `BattlefieldPresentationOverdriveCISmokeProbe` validates priority ordering, adaptive-budget monotonicity, telegraph caps, snapshot freshness and 2.5D ring geometry.
- Dedicated Windows x64 CI builds the exact candidate and boots that packaged EXE with `-tr-v126-smoke` on a fresh Windows runner before roadmap qualification.
