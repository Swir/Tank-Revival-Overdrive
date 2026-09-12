# v5.4.0-dev — Career Records & Achievements

## Major systems
- Persistent career journal for lifetime enemy kills, boss kills, runs and round milestones.
- Ten gameplay-earned achievements ranging from first kill to round 100 and veteran run milestones.
- F2 career overlay showing lifetime records, PlayerProfile furthest-round context and achievement state.
- Dedicated packaged Windows runtime qualification for catalog integrity and persistence round-trip.

## Integration rules
- PlayerProfileDirector remains the authority for resilient profile/recovery state.
- CareerRecordsDirector observes authoritative TankGame and RuntimeBattleRegistry actors; it does not change combat outcomes.
- Achievement state persists independently so existing v5.1 profile schema is not destabilized.
- Nothing is published to main until compile/package and packaged Windows runtime gates are green.
