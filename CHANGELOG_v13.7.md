# v13.7 — Late-Round Performance & Battle Density Reforge

Status: **IN DEVELOPMENT**. Roadmap qualification and public release readiness are separate gates.

## Gameplay/runtime direction

- Adds a deterministic Normal / Dense / Critical presentation-pressure planner driven by round band, live registered battle density, explosion pressure and the existing `WarfarePerformanceGovernor` tier.
- Integrates proactive v13.7 budgets into the existing `MassBattleFxBudget`; optional trails, micro FX, tactical cues and explosion detail scale down before severe late-round presentation churn.
- Preserves gameplay density: no enemy deletion, spawn reduction, projectile suppression, hidden damage changes or alternate movement/targeting authority.
- Adds bounded 0.5-second managed-memory and GC collection telemetry with immediate escalation and delayed one-step recovery to avoid budget thrash.
- Adds a packaged-runtime smoke contract for monotonic budgets, pool integrity and runtime service installation.

## Qualification policy

The eight v13.7 roadmap deliverables remain unchecked until one exact Windows x64 candidate passes the dedicated v13.7 runtime smoke, historical v13.6/v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak on the same packaged executable.
