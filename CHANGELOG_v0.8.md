# Tank Revival: Orzeł Overdrive — v0.8 Frontline Evolution

## Campaign mission system

Rounds 6–99 can now receive deterministic special operations layered on top of the normal enemy assault. Boss rounds remain focused encounters, while standard rounds rotate between mission types so the 100-round campaign no longer feels like the same defense loop repeated with larger numbers.

### Armored Escort
Protect an allied armored supply carrier while it crosses the battlefield. The convoy has campaign-scaled armor and can be destroyed by enemy fire. Successful extraction grants a frontline reward.

### Hold the Line
Two forward command posts, expanding to three in the late campaign, must survive a timed enemy assault. Losing every post fails the optional operation; holding at least one until the timer expires succeeds.

### Decapitation Strike
An enemy command bunker appears as a secondary high-value target. It has campaign-scaled health and an autonomous defensive gun that fires at the player. Destroying it completes the operation.

### Minefield Breakthrough
Faction-neutral mines reshape movement lanes and can damage either army. Mines may be deliberately baited into enemy armor, turning environmental danger into a tactical weapon.

## Frontline rewards
Successful special operations repair Orzełek by one point and deliver campaign-scaled special ammunition. From the mid-campaign onward they also grant an immediate combat upgrade, making optional objectives strategically meaningful instead of cosmetic.

## HUD and readability
A dedicated bottom-center operation panel shows the mission name, live objective state, timers, remaining strongpoints or active mines, and clear COMPLETE/FAILED resolution states.

## Technical approach
The mission layer is implemented as a self-contained runtime director and mission components. It does not replace TankGame round completion, EnemyTank AI, War Machine mutations or Tactical Warfare logic, reducing regression risk while integrating with Health, ammo, power-ups, battle audio and procedural visuals.
