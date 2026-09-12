# v3.7.0 — Battlefield Operations & Friendly Support

## Major gameplay systems

- Added **Operation Iron Wing**, a recurring three-stage combined-arms operation on selected non-boss rounds.
- Phase 1 requires the player to physically secure a marked landing zone.
- Phase 2 deploys an allied armored column and asks the player to keep it combat-effective under fire.
- Phase 3 turns the surviving column into a breakthrough force and tracks real enemy kills through `Projectile.DamageResolved`.
- Operation completion awards War Bonds through the existing `WarEconomyDirector`, plus ammunition, repair support and a temporary defensive window.
- Operation failure does **not** invalidate the round or bypass `TankGame`; the battle continues under the normal campaign rules.

## Friendly support vehicles

Added three playable-world allied support roles, all using the same authoritative projectile and health systems as the rest of the game:

- **Guardian** — frontline escort with faster fire cadence and short Orzełek protection pulses.
- **Medic** — follows the player, periodically repairs nearby player armor/health and can service Orzełek when close enough.
- **Hunter** — mobile anti-armor support using Armor Piercing rounds and prioritizing Siege/Sniper threats.

All support vehicles use `Rigidbody2D`, `Collider2D`, `Health(Team.Player)` and `TankGame.SpawnProjectile` instead of a parallel damage model.

## Dynamic battlefield events

- Added a once-per-round **Emergency Support** response when the player or Orzełek reaches critical health.
- Emergency response deploys a context-sensitive Guardian or Medic and a field supply cache.
- Added collectible field supply caches that grant AP/Explosive/EMP ammunition and a small heal through existing `PlayerTank` APIs.
- Added battlefield LZ, IFF and supply markers plus a compact Operations HUD.

## Integration and stability

- v3.6 Mission Objectives continue to run independently; v3.7 operations are deliberately sparse combined-arms events rather than replacements for every directive.
- Boss rounds remain dedicated Boss Legend encounters.
- Existing round clear, Orzełek destruction, Projectile damage, ArmorSystem, CombatRoster and War Economy remain authoritative.
- No changes are made directly to stable `main`; this milestone remains isolated on `dev-v3-7` until Windows x64 CI is green and runtime playtesting confirms allied AI pathing, operation pacing and HUD readability.
