# Tank Revival: Orzeł Overdrive — v12.5

## Signals Intelligence Fire Support & Deception Raids

v12.5 turns the v12.3–v12.4 reconnaissance/EW network into a playable fire-control contest instead of granting perfect target knowledge.

### Gameplay

- Dual-relay geometric SIGINT now produces bounded firing-solution confidence from real relay positions; suppressed or destroyed relays remove their contribution.
- Enemy operations can deploy one physical true fire-control emitter plus one physical decoy transmitter. Both use canonical `Health`, collision and kinematic `Rigidbody2D` authority.
- Players can close with an unknown emitter to verify its identity. Exposing the decoy isolates the true emitter, but fire support still requires a valid dual-relay triangulation solution.
- Verified SIGINT opens a short player-triggered `[F]` fire-support window. Each operation is capped at two missions with three shells per mission.
- Every support shell is created exclusively through `TankGame.SpawnProjectile(..., AmmoType.Explosive)`; v12.5 does not introduce direct hidden damage or a second projectile path.
- Fast, Elite and Sniper enemies may form a three-actor counter-SIGINT screen through the existing `TacticalNavigationAgent` as Flanker, Hunter and Suppressor roles.
- New HUD telemetry exposes triangulation confidence, deception identity, support readiness, mission budget and counter-SIGINT guard strength.

### Runtime and qualification

- All emitter, guard, mission and shell counts are hard-capped and cleaned on operation/round resolution.
- Deterministic helper contracts cover emitter placement, HP scaling, triangulation geometry and fire-mission scatter.
- `SignalsIntelligenceFireSupportCISmokeProbe` validates configuration, geometry, authority, class-role mapping and bounded cases inside the packaged Windows EXE.
- `Signals Intelligence Fire Support v12.5 Windows Gate` builds Unity `6000.3.17f1` StandaloneWindows64 and boots the exact packaged candidate before ROADMAP qualification.

No new currency, alternate tank movement authority, direct-damage shortcut or unbounded late-round actor pool is introduced.
