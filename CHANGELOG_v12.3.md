# Tank Revival: Orzeł Overdrive v12.3 — Reconnaissance Network & Electronic Counter-Logistics

## Milestone intent
v12.3 turns the v12.2 route-intelligence layer into a playable information-war objective. Enemy logistics can now be protected by a physical jammer and a bounded EW guard cell while the player must collect route packets from distinct Orzełek scout relays or destroy the jammer to break interference.

## Gameplay systems
- Two physical Orzełek scout relays spawn around an active enemy logistics route. Each relay uses canonical `Health`, collision and a kinematic `Rigidbody2D`.
- Driving close to distinct live relays collects intelligence packets. One packet establishes `CONTACT`; both packets verify the real route.
- A physical enemy EW jammer spawns off the real route lane. While alive it reduces relay sync radius and keeps a visible spoof-risk state active.
- Destroying the jammer restores signal confidence, awards the existing War Bonds currency and upgrades route intelligence without creating a parallel reward or damage system.
- Existing Fast/Elite/Sniper enemies form a capped three-actor EW guard screen through `TacticalNavigationAgent`; `EnemyTank` and `Rigidbody2D` remain movement authority.
- Compact recon/EW HUD telemetry exposes packet count, signal quality, jammer state, spoof risk and current guard strength.
- All relays, jammer state, guard references and packets are removed/reset when the logistics route resolves or the round changes.

## Safety / authority
- No projectile spawning, direct-damage path or second movement controller is introduced by v12.3.
- The real logistics column remains owned by `OperationalSustainmentDirector`.
- Existing `LogisticsRouteIntelligenceDirector` remains the route/reroute/decoy authority; v12.3 only raises its existing monotonic intelligence state through a validated bridge.
- Relay and jammer infrastructure use ordinary `Health` + collider authority and bounded HP scaling.

## Qualification target
The milestone is not qualified until the exact `dev-v12-3` candidate passes source/authority contracts, Unity 6000.3.17f1 `StandaloneWindows64` build, packaged EXE boot and the dedicated `-tr-v123-smoke` runtime probe on a fresh Windows runner. Only then may the eight v12.3 roadmap deliverables be marked complete.
