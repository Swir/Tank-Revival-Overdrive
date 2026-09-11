# Tank Revival: Orzeł Overdrive — v2.8.0

## 100-ROUND ENCOUNTER CAMPAIGN

v2.8 turns the existing 100-round structure into a more authored combat campaign instead of relying only on linear enemy-count scaling.

### Major systems

- Added `CampaignEncounterDirector` as a campaign pacing layer driven by the real `TankGame.CurrentRound`.
- Added deterministic encounter archetypes across all 100 rounds:
  - Frontline Assault
  - Armored Column
  - Wolfpack
  - Sniper Net
  - Siege Push
  - Artillery Screen
  - Supply Interdiction
  - Elite Hunt
  - Last Stand
  - Boss Gauntlet
- Every tenth round receives a named boss operation, culminating in `OVERDRIVE ZERO` on round 100.
- Mid-sector round 5 encounters become high-value-target hunts.
- Sector/round combinations rotate doctrines so adjacent rounds no longer feel like the same random wave with larger numbers.

### Encounter combatants

- Added `EncounterCombatant`, attached only to real spawned enemies.
- Encounter-specific HP tuning amplifies the unit classes that fit the active battle doctrine.
- High-value targets receive a bounded health boost, visible warning pulse and short deployment protection.
- Killing a champion opens a counterattack window and briefly EMP-disrupts surviving enemies.
- Selected encounter units gain capped support-fire shots that use the existing `TankGame.SpawnProjectile` path.
- Siege, sniper, elite and boss units gain different support-fire behavior instead of a single generic bonus.

### Boss escalation

- Boss Gauntlet encounters add a three-step health-threshold escalation layer around the existing boss systems.
- Phase transitions create readable explosion/ring feedback and short bounded protection windows so transitions are not lost inside burst damage.
- Existing `BossWeaponController`, boss armor, damage and destruction paths remain authoritative.

### Strategic battlefield pressure

- Artillery Screen, Siege Push and later Boss Gauntlets can trigger telegraphed strategic strikes.
- Strikes target either the player or Orzełek depending on encounter doctrine.
- Warning rings always precede damage.
- Later campaign sectors can generate a limited follow-up impact, but total strikes are capped per round for performance and fairness.
- Strategic damage only applies to Team.Player health objects and therefore respects the existing player/Orzełek health model.

### UI / readability

- Added a compact encounter briefing panel with round codename, archetype and objective.
- Added battlefield event messages for champion deployment/destruction and boss phase escalation.

### Architecture / compatibility

- `TankGame`, `EnemyTank`, `Rigidbody2D`, `Collider2D`, `ArmorSystem`, `CombatStatus`, existing AI directors and projectile combat remain authoritative.
- The new milestone does not replace spawning or duplicate enemy ownership.
- The director scans at a bounded cadence and modifies each enemy only once per round.
- No changes are pushed to `main` until Windows x64 development CI is green.
