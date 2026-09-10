# TANK REVIVAL: ORZEŁ OVERDRIVE

Original top-down tank combat for Unity 6, inspired by the fast readable feel of classic 8-bit console tank games and rebuilt from scratch with modern effects, progression and a 100-round campaign.

## Mission

Defend the **Orzełek stronghold** through 100 increasingly dangerous rounds. Enemy formations become faster, tougher and more aggressive as the campaign advances. Siege units prioritize the stronghold, elite tanks pressure the player, supply tanks carry special ammunition, and every tenth round ends with a boss assault.

## v0.3 development milestone — Field Command

The next major milestone adds a persistent campaign economy and tactical battle director on top of the v0.2 combat foundation.

- **War Bonds** earned from destroyed enemies and secured rounds
- **Field Command Center every five cleared rounds** before the next deployment
- permanent campaign upgrades for cannon, autoloader, engine, composite armor, Eagle sentry network and logistics
- paid field repair for Orzełek during Command Center visits
- up to **three autonomous sentry cannons** around the stronghold with target prioritization and upgraded penetration
- named tactical wave doctrines including Recon Patrol, Blitz Wave, Siege Column, Marksmen, Heavy Column, Supply Raid, Elite Hunters, Crossfire, Iron Storm and Boss Protocol
- doctrine-specific reinforcement armor, coordinated extra fire, target pressure, projectile speed and salvage rewards
- commander upgrades stack safely with normal field power-ups instead of overwriting the saved tank loadout
- CI now reads the real project `VERSION` and writes current version/control information into each Windows build

## v0.2 combat foundation

- 100 progressively harder rounds
- Orzełek stronghold with persistent health and critical-damage alarm
- automatic +1 stronghold repair between cleared rounds
- 8 enemy classes: Basic, Fast, Heavy, Sniper, Siege, Elite, Supply and Boss
- special glowing Supply Tanks that always drop ammunition
- 7 ammunition modes: Standard, AP, HE, Incendiary, EMP, Twin Shot and Plasma
- ammunition inventory persists between rounds and player respawns
- cannon, fire-rate and engine upgrades persist through the campaign
- 10 boss difficulty tiers with increasingly complex special salvos
- final round 100 boss uses radial and directional multi-stage fire patterns
- destructible brick walls, steel barriers and water obstacles
- procedural 2.5D tank visuals with layered armor, rounded turrets, lamps and animated tracks
- muzzle flashes, projectile trails, smoke, sparks, shock rings, explosions and screen shake
- tread dust and track marks with performance-aware emission
- runtime-generated combat audio: cannon shots, heavy guns, explosions, ricochets, plasma, EMP, alarms, pickups and engine loop
- high-score saving

## Special ammunition

| Key | Ammunition | Role | First available |
|---|---|---|---:|
| 1 | Standard | Unlimited general-purpose shell | Round 1 |
| 2 | AP Piercing | Faster, harder shot with penetration | Round 3 |
| 3 | HE Explosive | Area damage against enemies and brick cover | Round 8 |
| 4 | Incendiary | Applies damage over time | Round 15 |
| 5 | EMP Shock | Temporarily disables enemy movement and weapons | Round 25 |
| 6 | Twin Shot | Fires two parallel shells | Round 35 |
| 7 | Plasma | Fast, high-damage, multi-penetrating projectile | Round 50 |

Use **Q / E** to cycle through ammunition currently in inventory. Destroy colored Supply Tanks to obtain new ammunition; their glow identifies what they carry.

## Controls

- **WASD / Arrow keys** – move
- **Space / Left Ctrl** – fire
- **Q / E** – previous / next available ammunition
- **1–7** – directly select ammunition type
- **P / Escape** – pause / resume
- **Enter / Space** – start/restart and deploy from the Command Center
- **1–6 while Command Center is open** – buy the corresponding permanent upgrade
- **7 while Command Center is open** – repair Orzełek

## Windows build

Windows 10/11 x64 is the primary target. GitHub Actions compiles `TankRevivalOverdrive.exe`, verifies the executable and `_Data` directory, creates a portable ZIP and publishes stable builds under GitHub Releases. Players do **not** need Unity installed.

Stable code lives on `main`. Major development milestones are built on dedicated `dev-vX.Y` branches and validated by Windows CI before merge.

## Creative direction

The game is an original project using its own code, procedural visuals and procedural audio. It draws on broad top-down tank-game conventions but does not use ripped maps, sprites, audio or other assets from existing games.
