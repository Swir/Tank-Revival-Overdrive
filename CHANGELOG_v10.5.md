# v10.5.0-dev — Player Counter-Orders, Reserve Traps & Operational Intelligence

## Major gameplay milestone

- Confirmed v10.4 deception intelligence now opens one bounded player decision window during the real main-effort phase instead of resolving counter-intelligence as a passive automatic debuff only.
- **Z — BLOCK** hardens the revealed main axis with bounded Orzełek/player recovery plus authoritative AP defensive fire.
- **X — COUNTERATTACK** converts the identified feint into a reserve trap, directing bounded HE support into the false axis while the enemy commitment is exposed.
- **C — DEEP STRIKE** exploits the revealed operation plan to interdict high-value Siege/Elite/Sniper/Heavy targets with authoritative AP fire.
- If the player does not answer within eight seconds, the fallback order adapts to real Orzełek health: BLOCK under heavy pressure, COUNTERATTACK from a strong position, otherwise DEEP STRIKE.
- Every operation accepts at most one order, at most two response beats and two support shells per beat. BLOCK recovery is capped at three HP per operation and mission-bond payouts remain bounded.
- Existing `HighCommandDeceptionWarDirector`, `CommandNetworkHuntDirector`, `DynamicFrontlineTerritoryDirector`, `TankGame`, `Projectile`, `Health`, enemy roster and `WarEconomyDirector` remain authoritative.

## Qualification

A dedicated Windows x64 gate builds the exact v10.5 candidate and boots the packaged EXE on a fresh Windows runner with `-counter-orders-smoke`. The standard development Windows build and existing packaged runtime regressions must also be green before ROADMAP v10.5 can be locked complete.

`main` and the public Release line remain untouched by this development milestone.
