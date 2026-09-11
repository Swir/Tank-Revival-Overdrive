# Tank Revival: Orzeł Overdrive — v1.9.0 DESTRUCTION & WARZONE REFORGED

## Major gameplay systems

### Persistent same-round battlefield destruction
- Destroyed tanks now leave procedural wrecks and impact craters instead of disappearing cleanly.
- Heavy, Siege, Elite and Boss losses create larger wreck fields and denser debris.
- The battlefield visually remembers combat until the round/world is rebuilt.
- Orzełek receives dedicated visible impact scars and leaves a large ruin state if the defense core is destroyed.

### Burning heavy wreck hazards
- Heavy wrecks remain dangerous for a short period after destruction.
- Burning wrecks periodically damage nearby mobile units from either faction.
- The Orzełek core is explicitly excluded from wreck-fire environmental damage, preserving enemy siege as the real base-kill condition.
- Boss wrecks burn longer and threaten a wider area.

### Progressive fortification and obstacle damage
- Multi-HP brick defenses now gain visible cracks and chipped sections before collapse.
- Destroyed brick defenses throw persistent short-lived debris across the local battlefield.
- Steel armor remains mechanically resistant but now accumulates visible dents and impact rings.
- Obstacle hits feed the same warzone scar layer as vehicle destruction.

### Late-game performance discipline
- Wrecks, craters and impact marks use hard global budgets.
- Oldest destruction objects are retired automatically when budgets are reached.
- Destroyed-world references are periodically cleaned as rounds rebuild the battlefield.
- Cosmetic debris is lifetime-limited to prevent accumulation during rounds 70–100.

## Design intent
v1.9 makes every battle leave a readable physical history without replacing the existing combat authority. Armor, factions, Eagle Siege, field engineering, ammo, bosses and the destructible Orzełek remain authoritative; the new destruction layer composes with them.
