# v1.3.0 — STEEL DOCTRINES: EAGLE SIEGE

## Major systems

### Eagle Fortress
- Orzelek remains fully destructible and losing the defense core still ends the campaign.
- Every round now builds a destructible fortress layer around the Eagle core.
- Armor nodes physically intercept enemy shells and can be destroyed.
- Eagle sentry modules automatically engage nearby enemy armor using the normal projectile system.
- From round 25 a limited-charge repair relay can restore Orzelek if the relay survives.
- Fortress durability and module count scale through the 100-round campaign.
- New fortress HUD exposes core health, active modules and critical alerts.

### Steel Doctrines
Every War Garage chassis now changes active combat gameplay, not only statistics. Press **R** to activate the selected doctrine:
- **Orzel Mk-I Assault — Shock Barrage:** seven-shot AP assault fan.
- **Bastion Heavy — Eagle Aegis:** temporary player/core shielding plus fortress field repair.
- **Wicher Scout — Wicher Overboost:** combat dash followed by an EMP radial counterattack.
- **Hunter TD — Rail Lance:** high-speed Plasma/AP anti-armor lance.

Each doctrine has its own cooldown, VFX/audio feedback and HUD state.

### Eagle Siege AI
- Enemy forces now treat Orzelek as an explicit strategic objective.
- Siege and Boss units always qualify as Eagle Hunters; Heavy/Elite/Fast units can join the assault as campaign pressure rises.
- Eagle Hunters execute telegraphed long-range siege fire against the core.
- Late Siege/Boss attacks use explosive shells that can break fortress modules and open lanes to Orzelek.
- Enemy assault doctrines escalate from Probing Attack to Breach Column, Siege Network and Final Eagle Hunt.
- A dedicated threat strip reports active Eagle Hunters so the player can prioritize targets.

## Engineering
- Development Windows CI now accepts every `dev-v*` milestone branch instead of requiring a workflow edit every version.
- All new combat systems compose with the existing Health, Team, Projectile, War Garage, Boss Legends and 100-round campaign systems.
- No v1.3 code is intended for `main` until Windows x64 CI is green.
