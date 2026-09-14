# Tank Revival: Orzeł Overdrive — v9.0.0-dev

## Dynamic Frontline, Territory Control & Multi-Objective Warfare

- Added three live battlefield control lanes (west/center/east) with friendly, contested and enemy ownership driven by real player/enemy presence.
- Frontline state carries partially between rounds, so territorial gains and losses influence the next engagement without becoming permanent snowballing.
- Added bounded multi-objective operations on selected non-boss rounds. They intentionally overlap existing Dynamic Battlefield objective rounds and require 2 of 3 conditions: hold the frontline, finish the existing battlefield objective, protect Orzełek or a secured Forward Recovery Base.
- Friendly territorial superiority delays existing campaign fire-support and can repair Orzełek after a successful operation; enemy superiority advances pressure within a hard bound.
- Enemy units can be temporarily retasked through existing TacticalNavigationAgent authority to contest uncontrolled frontline lanes; no parallel enemy roster, movement model, projectile system or health model was introduced.
- Added a compact frontline-control HUD that exposes lane ownership and operation score without covering the central playfield.
- Added packaged Windows runtime qualification via `-dynamic-frontline-smoke`, including 1–100 schedule validation, territory thresholds/carry bounds, objective overlap, dependency installation and exact v9.0 version identity.

## Safety / integration rules

- Boss rounds remain excluded from v9.0 multi-objective scheduling.
- Territory effects are bounded and use existing CampaignEncounterDirector, TankGame, Health, WarEconomyDirector, DynamicBattlefieldDirector, ForwardRecoveryFrontlineDirector and TacticalNavigationAgent authority.
- `main` is not a target of this development milestone; qualification happens on `dev-v9-0` first.
