# Tank Revival: Orzeł Overdrive — Roadmap

This roadmap tracks large playable milestones. Small cosmetic-only releases are intentionally avoided.

## Current production track

### v4.7 — Projectile Pooling & 100-Round Stress Harness — COMPLETE
- Warm/reusable projectile runtime pool integrated with the existing Projectile authority.
- Reuse collider, Rigidbody2D and projectile renderers instead of reconstructing them for every live shot.
- Non-allocating explosive splash query buffer.
- Development-only late-round stress harness: direct round 80/90/100 jumps, pressure-wave injection and pool/GC/FPS telemetry.
- Goal achieved: the heaviest campaign combat can be profiled repeatedly.

### v4.8 — Demo Stability & Runtime Hardening — COMPLETE
- Projectile pool lifecycle validation, stale-reference pruning and bounded integrity telemetry.
- Persistent Runtime Stability Director watching authoritative campaign, registry and pool state.
- Conservative self-healing for repeated stale registry references and orphaned active projectiles after gameplay ends.
- Development-only automated F5 soak gate covering rounds 80/90/100 with injected heavy pressure waves.
- Soak summary records FPS, managed-memory pressure, pool reuse and runtime repair/warning deltas.
- Goal achieved: stable 100-round gameplay foundation suitable for a demo candidate.

### v4.9 — Demo UX, Settings & First-Run Polish — COMPLETE
- Full player-facing main menu, pause overlay, replay/end screen and Windows exit path.
- First-run onboarding explaining the objective, supply tanks, ammunition and essential controls.
- Persistent graphics presets, fullscreen/windowed mode, resolution selection, V-Sync, FPS cap and master volume.
- Graphics presets are integrated with WarfarePerformanceGovernor budget floors while dynamic load protection remains active.
- Dedicated controls reference and clear pre-demo build identity.
- Goal achieved: game can be handed to a player as a normal Windows game without developer knowledge.

### v5.0 — PUBLIC DEMO CANDIDATE — CURRENT (RC2)
- Dedicated non-development Windows x64 candidate build path in CIBuild.
- Separate Demo Candidate Windows workflow, independent from ordinary development artifact packaging.
- Source gate validates required demo/stability/pooling systems and exact RC version before Unity build starts.
- Package gate validates executable, Unity data directory, BUILD_INFO, README_DEMO and DEMO_MANIFEST.
- Candidate manifest records the exact GitHub commit used to produce the ZIP.
- ZIP receives a SHA-256 checksum and a demo-specific artifact name.
- RC2 adds a packaged Windows runtime qualification stage: the exact ZIP is downloaded on `windows-latest`, checksum-verified, extracted and the real standalone EXE is launched.
- `DemoCISmokeProbe` is enabled only with `-demo-ci-smoke` and requires live `TankGame`, player-facing demo shell and Runtime Stability Director before writing a PASS marker and exiting cleanly.
- Windows qualification also captures Player.log and rejects blocking crash/exception signatures.
- Development-only F10 acceptance overlay remains available for longer interactive menu -> play -> pause -> resume/soak confirmation.
- Final acceptance remains: green compile/package CI + green packaged Windows boot gate + no blocker from late-round 80/90/100 soak.

When this gate is fully reached, release reporting must explicitly state:

**🎮 DEMO GOTOWE DO GRANIA — WINDOWS EXE**

## v5.1+ post-demo direction
- Player feedback / difficulty tuning.
- Save/profile hardening and recovery.
- Further art/audio replacement with authored production assets where appropriate.
- Expanded campaign variants, additional bosses and challenge modes only after demo stability is protected.
- Achievements and broader distribution packaging.

## Development rules
1. Each version must be a coherent milestone with a visible gameplay, production-quality or performance gain.
2. New systems must integrate with existing authoritative Health, Projectile, TankGame, CombatRoster, campaign and economy flows.
3. Do not merge unstable development milestones to `main`.
4. Windows CI must be green before a milestone is considered release-ready.
5. Performance changes must preserve gameplay authority; presentation density may scale, combat outcomes may not.
6. Demo-facing releases must pass both compile/package CI and explicit runtime stability/smoke gates.
7. A public demo ZIP must be reproducibly attributable to its exact commit and checksum.
8. A demo candidate is not release-ready until the packaged EXE itself boots on a fresh Windows runner.
