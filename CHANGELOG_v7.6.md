# Tank Revival: Orzeł Overdrive — v7.6.0-dev

## Tactical Counterplay, Smoke & Electronic Warfare

This milestone adds a playable counter-layer to the coordinated enemy doctrine introduced in v7.2–v7.5.

### Smoke concealment
- Player can deploy a bounded smoke screen with `R`.
- Smoke lasts 5.5 seconds, has a 2.6 unit tactical radius and a 15 second cooldown.
- While the player remains inside an active smoke zone, v7.5 adaptive predictive fire is suspended; base EnemyTank combat authority continues normally.
- Smoke presentation is budgeted and uses the existing mass-battle FX budget.

### Electronic countermeasures
- Player can trigger ECM with `C`.
- ECM disrupts coordinated squad, boss-command and adaptive-fire directors for 4.2 seconds with a 24 second cooldown.
- ECM does not stun ordinary EnemyTank movement/fire, remove enemies or modify Health directly; it specifically attacks higher-level coordination.

### EMP integration
- Existing player EMP ammunition now has strategic value beyond the existing CombatStatus EMP effect.
- Authoritative player EMP damage against enemy Health interrupts the tactical network for 3.5 seconds.
- The interrupt is driven from `Projectile.DamageResolved`; no parallel hit or damage path is introduced.

### Player readability
- Compact bottom countermeasure readout exposes Smoke/ECM readiness plus active CONCEALED/JAM state without adding a large central text panel.
- World-space pulses make smoke deployment, ECM activation and EMP network disruption readable during combat.

### Performance and authority
- Maximum two active smoke zones.
- No scene-wide per-frame enemy searches are added.
- Existing TankGame, EnemyTank, Projectile, Health, CombatStatus and ammunition authority remain intact.

### Qualification
- Dedicated `Tactical Counterplay Windows Gate` builds the exact Windows x64 candidate and boots the packaged EXE with `-tactical-counterplay-smoke`.
- Standard Dev Windows Build remains the regression gate for compile/package plus existing packaged runtime checks.
