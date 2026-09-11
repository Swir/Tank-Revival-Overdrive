# v3.1.0 — WAR GARAGE LOADOUTS & MODULE ECONOMY

## Major milestone
v3.1 turns the War Garage from a chassis/stat screen into a persistent build-crafting layer connected directly to the v3.0 Arsenal combat loop.

## Shared Garage Marks economy
- Garage Marks now fund both the original Cannon/Loader/Engine/Armor tree and the new module bay.
- Module spending is persistent and is deducted from the same available-mark calculation used by legacy upgrades.
- This prevents double-spending the same career progression currency across two garage systems.
- Module tiers cost 3 marks for tier I and 5 additional marks for tier II.

## Three persistent loadout slots
### Primary hardpoint
- **Standard Feed** — unchanged baseline cannon behavior.
- **Volley Feed** — every fifth real player projectile adds a Twin escort projectile and grants Twin resupply between rounds.
- **Breach Core** — every seventh real player projectile adds a high-speed AP escort projectile and grants AP resupply between rounds.

### Reactor bay
- **Standard Reactor** — baseline v3.0 Arsenal behavior.
- **Capacitor Bank** — real Overdrive ignition grants a short damage shield and bonus Plasma ordnance.
- **Thermal Sink** — real Arsenal reactor lock vents a close EMP shock and repairs armor modules.

### Hull system
- **Standard Hull** — baseline durability.
- **Reactive Plating** — adds hull capacity and periodically disperses a real incoming hit while repairing armor modules.
- **Repair Lattice** — adds hull capacity, repairs armor between rounds and restores hull every third round.

## Gameplay integration
- Escort rounds use the existing `TankGame.SpawnProjectile` path, so ArmorSystem, ammo behavior, impacts, effects and kills remain authoritative.
- Reactive plating listens to the real `Health.Damaged` event.
- Reactor modules use the real v3.0 `PlayerArsenalSystem` Overdrive/ReactorLocked states.
- Existing Rigidbody2D/Collider2D combat authority and Orzełek destruction/loss logic are unchanged.

## Presentation
- New loadout bay is visible in the pre-campaign garage with F9/F10/F11 controls.
- Normal F9/F10/F11 cycles unlocked packages; SHIFT + the same key unlocks the next tier.
- A compact in-battle loadout strip confirms the persistent build currently active.
- New procedural 3D hardpoint pods, reactor hardware, thermal fins, reactive plates and repair lattice pieces are physically mounted on the player tank.
- Reactor package visuals animate from live Arsenal energy/heat state.

## Safety / architecture
- Built on green v3.0 without merging experimental development into `main`.
- No replacement combat model was introduced.
- New systems are intentionally additive and tied into existing projectile, health, armor, garage and Arsenal systems.
