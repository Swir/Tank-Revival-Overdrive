# Tank Revival: Orzeł Overdrive — v8.9.0-dev

## Forward Recovery Bases, Salvage Convoys & Frontline Control

- Adds a playable strategic-salvage commitment path: press `B` near an unsecured v8.8 salvage cache to convert it into a real friendly recovery operation instead of taking the immediate FIELD/STRATEGIC payout.
- Captured materiel is moved by a vulnerable convoy with authoritative `Health` and collision to a deterministic forward position. Losing the convoy loses the operation.
- Reaching the destination establishes a temporary Forward Recovery Base that must survive a timed hold. Nearby enemy pressure contests the timer instead of applying hidden scripted damage.
- Real surviving enemy units are retasked through `TacticalNavigationAgent` and fire real authoritative projectiles at the convoy/base with strict actor, shot and cadence budgets.
- A secured base pays a bounded War Bond reward, then provides at most two support pulses: +1 player repair, +1 reserve-matched AP/HE/EMP ammunition, local base repair and a one-time Orzełek repair when Fire Support/logistics-grade salvage is secured.
- Bosses and Supply units are excluded from counteroffensive retasking; no parallel enemy roster, damage model or currency was introduced.
- Adds a packaged Windows x64 qualification gate and runtime smoke probe for v8.9 identity, bridge integrity, health progression, counteroffensive scaling and reward/ammunition mappings.

This is a development milestone and does not publish or modify the public release on `main`.
