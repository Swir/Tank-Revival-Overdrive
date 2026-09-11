# Tank Revival: Orzeł Overdrive — v2.7.0

## ENEMY TACTICS & BATTLE GROUPS

v2.7 is a gameplay milestone focused on making enemy forces behave like organized armored formations instead of isolated units.

### Battle groups
- Living enemies are automatically assigned to persistent combat groups.
- Group size scales from four to six units as the campaign advances.
- Every group receives a callsign, leader, doctrine, cohesion state and synchronized order cycle.
- Existing `EnemyTank`, `Health`, `ArmorSystem`, status effects and Rigidbody2D collision remain authoritative.

### Tactical roles
Groups assign real movement jobs instead of sending every tank directly at the same point:
- Vanguard
- Left / right flanker
- Siege escort
- Fire-support element
- Eagle raider
- Reserve
- Commander

### Dynamic doctrines
A group's doctrine changes from its actual composition, sector and battlefield losses:
- Coordinated Assault
- Pincer Attack
- Siege Column
- Fire Support
- Armored Breakthrough
- Eagle Raid
- Fighting Withdrawal

Siege tanks pull escorts around themselves, fast tanks split to opposite flanks, snipers maintain standoff positions, heavy formations form breakthrough groups, and damaged formations can disengage instead of suiciding one tank at a time.

### Sector-aware enemy behavior
The ten-round campaign sectors now influence the tactical layer, not only visuals/hazards. Later fortress/final sectors bias enemy groups toward Eagle raids and armored breakthroughs, while maneuver sectors favor pincers.

### Coordinated fire
From round 12 onward eligible groups can launch bounded synchronized volleys. Volley size and cadence scale with campaign progress, doctrine and commander presence, while remaining capped to protect balance and performance.

### Formation movement and anti-clumping
`EnemyTacticalAgent` executes after the legacy movement decision and replaces only the final Rigidbody2D movement target. It adds:
- role-based formation anchors,
- flank collapse behavior,
- siege escort spacing,
- sniper/fire-support standoff movement,
- reserve positioning,
- local separation to reduce tank stacking,
- low-health withdrawal,
- stuck detection and short unstick maneuvers,
- arena-bound clamping.

### Commander integration
Existing `WarCommander` enemies become actual battle-group leaders. Their groups receive stronger movement tempo and prefer breakthrough doctrine rather than commander status being only a threat/HUD modifier.

### Tactical readability
A compact Enemy Battle Net panel reports the active enemy doctrine, number of organized groups and number of coordinated units so the player can understand why a wave is behaving differently.

## Compatibility / safety
- No migration to 3D physics.
- No replacement of proven projectile, armor, status or collision authority.
- Orzełek remains fully destructible.
- v2.7 is isolated on `dev-v2-7` until Windows x64 CI and runtime playtesting are green.
