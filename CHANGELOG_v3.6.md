# Tank Revival: Orzeł Overdrive v3.6.0 — MISSION OBJECTIVES & DYNAMIC BATTLE EVENTS

## Milestone goal
Break the repetitive "destroy everything" rhythm without replacing the proven TankGame round-clear path. v3.6 adds deterministic tactical directives that are completed through the real movement, enemy roster, Eagle health and War Economy systems.

## New mission directive layer
Every non-boss round receives one of six tactical objectives:

- **Hold Relay** — physically occupy a battlefield uplink zone long enough to secure it.
- **Armor Break** — destroy a quota of Heavy / Siege / Elite armor.
- **Counter-Battery** — specifically hunt Sniper / Siege support units.
- **Supply Interdiction** — destroy a supply carrier; automatically converts to an armor directive if RNG never produces one.
- **Eagle Shield** — keep Orzełek completely undamaged for a timed window.
- **Commander Hunt** — eliminate the strongest currently available marked priority target; gracefully converts to a relay objective when no valid target spawns.

Boss rounds remain dedicated Legend operations so v3.3 multi-phase bosses are not diluted by unrelated side objectives.

## Gameplay integration
- Objective progress reads the existing `CombatRoster`; no duplicate enemy registry was introduced.
- Commander Hunt marks a real `EnemyTank` and resolves using its real `Health` state.
- Eagle Shield reads the authoritative Orzełek `Health` object.
- Hold Relay uses the real player transform and creates a visible battlefield capture zone.
- Completion rewards flow through the existing War Economy rather than a new currency.
- Selected objectives can trigger a short support pulse: player repair, brief protection, AP resupply and temporary Eagle protection.
- Objective fallback logic prevents optional tasks from becoming impossible because of random wave composition.

## Economy integration
`WarEconomyDirector.AwardMissionBonds(...)` is now the single mission-reward bridge. Mission payouts update both current War Bonds and lifetime logistics progression, so v3.4 Commander Logistics and existing economy progression remain coherent.

## Presentation
- Persistent mission HUD at the top of the battle view.
- Relay zones use live pulsing world markers.
- Commander Hunt targets receive repeated tactical ring pings.
- Success/failure messaging is explicit and uses existing audio / visual feedback paths.

## Stability rules
- Existing `TankGame` spawning and round-clear logic remains authoritative.
- Existing `EnemyTank`, `Health`, `ArmorSystem`, `Projectile`, `Rigidbody2D` / `Collider2D`, boss and Orzełek destruction systems are unchanged.
- v3.6 stays off `main` until Windows x64 CI is green.
