# v5.6.0-dev — Ace Commanders & Live Bounty Hunts

## Gameplay
- Rare deterministic Ace Commander encounters now appear across the 100-round campaign using existing live enemy actors.
- Four Ace archetypes: Precision Hunter, Breaker, Blitz Raider and Siege Marshal.
- Each archetype has bounded endurance scaling, its own special-attack cadence, ammunition identity and target preference.
- Named callsigns and pulsing battlefield identification make the priority threat readable during mass combat.

## Bounties & economy
- Every promoted Ace opens a timed live bounty objective.
- Destroying the Ace before the deadline pays War Bonds through `WarEconomyDirector.AwardMissionBonds`.
- Missing the timer does not despawn or weaken the Ace; only the bonus reward expires.

## Architecture
- `AceCommanderDirector` consumes `RuntimeBattleRegistry.EnemySnapshot` rather than adding repeated scene scans.
- `EnemyTank`, `Health`, `Projectile`, `TankGame` and the existing War Bond economy remain authoritative.
- Boss and Supply enemies are excluded from Ace promotion to avoid conflicting with existing boss and logistics systems.

## Qualification
- `AceCommanderCISmokeProbe` validates the four-archetype catalog, bounded health mutation, cadence/reward ranges and runtime installation.
- `ace-commanders-windows.yml` boots the exact packaged Windows x64 EXE on a fresh Windows runner and rejects blocking runtime exception signatures.
