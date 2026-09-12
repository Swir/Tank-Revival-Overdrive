# Tank Revival: Orzeł Overdrive — Roadmap

This roadmap tracks large playable milestones. Small cosmetic-only releases are intentionally avoided.

## Current production track

### v4.7 — Projectile Pooling & 100-Round Stress Harness — COMPLETE
- Warm/reusable projectile runtime pool integrated with the existing Projectile authority.
- Reuse collider, Rigidbody2D and projectile renderers instead of reconstructing them for every live shot.
- Non-allocating explosive splash query buffer.
- Development-only late-round stress harness: direct round 80/90/100 jumps, pressure-wave injection and pool/GC/FPS telemetry.
- Goal achieved: the heaviest campaign combat can be profiled repeatedly.

### v4.8 — Demo Stability & Runtime Hardening — CURRENT MILESTONE
- Projectile pool lifecycle validation, stale-reference pruning and bounded integrity telemetry.
- Persistent Runtime Stability Director watching authoritative campaign, registry and pool state.
- Conservative self-healing for repeated stale registry references and orphaned active projectiles after gameplay ends.
- Development-only automated F5 soak gate covering rounds 80/90/100 with injected heavy pressure waves.
- Soak summary records FPS, managed-memory pressure, pool reuse and runtime repair/warning deltas.
- Goal: stable 100-round gameplay foundation suitable for a demo candidate.

### v4.9 — Demo UX, Settings & First-Run Polish — NEXT
- Final main menu / pause / settings flow.
- Graphics quality presets tied to WarfarePerformanceGovernor budgets.
- Audio volume controls, display mode/resolution, controls reference and accessibility/readability pass.
- Demo-facing HUD cleanup and clear version/build identity.
- First-run flow requiring no developer keys or knowledge.
- Goal: game can be handed to a player as a normal Windows game.

### v5.0 — PUBLIC DEMO CANDIDATE
- Full Windows x64 development/release CI validation.
- Clean launch -> play -> pause/settings -> campaign -> restart/exit smoke path.
- Late-round stress/soak acceptance pass.
- Package as a normal Windows ZIP containing the executable and Unity runtime data.
- Publish only after green CI and release-candidate validation.

When this gate is reached, release reporting must explicitly state:

**🎮 DEMO GOTOWE DO GRANIA — WINDOWS EXE**

## Post-demo direction
- Player feedback / difficulty tuning.
- Further art/audio replacement with authored production assets where appropriate.
- Expanded campaign variants, additional bosses and challenge modes only after demo stability is protected.
- Save/profile hardening, achievements and distribution packaging.

## Development rules
1. Each version must be a coherent milestone with a visible gameplay, production-quality or performance gain.
2. New systems must integrate with existing authoritative Health, Projectile, TankGame, CombatRoster, campaign and economy flows.
3. Do not merge unstable development milestones to `main`.
4. Windows CI must be green before a milestone is considered release-ready.
5. Performance changes must preserve gameplay authority; presentation density may scale, combat outcomes may not.
6. Demo-facing releases must pass both compile/package CI and explicit runtime stability/smoke gates.
