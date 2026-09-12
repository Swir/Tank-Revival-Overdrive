# v4.8.0 — Demo Stability & Runtime Hardening

## Major systems

### Runtime Stability Director
- Added a persistent low-frequency runtime sentinel for long 100-round sessions.
- Validates authoritative TankGame round bounds, RuntimeBattleRegistry player/Orzełek references and ProjectilePool integrity.
- Uses conservative self-healing only after repeated registry faults to avoid fighting legitimate respawn/arena rebuild transitions.
- Automatically releases orphaned active projectiles when gameplay has ended.
- Detects prolonged no-combat conditions during an active campaign and emits diagnostics for potential soft-lock investigation.
- Development F2 hold-overlay exposes check/repair/warning counts, registry state, revision and projectile pool faults.

### Projectile Pool hardening
- Added destroyed-reference pruning for both active and inactive projectile collections.
- Added explicit pool integrity validation to detect active/inactive overlap, invalid activation state and retention-cap violations.
- Added lifecycle telemetry for integrity faults and stale-reference cleanup.
- Stress/restart cycles can now validate that pooled objects remain bounded and internally consistent instead of relying only on object counts.

### Automated late-round soak gate
- Extended the development stress harness with F5 AUTO SOAK.
- Automatically runs rounds 80, 90 and 100, injects +24 Heavy/Siege/Sniper/Elite pressure waves and samples the session continuously.
- Captures minimum instantaneous FPS, peak managed-memory reading, pool creation/reuse counts and stability warning/repair deltas.
- Produces PASS / CHECK / FAIL summary output and stops immediately on projectile-pool integrity failure.
- Existing manual F6/F7/F8/F9 late-round controls remain available.

## Why this milestone matters
v4.7 made heavy combat profileable. v4.8 makes that profiling repeatable and adds runtime guard rails for stale references, orphaned pooled objects and campaign-state inconsistencies. This is a production-hardening milestone: gameplay authority remains in TankGame, Health, Projectile and RuntimeBattleRegistry; the new layer observes and repairs only clearly invalid runtime state.

## Demo path
The next milestone is v4.9 — Demo UX, Settings & First-Run Polish. After that, v5.0 becomes the public Windows demo candidate and must pass full Windows CI plus the demo smoke/stability gate before release.
