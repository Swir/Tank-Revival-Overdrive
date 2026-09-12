# v4.7.0 — PROJECTILE POOLING & 100-ROUND STRESS HARNESS

## Major systems

### Persistent projectile runtime pool
- Added `ProjectilePool` with a warmed baseline of reusable shells and a bounded retained capacity.
- Existing `TankGame -> Projectile.Initialize` calls remain compatible: fresh legacy projectile proxies are transparently routed into warmed pooled projectiles.
- Reuses projectile collider, `Rigidbody2D`, glow/core/ring renderers and GameObjects across combat instead of recreating the full projectile object graph for every shot.
- Tracks active, idle, created, reused and routed shot counts for development telemetry.
- Pool remains presentation/lifecycle infrastructure only; Projectile/Health/ArmorSystem retain combat authority.

### Reusable projectile implementation
- `Projectile` now has an explicit pooled lifecycle (`InitializeFromPool`, `PrepareForPool`, recycle-on-impact/timeout).
- Visual children are created once and reconfigured per ammunition type.
- Penetration, critical, weak-point, ricochet, status and explosion behavior remains connected to the existing combat events.
- Explosive splash moved from `Physics2D.OverlapCircleAll` to a reusable non-alloc collider buffer to remove a common combat allocation spike.

### Late-round stress harness
- Added a development-build-only `LateRoundStressHarness`.
- F6 jumps to round 80, F7 to round 90 and F8 to round 100 through the real TankGame round pipeline.
- F9 injects a 24-unit Heavy/Siege/Sniper/Elite pressure wave to reproduce late-campaign load quickly.
- F4 toggles the stress telemetry panel.
- Telemetry exposes smoothed FPS/frame time, managed-memory estimate, projectile pool pressure, reuse/routing counts, runtime registry population, living enemies and the active performance-governor tier.
- Harness code is compiled only in Editor/Development builds and does not add release gameplay authority.

### Roadmap / demo gate
- Added a real `ROADMAP.md`.
- v4.8 is defined as demo stability/runtime hardening, v4.9 as demo UX/settings polish and v5.0 as the public Windows demo candidate.
- Public demo remains gated on green Windows CI and release-candidate validation.

## Safety
- No change to Health, ArmorSystem, campaign economy, enemy AI or Orzełek loss authority.
- No development milestone is published to stable `main` by this branch.
