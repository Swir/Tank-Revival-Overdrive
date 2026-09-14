# v9.4.0-dev — Siege Logistics, Fire-Control Recon & Mobile Batteries

## Major gameplay systems
- Enemy siege operations now receive a physical ammunition convoy with authoritative Health and collision on eligible non-boss siege rounds.
- Existing Sniper/Elite-capable enemies can act as live fire-control spotters instead of using an invisible accuracy flag or parallel AI roster.
- Destroying the ammunition convoy or eliminating the current spotter materially reduces v9.3 siege-battery firing cadence through the existing siege director.
- Batteries react to real counter-battery damage by performing bounded displacement maneuvers, with one relocation in the mid campaign and at most two in late rounds.
- Battery relocation preserves authoritative Health, projectile, lane and siege-line state; no respawn, healing or invulnerability is granted.

## Safety and integration
- Boss rounds remain excluded by the v9.3 siege scheduler.
- Maximum one physical siege-supply node and one live spotter are active for this layer.
- Supply movement, relocation count, relocation cadence and fire-control degradation are hard-capped.
- TankGame, Health, Projectile, RuntimeBattleRegistry, EnemyTank and SiegeLineWarfareDirector remain authoritative.

## Qualification
- Dedicated packaged Windows x64 source/build/runtime smoke gate: `Siege Logistics v9.4 Windows Gate`.
- Standard `Dev Windows Build` remains required before roadmap closure.
- ROADMAP scope is registered before qualification and can only be marked complete after both Windows gates are green.
