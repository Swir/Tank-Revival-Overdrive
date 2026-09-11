# v3.4.0 — PLAYER PROGRESSION, COMMANDER PERKS & META CAMPAIGN

## Milestone goal
Turn the existing passive Command Rank career into a persistent, player-directed progression layer that changes real campaign decisions without replacing the established combat, garage, economy or boss systems.

## Commander perk command
- Added three persistent five-level career tracks sharing one finite perk-point pool:
  - **Vanguard Doctrine** — deployment ordnance, kill-chain resupply, boss-round ammunition and an elite combat-surge reward.
  - **Combat Engineering** — permanent deployment hull capacity, armor module servicing, periodic Eagle repair and a once-per-round low-health emergency failsafe.
  - **Logistics Command** — battlefield supply cadence, reduced War Bond support prices and increased War Bond survival income.
- F9/F10/F11 purchase Vanguard/Engineering/Logistics levels while outside combat.
- Career points come from the existing Command Rank plus one extra point for every two unique legendary boss relics.
- Maximum earned budget is 15 points, exactly matching the 15 total perk levels, so a complete career requires both rank progression and legendary campaign victories.

## Legendary boss relic meta progression
- The ten v3.3 legendary boss tiers now feed a persistent 10-bit relic collection.
- First defeat of each legendary tier records a unique relic and updates the existing `TankRevival.BossLegendsDefeated` career metric.
- Repeat boss victories still grant battle rewards but do not duplicate career points.
- Relic victories supply immediate plasma/explosive ordnance and interact with Vanguard/Engineering perks.

## Real gameplay integration
- Existing `MetaProgressionDirector.CurrentRank` remains the authoritative Command Rank source.
- Existing `PlayerTank`, `Health`, `ArmorSystem`, `CombatStatus`, `TankGame.RepairEagle`, `Projectile.DamageResolved` and boss `Health.Died` events remain authoritative.
- Vanguard kill-chain bonuses are driven by actual player projectile kills.
- Engineer failsafe is driven by actual `Health.Damaged` events and can trigger only once per round.
- War Economy now reads the Commander Logistics track directly:
  - up to **-4 War Bonds** additional support discount;
  - up to **x1.35** survival payout multiplier.
- No duplicate damage, boss, currency or progression model was introduced.

## Safety / compatibility
- Rigidbody2D/Collider2D combat remains unchanged.
- Orzełek remains fully destructible and the campaign-loss path is untouched.
- Existing War Garage, Weapon Family Mastery, Arsenal, Fortress Command and boss systems remain independent but are now connected by the career layer.
- `main` must remain unchanged until Windows x64 CI passes and runtime progression balance is playtested.
