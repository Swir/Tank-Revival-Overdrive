# Tank Revival: Orzeł Overdrive — Roadmap

<!-- SWIR-ROADMAP-STANDARD:v1 -->
<!-- ROADMAP-PROGRESS:START -->
<p align="center">
  <a href="https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/dev-windows-build.yml"><img alt="CI" src="https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/dev-windows-build.yml/badge.svg?branch=dev-v7-1"></a>
  <img alt="Roadmap progress" src="https://img.shields.io/badge/ROADMAP-96.3%25-7c3aed?style=for-the-badge">
  <img alt="Completed" src="https://img.shields.io/badge/DONE-105%2F109-1f6feb?style=for-the-badge">
  <img alt="Status" src="https://img.shields.io/badge/STATUS-V7.1%20IN%20DEVELOPMENT-7c3aed?style=for-the-badge">
</p>

## 📊 Overall progress

```text
███████████████████░ 96.3%
```

| ✅ Completed | ⏳ Remaining | 📦 Total | 🎯 Progress |
|---:|---:|---:|---:|
| **105** | **4** | **109** | **96.3%** |

> **Progress rule:** calculate progress from explicit roadmap deliverables only: `[x] / ([x] + [ ])`. Update the checklist first, then badges, numbers, percentage and the 20-segment bar. Never estimate progress from version numbers, commit count, elapsed time or activity.
<!-- ROADMAP-PROGRESS:END -->

This roadmap tracks large playable milestones. Small cosmetic-only releases are intentionally avoided. `ROADMAP_v03.txt` is historical only.

## v4.7 — Projectile Pooling & 100-Round Stress Harness — COMPLETE
- [x] Warm/reusable projectile runtime pool integrated with existing Projectile authority.
- [x] Reuse collider, Rigidbody2D and projectile renderers instead of reconstructing every live shot.
- [x] Non-allocating explosive splash query buffer.
- [x] Development late-round stress harness with round 80/90/100 jumps, pressure injection and pool/GC/FPS telemetry.

## v4.8 — Demo Stability & Runtime Hardening — COMPLETE
- [x] Projectile pool lifecycle validation and stale-reference pruning.
- [x] Persistent Runtime Stability Director watching campaign, registry and pool state.
- [x] Conservative recovery for repeated stale registry references and orphaned projectiles after gameplay ends.
- [x] Automated development soak gate covering rounds 80/90/100 with heavy pressure waves.
- [x] Soak summary records FPS, managed memory, pool reuse and runtime repair/warning deltas.

## v4.9 — Demo UX, Settings & First-Run Polish — COMPLETE
- [x] Player-facing main menu, pause overlay, replay/end screen and Windows exit path.
- [x] First-run onboarding explaining objective, supply tanks, ammunition and essential controls.
- [x] Persistent graphics/fullscreen/resolution/V-Sync/FPS/audio settings.
- [x] Graphics presets integrated with WarfarePerformanceGovernor while preserving combat authority.
- [x] Dedicated controls reference and clear pre-demo build identity.

## v5.0 — Public Demo Candidate — QUALIFIED RC3
- [x] Dedicated non-development Windows x64 candidate build path.
- [x] Separate Demo Candidate Windows workflow and exact-commit package manifest.
- [x] Package validation for EXE, Unity data, BUILD_INFO, README_DEMO and DEMO_MANIFEST.
- [x] SHA-256 checksum for the candidate ZIP.
- [x] Fresh Windows runner downloads and boots the exact packaged candidate EXE.
- [x] DemoCISmokeProbe requires live TankGame, demo shell and Runtime Stability Director.
- [x] DemoCISoakProbe drives the packaged standalone through rounds 80/90/100.
- [x] Soak injects Heavy/Siege/Sniper/Elite pressure while preserving authoritative gameplay systems.
- [x] Runtime qualification rejects blocking crash/exception signatures and pool/stability faults.
- [x] RC3 compile/package + packaged boot + packaged 80/90/100 soak gates are green.

When this gate is fully reached, release reporting must explicitly state:

**🎮 DEMO GOTOWE DO GRANIA — WINDOWS EXE**

## v5.1 — Resilient Player Profile & Recovery — COMPLETE
- [x] Atomic versioned JSON profile persistence with integrity checksum.
- [x] Automatic backup rotation and corruption recovery without blocking gameplay.
- [x] Legacy high-score migration plus durable run/furthest-round profile statistics.
- [x] Lifecycle-safe autosave on round transitions, focus loss, pause and application exit.

## v5.2 — Production Art & Audio Overdrive Pass — COMPLETE
- [x] Authored runtime-loaded production audio assets replace/augment purely synthesized combat layers.
- [x] Enemy classes receive immediately readable silhouette/detail packages without changing combat colliders or authority.
- [x] New production presentation obeys FULL/BALANCED/SURVIVAL performance budgets and degrades cleanly under mass-battle pressure.
- [x] Production presentation has automated runtime verification for required audio resources and class-signature installation.

## v5.3 — Expanded Campaign, Boss Contracts & Challenge Modes — COMPLETE
- [x] Sector operations add new replayable combat pressure packages across the 100-round campaign without replacing existing campaign authority.
- [x] Challenge contracts create optional high-risk objectives with real success/failure tracking and War Bond rewards.
- [x] Boss rounds receive additional contract modifiers that materially alter endurance, pressure and weak-point combat while preserving Boss Legend authority.
- [x] A Windows runtime verification gate proves sector operations, challenge contracts and boss-contract installation in the packaged development EXE.

## v5.4 — Career Records & Achievements — COMPLETE
- [x] Durable career journal tracks real enemy kills, boss kills, runs, round milestones and lifetime combat records without replacing PlayerProfileDirector authority.
- [x] Achievement catalog unlocks from authoritative gameplay observations and persists unlock state across sessions.
- [x] Player-facing career/achievement overlay exposes progress, unlocked medals and lifetime records without interrupting combat.
- [x] Packaged Windows runtime gate verifies career persistence, achievement unlocks and catalog integrity in the exact development EXE.

## v5.5 — Combat Balance, Difficulty & Telemetry — COMPLETE
- [x] Deterministic 1–100 difficulty curve smooths enemy endurance across early/mid/late campaign and prevents late-round HP cliffs while preserving class identity.
- [x] Runtime balance telemetry records round band, enemy pressure, player/Eagle health pressure and bounded-relief events without replacing gameplay authority.
- [x] Anti-spike safety policy provides tightly bounded Eagle recovery only after measurable pressure thresholds, never free invulnerability or enemy deletion.
- [x] Packaged Windows balance gate verifies tuning bounds, telemetry service installation and representative round 1/25/50/75/100 curve samples in the exact EXE.

## v5.6 — Ace Commanders & Live Bounty Hunts — COMPLETE
- [x] Deterministic Ace promotion injects rare named elite threats across the 100-round campaign with bounded endurance scaling and immediately readable battlefield identity.
- [x] Ace archetypes add distinct live tactical pressure through precision, breaker, blitz and siege command attacks without replacing EnemyTank or Projectile authority.
- [x] Live bounty tracking exposes the active Ace objective, time pressure and War Bond payout through the existing war economy instead of a parallel reward currency.
- [x] Packaged Windows Ace gate boots the exact development EXE and verifies archetype catalog, promotion health mutation, tactical configuration and runtime service installation.

## v5.7 — Boss Legends: Second Generation — COMPLETE
- [x] Second-generation boss doctrines make major boss encounters mechanically distinct through phase-aware command patterns, not simple HP inflation.
- [x] Module-reactive retaliation turns real ArmorSystem gun/track/engine/ammo-rack damage into readable boss counterplay and changing attack pressure.
- [x] Boss command telegraphs expose doctrine, retaliation state and vulnerable windows clearly while preserving BossLegendDirector weak-point and projectile authority.
- [x] Packaged Windows boss-generation gate boots the exact development EXE and verifies doctrine catalog, module reaction thresholds, bounded attack configuration and runtime installation.

## v5.8 — Orzełek Fortress & Active Defense Network — COMPLETE
- [x] War Bond-funded Eagle defense network adds player-triggered shield, repair and counter-battery options without introducing a parallel currency.
- [x] Defense readiness reacts to real Eagle damage/health state and uses bounded cooldowns/costs so active defense supplements rather than replaces combat skill.
- [x] Player-facing fortress HUD communicates available defenses, costs, cooldown/readiness and Eagle pressure clearly during live rounds.
- [x] Packaged Windows fortress gate boots the exact development EXE and verifies economy spending, defense configuration bounds and runtime installation.

## v5.9 — Command Network & Fortress Siege Counterplay — COMPLETE
- [x] Enemy command network assembles live suppressor, escort and siege cells from existing enemy classes and changes target pressure without replacing EnemyTank authority.
- [x] Fortress-breaker operations create telegraphed attacks against Orzełek with bounded counterplay windows and explicit interaction with active fortress defenses.
- [x] Command-threat HUD communicates active operation, assigned roles, countdown, counterplay state and battlefield outcome during live rounds.
- [x] Packaged Windows command-network gate boots the exact development EXE and verifies role catalog, operation bounds, fortress-counterplay configuration and runtime installation.

## v6.0 — Dynamic Battlefield & Objective Warfare — COMPLETE
- [x] Deterministic objective warfare injects playable Secure Relay, Artillery Uplink demolition and Fortification restoration missions across non-boss rounds without replacing TankGame round authority.
- [x] Battlefield hazard layer adds visible minefields and telegraphed artillery danger zones with bounded damage, counterplay time and no hidden unavoidable spawn damage.
- [x] Objective HUD, battlefield markers and existing War Bond/Eagle repair integrations make the new systems readable and materially connected to progression and fortress defense.
- [x] Packaged Windows dynamic-battlefield gate boots the exact development EXE and verifies objective/hazard catalog, deterministic scheduling, tuning bounds and runtime installation.

## v6.1 — Battlefield Control & Multi-Stage Operations — COMPLETE
- [x] Deterministic multi-stage operations chain capture, interdiction and final hold phases into one playable mission arc on selected non-boss rounds without replacing TankGame round authority.
- [x] Sector-control simulation allows enemies to contest and reverse capture progress while designated command targets connect live combat to operation advancement.
- [x] Operation HUD and battlefield markers clearly expose current phase, contest state, command target and War Bond reward through existing progression systems.
- [x] Packaged Windows multi-stage-operation gate boots the exact development EXE and verifies phase catalog, scheduling, contest/reward bounds and runtime installation.

## v6.2 — Convoy Warfare & Mobile Frontlines — COMPLETE
- [x] Friendly convoy escort and enemy logistics interception create mobile objectives with real Health, route progress and combat failure/success states without replacing TankGame round authority.
- [x] Deterministic ambush/frontline events connect live enemy pressure to convoy position, including bounded escort support and anti-stall route recovery instead of scripted invulnerability.
- [x] Convoy HUD and battlefield markers expose route progress, vehicle HP, current threat state and War Bond reward while using the existing economy and combat registry.
- [x] Packaged Windows convoy-warfare gate boots the exact development EXE and verifies mission catalog, route/reward/health bounds, deterministic scheduling and runtime installation.

## v6.3 — Combined Arms & Reinforcement Warfare — COMPLETE
- [x] Enemy reinforcement command posts deploy bounded specialist relief waves into active combat through authoritative TankGame spawning and can be destroyed to stop the flow.
- [x] Combined-arms response teams react to convoy/objective pressure with deterministic escort, hunter and siege compositions while preserving existing enemy AI and health authority.
- [x] Player battlefield support earns limited artillery/air-support charges from destroyed reinforcement infrastructure and exposes readable telegraphs, targeting and War Bond rewards without a parallel currency.
- [x] Packaged Windows combined-arms gate boots the exact development EXE and verifies reinforcement scheduling, spawn caps, support-charge bounds, target authority and runtime installation.

## v6.4 — Combat Readability & Adaptive HUD — COMPLETE
- [x] Adaptive combat HUD consolidates simultaneous objective, convoy, fortress, command-network and combined-arms information into a bounded priority display instead of independent permanent text panels.
- [x] Focus/standard/minimal HUD density modes preserve critical warnings while allowing the player to reclaim battlefield visibility during heavy combat.
- [x] Legacy tactical panels are safely suppressed only during rendering while their gameplay directors continue updating authoritative combat state without changing outcomes.
- [x] Packaged Windows combat-readability gate boots the exact development EXE and verifies HUD installation, density-mode bounds, legacy-panel suppression safety and live gameplay authority.

## v6.5 — Battlefield Presentation & Visual Overdrive — COMPLETE
- [x] Persistent vehicle damage-state presentation adds readable hull distress, critical-health pulse and class-aware threat markers without modifying Health, armor or AI authority.
- [x] Event-driven combat feedback adds bounded player-hit vignette, enemy-hit confirmation and destruction emphasis so impacts are readable without adding permanent text panels.
- [x] High-value battlefield silhouettes make Boss/Elite/Siege threats visually identifiable through lightweight world-space rings/chevrons that obey strict presentation budgets.
- [x] Packaged Windows visual-overdrive gate boots the exact development EXE and verifies presentation installation, damage/threat thresholds, FX budgets and unchanged authoritative Health behavior.

## v6.6 — Cinematic Battlefield & Environment Reforge — COMPLETE
- [x] Sector-aware environment reforge adds deterministic atmospheric layers, battlefield depth cues and distinct visual identities across the 100-round campaign without modifying colliders or combat authority.
- [x] Comfort-bounded combat camera adds subtle player lead, threat framing and event impulses while preserving the full playable arena and preventing aim/physics changes.
- [x] Budgeted battlefield aftermath adds reusable track marks, impact scars and destruction debris that visually accumulate during combat and recycle under late-round pressure.
- [x] Packaged Windows cinematic-battlefield gate boots the exact development EXE and verifies atmosphere catalog, camera bounds, aftermath budgets and unchanged authoritative combat state.

## v6.7 — Vehicle Motion & Weapon Animation Reforge — COMPLETE
- [x] Vehicle presentation adds class-aware hull lean, suspension travel and track/wheel animation driven only by observed movement without changing Rigidbody2D or collision authority.
- [x] Weapon presentation adds bounded cannon recoil, muzzle flash and recovery timing for player and enemy fire while Projectile remains the sole damage authority.
- [x] Light/Fast, Heavy/Siege and Boss vehicles receive distinct motion language and recoil weight so battlefield silhouettes also communicate handling and threat class.
- [x] Packaged Windows vehicle-motion gate boots the exact development EXE and verifies motion/recoil bounds, shot notification integration and unchanged authoritative combat behavior.

## v6.8 — Weapon Impacts, Explosion & Combat VFX Reforge — COMPLETE
- [x] Ammo-aware impact signatures make Basic/AP/HE/Incendiary/EMP/Twin/Plasma visually distinct using Projectile impact authority without changing damage, penetration or status resolution.
- [x] Layered explosion and destruction presentation adds bounded flash, shockwave, sparks, smoke and debris intensity scaled by impact/destruction significance rather than spawning unlimited effects.
- [x] Projectile trail presentation differentiates high-value ammunition while obeying FULL/BALANCED/SURVIVAL budgets and preserving projectile movement/collision authority.
- [x] Packaged Windows combat-VFX gate boots the exact development EXE and verifies ammo signature catalog, FX budgets, Projectile event integration and unchanged authoritative Health behavior.

## v6.9 — Destruction, Wreckage & Battlefield Damage Reforge — COMPLETE
- [x] A single wreck-authority path replaces duplicate death-wreck generation and creates class-aware Basic/Fast/Sniper/Heavy/Siege/Elite/Boss aftermath without changing Health or kill authority.
- [x] Wreck lifecycle presentation transitions through bounded hot, smoldering and cold states with class-scaled fire/smoke/debris while preserving movement, collision and damage outcomes.
- [x] Destructible battlefield objects receive staged impact/collapse presentation and heavy-ammo reaction cues connected to existing Obstacle destruction instead of a parallel damage model.
- [x] Packaged Windows destruction-reforge gate boots the exact development EXE and verifies single wreck authority, class profile catalog, FULL/BALANCED/SURVIVAL budgets and unchanged authoritative Health/Obstacle behavior.

## v7.0 — Public Demo 2 & Final Player Experience Reforge — QUALIFIED RC1
- [x] Player-facing shell is refreshed for Demo 2 with current build identity, cleaner menu/pause/end presentation and no stale pre-demo version labels.
- [x] First-ten-round contextual coaching replaces persistent instruction clutter with short auto-hiding movement, firing, ammunition, Orzełek and tactical-HUD prompts.
- [x] Combat HUD defaults to a battlefield-first minimal presentation while preserving player-selectable Minimal/Focus/Standard modes and all critical tactical warnings.
- [x] Dedicated Demo 2 candidate packaging produces a non-development Windows x64 ZIP with exact-commit manifest and SHA-256 checksum.
- [x] Fresh Windows runners boot the exact packaged Demo 2 EXE and complete smoke plus late-round 80/90/100 qualification before the candidate is considered release-ready.

## v7.1 — Frontend, Menu & HUD Art Reforge — IN DEVELOPMENT
- [ ] Player-facing menu, pause and end-of-run shell is rebuilt into a stronger game-first visual hierarchy with concise controls and no debug-like text wall.
- [ ] Combat status becomes icon/bar-first: player armor, Orzełek health, lives, round pressure and active ammunition are represented through compact visual meters while authoritative TankGame/Health/Ammo state remains unchanged.
- [ ] Ammunition inventory and tactical alerts use bounded icon chips and critical-only text so the battlefield remains visible during high-pressure rounds and all seven ammunition families stay immediately readable.
- [ ] Packaged Windows frontend/HUD gate boots the exact development EXE and verifies frontend installation, HUD bounds, seven-ammo catalog coverage, legacy GUI suppression safety and unchanged authoritative combat state.

## Development rules
1. Each version must be a coherent milestone with a visible gameplay, production-quality or performance gain.
2. New systems must integrate with existing authoritative Health, Projectile, TankGame, CombatRoster, campaign and economy flows.
3. Do not merge unstable development milestones to `main`.
4. Windows CI must be green before a milestone is considered release-ready.
5. Performance changes must preserve gameplay authority; presentation density may scale, combat outcomes may not.
6. Demo-facing releases must pass compile/package CI and explicit runtime stability/smoke gates.
7. A public demo ZIP must be reproducibly attributable to its exact commit and checksum.
8. A demo candidate is not release-ready until the packaged EXE itself boots on a fresh Windows runner.
9. A public demo is not release-ready until that same packaged EXE also passes automated late-round 80/90/100 runtime qualification.
10. `ROADMAP.md` must preserve `<!-- SWIR-ROADMAP-STANDARD:v1 -->` and the canonical dashboard structure.
