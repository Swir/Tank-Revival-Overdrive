# v6.1.0-dev — Battlefield Control & Multi-Stage Operations

## Major gameplay milestone
- Adds deterministic multi-stage operations on selected non-boss rounds.
- Each operation chains three playable phases: capture a control point, destroy a marked enemy command vehicle, then hold an extraction/fortification zone.
- Enemy units inside the sector actively contest and reverse capture/hold progress with a bounded contest rate.
- Command targets are selected from live RuntimeBattleRegistry enemies and preserve EnemyTank/Health combat authority.
- Three operation doctrines rotate deterministically across the 100-round campaign.
- Operation completion pays bounded War Bond rewards through the existing WarEconomyDirector.
- New HUD communicates doctrine, current phase, contest state, command target health, progress and payout.

## Quality gate
- Adds a packaged Windows x64 runtime gate using `-multi-stage-operation-smoke`.
- The gate validates doctrine/phase catalogs, deterministic scheduling, boss-round exclusion, reward bounds, contest pressure bounds and runtime director installation.
- Exact packaged EXE is booted on a fresh Windows runner and blocking runtime exception signatures fail qualification.
