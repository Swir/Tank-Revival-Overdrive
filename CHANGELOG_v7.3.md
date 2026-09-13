# v7.3.0-dev — Formation Movement, Pathing & Tactical Navigation Reforge

## Major gameplay changes

- Converts v7.2 squad roles into movement doctrine rather than leaving all enemies on independent local direction changes.
- Adds role-aware formation objectives for Vanguard, Flanker, Suppressor, Breaker, Escort and Hunter behavior.
- Snipers maintain standoff space, Fast/Hunter units close pressure, Breakers route toward Orzełek, and Escorts stay near Heavy/Siege/Boss priority units.
- Adds local obstacle steering with bounded forward probes plus neighbor separation to reduce clumping and repeated collision loops.
- Adds deterministic anti-stall detours when an agent is trying to move but makes no meaningful progress.
- Uses RuntimeBattleRegistry snapshots and caps tactical navigation at 40 managed enemies to keep late-round CPU cost bounded.

## Authority and safety

- Health, Projectile, TankGame, spawning, damage, scoring and collision remain authoritative.
- TacticalNavigationAgent only supplies bounded Rigidbody2D movement intent after the legacy EnemyTank movement pass; it does not create a parallel health, projectile or spawn model.
- Supply tanks are excluded from tactical formation control.

## Qualification

- Dedicated `Tactical Navigation Windows Gate` builds the exact Windows x64 candidate and boots the packaged EXE with `-tactical-navigation-smoke`.
- Standard Dev Windows Build remains required before ROADMAP deliverables are marked complete.
