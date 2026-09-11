# v3.5.0 — COMMAND CENTER & CAMPAIGN UX CONSOLIDATION

## Major milestone

v3.5 does not add another isolated combat subsystem. It consolidates the systems built across v2.3–v3.4 into a single deployment and campaign interface so the game's mechanical depth becomes usable without memorizing overlapping function-key maps.

## Command Center

- New full-screen Command Center opened with `TAB`.
- Six pages: Overview, Vehicle, Modules, Weapons, Commander and Campaign.
- Interactive pre-deployment chassis selection.
- Interactive permanent Cannon / Loader / Engine / Armor upgrades.
- Interactive v3.1 module unlocks and selections using the same shared Garage Marks economy.
- Interactive v3.2 primary weapon family selection with per-family Combat Mastery progress.
- Interactive v3.4 Commander career spending across Vanguard, Engineering and Logistics.
- Live campaign page reports current round, sector, War Bonds, fortress doctrine, command charges and doctrine bonuses.
- Overview performs build-synergy analysis between chassis, weapon family and selected garage modules.
- Configuration is explicitly deployment-locked while a campaign is active; the same authoritative runtime systems remain in control.

## Input conflict repair

v3.1 Garage Loadouts and v3.4 Commander Career both consumed F9/F10/F11 before deployment. A compatibility guard now prevents one key press from mutating two persistent systems:

- Plain F9/F10/F11 keep legacy module cycling.
- Shift+F9/F10/F11 keep legacy module unlock behavior.
- Ctrl+F9/F10/F11 are reserved for legacy Commander perk purchases.
- While Command Center is open, those legacy keys cannot mutate either configuration path.

The preferred v3.5 workflow is the new mouse-driven Command Center.

## Integration and safety

- No replacement combat model was introduced.
- Existing `PlayerPrefs` keys remain the single persistent source consumed by War Garage, Garage Loadout, Weapon Families and Commander Career.
- Existing `PlayerTank`, `Projectile`, `ArmorSystem`, `WarEconomyDirector`, `EagleFortressCommandDirector` and Orzełek destruction/loss paths remain authoritative.
- No unstable changes are intended for `main`; v3.5 stays on its milestone branch until Windows CI and runtime validation are green.
