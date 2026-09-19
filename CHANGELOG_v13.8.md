# Tank Revival: Orzeł Overdrive — v13.8 development changelog

## Battlefield Suppression & Morale Warfare

v13.8 introduces a bounded enemy suppression layer that consumes combat evidence without becoming a second combat authority.

### Gameplay
- Adds deterministic **Steady / Pressed / Suppressed / Broken / Recovering** morale states with hysteresis and a hard anti-permanent-Broken recovery window.
- Player projectile impacts and bounded trajectory near-misses create suppression pressure; AP and Plasma provide stronger deliberate counterplay, while bosses remain resistant rather than immune.
- Existing `EnemyTank` remains the sole movement/fire owner and consumes only clamped movement, reload, spread and fallback-direction intent.
- v13.3 cohesion feeds recovery vulnerability and retains priority over regroup intent; v13.7 late-round pressure budgets reduce cheap mass near-miss pressure rather than lowering enemy counts.
- Ally losses create short-range morale shock without bonus damage, enemy deletion, stun locks or hidden healing.

### Presentation & performance
- Fixed capacity: 24 tracked enemies and at most eight morale markers.
- Sample cadence is 0.25 s; registry snapshots replace scene-wide enemy scans in projectile pressure handling.
- Marker text changes only on state/sample updates and uses v13.7 tactical presentation budgets.

### Verification
- `Tools/verify_v138_suppression_morale.py` guards authority boundaries, caps, integration and hot-path constraints.
- `BattlefieldSuppressionMoraleCISmokeProbe` verifies thresholds, hysteresis, recovery, AP/Plasma ordering, boss resistance, density anti-cheap-pressure behavior and bounded multipliers.
- No v13.8 roadmap deliverable is complete until a single exact packaged Windows candidate passes v13.8 plus the required historical regression/late-round soak matrix.
