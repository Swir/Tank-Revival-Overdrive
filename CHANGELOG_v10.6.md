# v10.6.0-dev — Counter-Offensive Campaigns, Captured Intelligence & Command Collapse

## Major gameplay systems

- Counter-order execution from v10.5 is now evaluated against live battlefield results after the response beats complete.
- BLOCK, COUNTERATTACK and DEEP STRIKE resolve to SUCCESS, STALEMATE or FAILURE using bounded battlefield evidence instead of selection alone.
- Successful outcomes seed distinct next-sector plans: Local Offensive, Feint Exploit or Command Collapse.
- Failed counter-orders can seed a bounded Enemy Recovery response using existing high-value units and Health authority.
- Operational Momentum persists within the run at -2..+3 and cannot snowball without limit.
- Successful DEEP STRIKE can capture up to two intelligence tokens; captured intel strengthens command-collapse exploitation and can cancel later enemy recovery.
- Carryover effects are limited to the first three rounds of the next sector and use existing TankGame projectile, Health, frontline, roster and War Bond systems.

## Safety / balance caps

- Assessment delay: 7.5 seconds after counter-order execution begins.
- Max carryover rounds: 3 per resolved operation.
- Max support shells: 2 per carryover round.
- Enemy recovery: at most one 1-HP high-value heal per carryover round.
- Operational Momentum: -2..+3.
- Captured intelligence: 0..2.
- Success reward: max 5 War Bonds.

## Qualification

The milestone is not qualified until the dedicated packaged Windows v10.6 gate and the normal Dev Windows Build both pass. ROADMAP deliverables remain unchecked until those gates are green.
