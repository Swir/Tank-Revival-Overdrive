# v4.2.0 — CAMPAIGN EVENTS & STRATEGIC BRANCHING

## Major systems

### Strategic campaign events
- Adds one major command dilemma to sectors 2–9.
- Event choices use dedicated `4` / `5` inputs so they do not collide with the existing v4.0 sector-route selection on `1` / `2` / `3`.
- Choices are per-campaign-run rather than permanently locked, so repeated campaigns can take different strategic paths.
- Events are delayed until after route deployment to avoid stacking two decision overlays at sector entry.

### Four event families
- **Broken Arrow** — rescue an allied convoy for repairs/sustainment, or raid the enemy fuel depot for larger War Bond/ammunition rewards and a heavier armored response.
- **Black Signal** — jam enemy communications for controlled EMP/intel support, or launch deep reconnaissance for stronger priority-target payouts against reinforced elite units.
- **Iron Bridge** — fortify the Orzełek crossing, or launch a demolition raid that increases explosive support while provoking tougher heavy/siege counterattacks.
- **Last Light** — establish a field hospital for hull/armor recovery, or perform a night raid for maximum spoils and plasma support against reinforced elite hunters.

### Persistent sector consequences
- Safe branches create periodic repairs, Eagle recovery, EMP reserves or armor service during the rest of the sector.
- Risk branches create recurring ordnance support and real kill-driven War Bond rewards while selectively reinforcing the enemy classes tied to the chosen operation.
- Enemy escalation reuses the authoritative `Health.SetMaximum()` path and never introduces parallel damage or spawn systems.
- Rewards reuse `WarEconomyDirector.AwardMissionBonds()` and existing `PlayerTank.AddAmmo()` inventory authority.

### Zero Front approach
- Campaign decisions accumulate non-spendable operational posture scores: support, assault and intel.
- By the final sector these resolve into one of three campaign approaches: **IRON SHIELD**, **BREAKTHROUGH**, or **GHOST SPEAR**.
- The resolved approach is exposed by `CampaignEventDirector.CurrentApproach` for the next milestone to consume in the Zero Front finale and Command Center.

## Integration
- Reuses `TankGame`, `CombatRoster`, `PlayerTank`, `Health`, `ArmorSystem`, `EnemyTank`, `WarEconomyDirector`, `BattleAudio` and v4.1 `StrategicWarMapDirector.Momentum`.
- Boss, round-clear, Orzełek loss, ammo, projectile and armor authority remain unchanged.
- No new currency, duplicate health model, duplicate enemy roster or parallel round state was added.

## Validation target
- Windows x64 development CI must compile the new director, verify the executable, package the build and upload the artifact before this PR is considered ready for review.
- Runtime playtest should focus on input overlap, event timing, risk/reward balance and stacked v4.1 + v4.2 enemy reinforcement pressure.
