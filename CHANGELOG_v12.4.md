# Tank Revival: Orzeł Overdrive — v12.4 Mobile Signal Warfare & Counter-Recon Raids

## Milestone intent
v12.4 turns the v12.3 reconnaissance network into a contested moving objective instead of a set of static relay/jammer props. Enemy logistics can now drag a physical mobile EW carrier behind the active front, keep interference alive after a fixed jammer is destroyed, and dispatch bounded hunter teams toward scout relays. The player can defeat the carrier conventionally or risk a close-range transmission intercept to create a temporary counter-jamming window and recover route intelligence.

## Gameplay systems
- Added `MobileSignalWarfareDirector` with one bounded physical enemy mobile jammer using canonical `Health`, `BoxCollider2D` and kinematic `Rigidbody2D` authority.
- Mobile jammer routing follows the public logistics/frontline bridge and never writes the real sustainment column body or private route lane fields.
- Added a proximity interception objective with distance-based signal quality, bounded progress decay and a one-time existing War Bond/intelligence reward.
- Added mobile interference pulses that feed the v12.3 recon network without creating a second route-intelligence model.
- Added counter-jamming recovery windows: successful intercept/destruction temporarily restores full relay sync radius even while an enemy jammer remains alive.
- Added bounded Fast/Elite/Sniper counter-recon raids through `TacticalNavigationAgent`; no direct EnemyTank rigidbody movement is introduced.
- Raider proximity can suppress a live relay for a finite duration. Suppression removes its current packet contribution but never deals hidden damage; after the window expires the player can physically return and re-sync the relay.
- Added mobile-signal HUD telemetry for jammer state, intercept progress, counter-jam time, suppressed relays and active raid strength.

## Recon bridge hardening
`ReconElectronicWarfareDirector` now exposes read-only relay positions plus bounded methods for relay suppression, mobile-jamming pulses, counter-jamming windows and recovered intelligence packets. All durations are clamped, state is reset on operation/round cleanup, and the original route-intelligence bridge remains monotonic.

## Verification
- Added packaged-EXE smoke probe `MobileSignalWarfareCISmokeProbe`.
- Added `Mobile Signal Warfare v12.4 Windows Gate` with source/authority contracts, Unity 6000.3.17f1 StandaloneWindows64 build and fresh Windows packaged-EXE runtime smoke.
- Added exact-SHA `ROADMAP v12.4 Qualification Finalizer`; roadmap completion is allowed only after the same gameplay candidate has a successful v12.4 Windows gate.
