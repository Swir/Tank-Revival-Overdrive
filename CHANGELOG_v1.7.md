# Tank Revival: Orzeł Overdrive — v1.7.0 ARMORED WARFARE REFORGED

## Major gameplay systems

### Four-module armor damage model
- Critical hits now damage a concrete tank module: Engine, Tracks, Gun or Ammo Rack.
- Every module has 0–100% integrity and persists for the lifetime of the tank.
- Engine/Track damage directly reduces mobility.
- Gun/Ammo Rack damage directly increases reload time.
- Rear and side hits have different module-selection profiles.
- AP and Plasma increase module-damage pressure and reduce ricochet reliability.
- Ammo-rack criticals increase resolved hull damage, making rear/side exposure dangerous.

### Battlefield salvage and field repairs
- Destroyed armored enemies now produce Field Salvage through the combat director.
- Heavy/Sniper/Siege/Elite/Boss kills are worth progressively more salvage.
- Each secured round grants a small salvage allowance so long campaigns cannot hard-lock repairs.
- `K` performs an integrated field repair of damaged modules plus one hull HP when available.
- Bastion Heavy chassis receives cheaper and stronger field maintenance, tying v1.7 into War Garage identity.
- Dedicated damage-control HUD shows all four module integrities, mobility, reload penalty and salvage reserve.

### Adaptive Armor Hunter AI
- Heavy, Sniper, Siege, Elite and Boss enemies gain telegraphed precision anti-armor attacks from round 12 onward.
- Hunters choose AP, HE, EMP or Plasma according to enemy class, campaign stage and the player's current damaged systems.
- Elite units can exploit mobility failures with EMP.
- Siege units use HE anti-armor pressure.
- Late Bosses can exploit weapon-system failures with Plasma and multi-shot precision fire.
- Attack cadence scales across the 100-round campaign but remains telegraphed instead of becoming unavoidable hitscan damage.

### Visible battlefield damage state
- Armored units below healthy module integrity emit bounded, module-colored damage effects.
- Ammo-rack, engine, gun and track failures have distinct visual warning colors.
- Effects use a global per-frame budget and scan cadence to avoid late-campaign particle explosions.

## Compatibility
- Existing directional armor, ricochets, critical-hit VFX, ammo, Health, TankGame and Orzełek systems remain authoritative.
- Orzełek destruction rules are unchanged: enemy siege remains the strategic campaign-loss condition.
- Existing War Garage chassis selection is reused rather than replaced.

## Validation target
- Full Unity Windows x64 compile.
- Executable and `_Data` verification.
- Portable development ZIP.
- GitHub Actions artifact upload.
- Remain off stable `main` until CI is green and runtime playtest is satisfactory.
