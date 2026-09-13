# Tank Revival: Orzeł Overdrive — v6.0.0-dev

## DYNAMIC BATTLEFIELD & OBJECTIVE WARFARE

This milestone changes the rhythm of the 100-round campaign by adding playable battlefield objectives and telegraphed environmental threats on top of the existing authoritative combat systems.

### Major gameplay systems
- Added `DynamicBattlefieldDirector` with three deterministic objective families: **Secure Relay**, **Demolish Artillery Uplink**, and **Restore Fortification**.
- Objectives use the existing player, `Health`, projectile collision/damage, Eagle repair, and War Bond reward paths rather than parallel gameplay state.
- Objective rewards are bounded to 5–13 War Bonds and boss rounds remain reserved for boss-specific systems.
- Added two live battlefield hazard families: **Artillery Danger Zones** and **Minefields**.
- Artillery gives a 2.8-second visible escape window, can hurt the player, and can also punish enemy armor caught inside the strike radius.
- Minefields are limited, visible, one-shot hazards and are kept away from the initial player spawn corridor.
- Added a dedicated objective HUD plus artillery countdown warnings.

### Production / safety
- Added `DynamicBattlefieldCISmokeProbe`.
- Added `Dynamic Battlefield Windows Gate`, which compiles a Windows x64 candidate, packages it, downloads the exact ZIP on a fresh Windows runner, boots the real EXE, and verifies the v6.0 objective/hazard catalog and tuning bounds.
- The runtime gate rejects common crash/exception signatures.

### Authority rules
- `TankGame` remains round/spawn authority.
- `Health` remains damage/death authority.
- `RuntimeBattleRegistry` remains live combat membership authority.
- `WarEconomyDirector` remains War Bond authority.
- Existing boss, command-network, fortress, projectile-pool and campaign systems remain intact.
