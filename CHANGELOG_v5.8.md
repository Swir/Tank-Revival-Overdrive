# v5.8.0-dev — ORZEŁEK FORTRESS & ACTIVE DEFENSE NETWORK

## Major gameplay systems

- Added `OrzelekFortressDirector` as a player-controlled defensive layer around the real Eagle `Health` object.
- Added three bounded War Bond-funded actions: **AEGIS Shield (F6)**, **Repair Drone (F7)** and **Counter Battery (F8)**.
- AEGIS grants a short, cooldown-limited Eagle invulnerability window rather than permanent protection.
- Repair Drone restores a bounded amount of real Eagle HP and cannot be purchased at full health.
- Counter Battery targets only nearby registered enemies, damages through authoritative `Health.Damage`, and caps the number of targets per activation.
- Fortress readiness reacts to real Eagle damage events and exposes pressure/readiness in a live combat HUD.
- Existing War Bonds remain the only currency; no parallel economy was introduced.

## Runtime qualification

- Added `OrzelekFortressCISmokeProbe` and `Orzelek Fortress Windows Gate`.
- The exact packaged Windows x64 EXE is booted on a fresh Windows runner with `-fortress-smoke`.
- The gate verifies runtime installation, cost/cooldown/radius/target bounds and an actual War Bond balance deduction, then restores the probe balance.
- Blocking crash and exception signatures fail the runtime qualification.
