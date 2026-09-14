# Tank Revival: Orzeł Overdrive — v8.5.0-dev

## Strategic Reserves, Reinforcement Pools & Campaign Attrition

v8.5 turns the persistent war-state layer from v8.4 into long-term material attrition. Heavy formations, fire-support assets and electronic-warfare specialists are now represented by bounded strategic reserve pools that are consumed only by real destroyed enemies.

### Strategic reserve pools
- Every campaign sector maintains Armor, Fire Support and Electronic Warfare reserves.
- Reserve pools use the existing EnemyTank/Health roster and never replace spawn, movement, projectile or damage authority.
- Heavy kills consume Armor reserves; Siege and Sniper kills consume Fire Support; Elite kills consume Electronic Warfare.

### Persistent campaign attrition
- Remaining reserves are carried forward into the next sector at a bounded ratio.
- v8.4 sector Victory reduces the enemy's next-sector replenishment while Defeat allows partial reinforcement recovery.
- This makes successful operations materially weaken later encounters instead of only changing temporary multipliers.

### Gameplay consequences
- Low Armor reserves reduce later encounter endurance.
- Low Fire Support reserves reduce support intensity, slow strategic strike cadence and can disable non-boss strategic strikes when critically depleted.
- Low EW reserves can remove non-boss champion pressure when the specialist pool is critically depleted.
- Boss encounters keep bounded minimum preparation so attrition creates an advantage without trivializing climax rounds.

### Qualification
- Dedicated `Strategic Reserves Windows Gate` builds the exact Windows x64 development candidate and boots the packaged EXE with `-strategic-reserves-smoke` on a fresh Windows runner.
- Runtime matrix verifies ten-sector reserve carryover, Victory/Neutral/Defeat ordering, class-to-reserve mapping, encounter-pressure ordering and v8.5 version identity.
- Standard Dev Windows Build remains required before ROADMAP completion.
