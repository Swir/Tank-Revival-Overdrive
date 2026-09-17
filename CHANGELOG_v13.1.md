# Tank Revival: Orzeł Overdrive — v13.1 Development Changelog

## Dynamic Objective Warfare & Battlefield Mutators

- Adds a deterministic 100-round objective planner with seven playable archetypes: Annihilation, Sector Defense, Command Breakthrough, Emitter Hunt, Supply Interception, Convoy Rescue and Counterattack.
- Keeps the qualified v13.0 Encounter Planner as campaign-pressure authority while objective doctrine consumes the same bounded read-only operational readiness snapshot.
- Adds bounded battlefield mutators that can only adjust live concurrency by ±1, scale spawn cadence inside 0.90–1.12, or deterministically substitute limited support/specialist spawns.
- Objective-specific composition guarantees priority command/signal/supply targets without introducing a second spawner; TankGame still performs every actual enemy spawn.
- Adds a pure objective state machine for success/failure/progress transitions, including Eagle damage budgets, timed counterattacks, emitter deadlines and convoy outcome bridging.
- Integrates friendly v6.2 convoy missions through new read-only ConvoyWarfareDirector telemetry instead of duplicating convoy movement, Health or reward authority.
- TankGame reports objective outcome bonuses through its existing score path; objective systems never award a new currency, heal hidden HP, move actors, spawn projectiles or apply damage.
- Tactical HUD gains a compact cached objective/mutator/signature line with a 0.20 s runtime refresh budget.
- Packaged v13.1 smoke validates all 100 plans, all seven archetypes, anti-repeat scheduling, boss-safe doctrine, mutator hard bounds, cross-system readiness bands and pure runtime state transitions.

## Qualification policy

v13.1 remains IN DEVELOPMENT until one exact Windows x64 candidate passes the dedicated packaged-EXE gate plus v13.0, v12.9, v12.8 and round 80/90/100 regressions. ROADMAP checkboxes remain open until that exact SHA is qualified.
