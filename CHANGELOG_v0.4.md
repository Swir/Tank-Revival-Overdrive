# Tank Revival: Orzeł Overdrive — v0.4 REAL ARMOR

## Independent turret combat

Player and enemy tanks now separate hull movement from gun direction. The player keeps the classic cardinal WASD/arrow movement model while the turret follows the mouse in full 360-degree aim. Left mouse fire is added without removing Space or Left Ctrl. Enemy turrets independently track their chosen tactical target, allowing tanks to maneuver, retreat or flank without pointing the entire chassis at the target.

A procedural spring-based recoil system moves the turret assembly on every shot. Heavy, Siege and Boss weapons kick harder than light cannons, while Plasma/AP/HE produce stronger player recoil.

## Directional armor

Every tank now has front, side and rear armor zones derived from actual hull orientation and incoming projectile velocity. Damage, ricochet chance and critical probability depend on where a shell lands.

Heavy, Siege, Elite, Supply and Boss tanks use distinct armor profiles. Boss frontal protection scales throughout the 100-round campaign. AP ammunition sharply reduces ricochets and gains anti-armor damage, while Plasma ignores ricochet calculations entirely and gains the strongest penetration bonus.

## Module damage

Penetrating side/rear hits can become critical hits. Criticals damage one of two persistent combat modules for the lifetime of that spawned tank:

- Engine damage lowers movement speed.
- Gun/autoloader damage increases reload time.

Player module damage also persists until that tank is destroyed/respawned, creating a meaningful reason to protect the hull angle instead of simply trading HP.

## Combat readability

A new REAL ARMOR HUD reports player engine condition, gun condition, last impact zone and whether the latest armored hit was critical. Ricochets and module hits use dedicated spark bursts, shock rings and impact audio integrated with the existing procedural effect stack.

## Technical

- Added `TankTurretRig.cs` for visual turret extraction, world-space aiming and procedural recoil.
- Added `ArmorSystem.cs` for zone resolution, class-specific armor, ricochets and module degradation.
- Added `RealArmorHud.cs` for lightweight runtime combat diagnostics.
- Integrated the armor resolver directly into `Projectile.cs` before health damage/status processing.
- Updated `PlayerTank.cs` and `EnemyTank.cs` to apply module multipliers to movement/reload behavior.
- Moved development CI validation to `dev-v0.4`.
