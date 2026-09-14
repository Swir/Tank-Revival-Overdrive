# v10.7.0-dev — War State Director, Dynamic Operation Chains & Campaign Endgame Reforge

## Major gameplay changes

- Added a run-level `WarStateEndgameDirector` that consumes the qualified v10.6 operational momentum, captured intelligence and latest counter-order result.
- The final campaign sector now resolves into `ADVANTAGE`, `CONTESTED` or `CRISIS` instead of using one fixed late-game pressure pattern.
- Rounds 90–99 form a deterministic three-phase endgame operation: Intelligence (90–92), Interdiction (93–96), and Breakthrough (97–99).
- `COMMAND COLLAPSE`, `BREAKTHROUGH PURSUIT` and `DESPERATE DEFENSE` produce different bounded battlefield consequences through existing `TankGame.SpawnProjectile`, `Health`, Orzełek and War Bonds authority.
- Round 100 receives a boss-safe final-war layer. The existing boss remains authoritative; v10.7 only adds tightly bounded support/pressure and a readable campaign-war outcome.

## Safety and balance

- Endgame activity is restricted to rounds 90–100.
- Friendly/enemy support is capped at two shells per round.
- Recovery is capped at one HP per eligible round.
- Final advantage reward is capped at eight War Bonds.
- No parallel enemy roster, boss damage model, currency, movement controller or health system is introduced.

## Verification

- `WarStateEndgameCISmokeProbe` verifies state resolution, all 11 endgame rounds, phase mapping, plan mapping, v10.6 integration and cap compatibility in the packaged Windows EXE.
- Dedicated Windows candidate gate builds and boots the exact packaged v10.7 candidate on a fresh Windows runner.
