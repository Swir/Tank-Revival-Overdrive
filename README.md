# TANK REVIVAL: ORZEŁ OVERDRIVE

Original top-down tank combat for Unity 6, inspired by the fast readable feel of classic console tank games and rebuilt from scratch with modern effects, progression and a 100-round campaign.

## Mission

Defend the **Orzełek stronghold** through 100 increasingly dangerous rounds. Enemy formations become faster, tougher and more aggressive as the campaign advances. Siege units prioritize the stronghold, elite tanks pressure the player, supply tanks carry special ammunition, and every tenth round ends with a boss assault.

## v0.4 development milestone — REAL ARMOR

The combat model now treats tanks as armored fighting vehicles instead of simple HP boxes.

- **independent turret and hull rotation** — drive one way while aiming another
- **mouse aiming + left-click fire** while keyboard fire remains available
- enemy turrets track their tactical target independently from chassis movement
- visible procedural **gun recoil** for player and enemy tanks
- **directional armor zones**: front, side and rear armor resolve incoming shell damage differently
- frontal and side armor can **ricochet** shells depending on tank class and ammunition type
- AP and Plasma ammunition defeat angled armor far more reliably than standard shells
- rear and side hits have elevated **critical-hit probability**
- critical hits can damage the **engine module** or **gun/autoloader module**
- damaged engine modules reduce movement speed for the lifetime of that tank
- damaged weapon modules increase reload time for the lifetime of that tank
- Heavy, Siege, Elite and Boss tanks have distinct armor profiles instead of relying only on higher HP
- bosses gain progressively stronger frontal/side protection through the campaign
- new **REAL ARMOR HUD** reports engine condition, gun condition, last impact zone and critical hits
- new ricochet sparks, shock rings and critical-impact feedback integrate with the existing procedural FX/audio stack

## v0.3 milestone — Field Command

- **War Bonds** earned from destroyed enemies and secured rounds
- **Field Command Center every five cleared rounds** before the next deployment
- permanent campaign upgrades for cannon, autoloader, engine, composite armor, Eagle sentry network and logistics
- paid field repair for Orzełek during Command Center visits
- up to **three autonomous sentry cannons** around the stronghold with target prioritization and upgraded penetration
- named tactical wave doctrines including Recon Patrol, Blitz Wave, Siege Column, Marksmen, Heavy Column, Supply Raid, Elite Hunters, Crossfire, Iron Storm and Boss Protocol
- doctrine-specific reinforcement armor, coordinated extra fire, target pressure, projectile speed and salvage rewards
- commander upgrades stack safely with normal field power-ups instead of overwriting the saved tank loadout

## Combat foundation

- 100 progressively harder rounds
- Orzełek stronghold with persistent health and critical-damage alarm
- 8 enemy classes: Basic, Fast, Heavy, Sniper, Siege, Elite, Supply and Boss
- special glowing Supply Tanks that always drop ammunition
- 7 ammunition modes: Standard, AP, HE, Incendiary, EMP, Twin Shot and Plasma
- ammunition inventory persists between rounds and player respawns
- cannon, fire-rate and engine upgrades persist through the campaign
- 10 boss difficulty tiers with increasingly complex special salvos
- destructible brick walls, steel barriers and water obstacles
- procedural 2.5D tank visuals, animated tracks, muzzle flashes, trails, smoke, sparks, explosions and screen shake
- runtime-generated combat audio and persistent high scores

## Special ammunition

| Key | Ammunition | Role | First available |
|---|---|---|---:|
| 1 | Standard | Unlimited general-purpose shell | Round 1 |
| 2 | AP Piercing | Faster anti-armor shot with penetration and lower ricochet chance | Round 3 |
| 3 | HE Explosive | Area damage against enemies and brick cover | Round 8 |
| 4 | Incendiary | Applies damage over time | Round 15 |
| 5 | EMP Shock | Temporarily disables enemy movement and weapons | Round 25 |
| 6 | Twin Shot | Fires two parallel shells | Round 35 |
| 7 | Plasma | Fast, high-damage, multi-penetrating anti-armor projectile | Round 50 |

Use **Q / E** to cycle through ammunition currently in inventory. Destroy colored Supply Tanks to obtain new ammunition; their glow identifies what they carry.

## Controls

- **WASD / Arrow keys** — move the hull
- **Mouse** — aim the turret independently
- **Left Mouse / Space / Left Ctrl** — fire
- **Q / E** — previous / next available ammunition
- **1–7** — directly select ammunition type
- **P / Escape** — pause / resume
- **Enter / Space** — start/restart and deploy from the Command Center
- **1–6 while Command Center is open** — buy the corresponding permanent upgrade
- **7 while Command Center is open** — repair Orzełek

## Windows build

Windows 10/11 x64 is the primary target. GitHub Actions compiles `TankRevivalOverdrive.exe`, verifies the executable and `_Data` directory, creates a portable ZIP and publishes stable builds under GitHub Releases. Players do **not** need Unity installed.

Stable code lives on `main`. Major development milestones are built on dedicated `dev-vX.Y` branches and validated by Windows CI before merge.

## Creative direction

The game is an original project using its own code, procedural visuals and procedural audio. It draws on broad top-down tank-game conventions but does not use ripped maps, sprites, audio or other assets from existing games.
