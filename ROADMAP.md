# Tank Revival: Orzeł Overdrive — Roadmap

<!-- SWIR-ROADMAP-STANDARD:v1 -->
<!-- ROADMAP-PROGRESS:START -->
<p align="center">
  <a href="https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/dev-windows-build.yml"><img alt="CI" src="https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/dev-windows-build.yml/badge.svg?branch=dev-v11-6"></a>
  <img alt="Roadmap progress" src="https://img.shields.io/badge/ROADMAP-100.0%25-brightgreen?style=for-the-badge">
  <img alt="Completed" src="https://img.shields.io/badge/DONE-279%2F279-1f6feb?style=for-the-badge">
  <img alt="Status" src="https://img.shields.io/badge/STATUS-V11.6%20QUALIFIED-brightgreen?style=for-the-badge">
</p>

## 📊 Overall progress

```text
████████████████████ 100.0%
```

| ✅ Completed | ⏳ Remaining | 📦 Total | 🎯 Progress |
|---:|---:|---:|---:|
| **279** | **0** | **279** | **100.0%** |

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

## v7.1 — Frontend, Menu & HUD Art Reforge — COMPLETE
- [x] Player-facing menu, pause and end-of-run shell is rebuilt into a stronger game-first visual hierarchy with concise controls and no debug-like text wall.
- [x] Combat status becomes icon/bar-first: player armor, Orzełek health, lives, round pressure and active ammunition are represented through compact visual meters while authoritative TankGame/Health/Ammo state remains unchanged.
- [x] Ammunition inventory and tactical alerts use bounded icon chips and critical-only text so the battlefield remains visible during high-pressure rounds and all seven ammunition families stay immediately readable.
- [x] Packaged Windows frontend/HUD gate boots the exact development EXE and verifies frontend installation, HUD bounds, seven-ammo catalog coverage, legacy GUI suppression safety and unchanged authoritative combat state.

## v7.2 — Enemy AI, Squad Tactics & Boss Behavior Reforge — COMPLETE
- [x] Coordinated enemy squad layer assigns six live tactical roles (Vanguard/Flanker/Suppressor/Breaker/Escort/Hunter) across existing EnemyTank classes and executes bounded crossfire/suppression/breaker beats through authoritative TankGame projectile spawning.
- [x] Squad pressure scales across the 100-round campaign with strict shot/cadence budgets, RuntimeBattleRegistry integration and no replacement of EnemyTank movement, Health, Projectile or collision authority.
- [x] Boss rounds gain phase-aware command behavior that turns BossLegend phase transitions into coordinated escort volleys and changing player/Orzełek pressure instead of isolated boss-only attacks.
- [x] Packaged Windows squad-AI gate boots the exact development EXE and verifies role catalog, shot/cadence bounds, boss-command installation, RuntimeBattleRegistry integration and version identity.

## v7.3 — Formation Movement, Pathing & Tactical Navigation Reforge — COMPLETE
- [x] Formation navigation turns v7.2 squad roles into coordinated movement objectives: flank lanes, siege escorts, sniper standoff positions, hunter pressure and breaker routes toward Orzełek.
- [x] Obstacle-aware tactical steering uses bounded physics probes, separation and anti-stall recovery so squads route around collisions without replacing Rigidbody2D or Health authority.
- [x] Class-aware formation doctrine adds regroup/fallback behavior, Heavy/Siege protection spacing and late-round pressure scaling while keeping movement budgets deterministic and bounded.
- [x] Packaged Windows tactical-navigation gate boots the exact development EXE and verifies navigation installation, role/class coverage, steering bounds, anti-stall safeguards and version identity.

## v7.4 — Terrain Intelligence, Cover & Breach Warfare — COMPLETE
- [x] Terrain-intelligence layer scores nearby brick/steel obstacles as tactical cover and refines role-aware movement objectives without replacing Rigidbody2D, Health or TankGame authority.
- [x] Cover doctrine gives Sniper/Suppressor units protected standoff anchors while Heavy/Escort units screen vulnerable allies and preserve deterministic bounded steering costs.
- [x] Breach doctrine lets Breaker/Siege units identify obstructed Orzełek approach lanes, select bounded breach points and pressure destructible brick obstacles through existing Obstacle/Projectile authority rather than a parallel damage model.
- [x] Packaged Windows terrain-intelligence gate boots the exact development EXE and verifies cover/breach configuration bounds, obstacle integration, role coverage and version identity.

## v7.5 — Adaptive Fire Control, Suppression & Combined Maneuver — COMPLETE
- [x] Predictive fire-control estimates player motion from observed positions and lets precision-capable enemies lead shots with bounded look-ahead while preserving Projectile/TankGame authority.
- [x] Suppression doctrine creates short-lived, telegraphed fire lanes that pressure movement space without direct hidden damage, with strict shot, lane and cadence budgets for late rounds.
- [x] Combined-maneuver doctrine sequences cover/suppression/flank/breach beats so Heavy/Escort screens and Suppressor fire create real movement windows for Flanker/Breaker units instead of independent actions.
- [x] Packaged Windows adaptive-fire-control gate boots the exact development EXE and verifies prediction/suppression/maneuver bounds, RuntimeBattleRegistry integration, authoritative projectile spawning and version identity.

## v7.6 — Tactical Counterplay, Smoke & Electronic Warfare — COMPLETE
- [x] Player-deployed smoke creates bounded concealment zones that suspend v7.5 predictive/combined fire-control while the player remains concealed without disabling ordinary EnemyTank combat authority.
- [x] Active ECM gives the player a cooldown-limited way to interrupt squad, boss-command and adaptive-fire coordination without deleting enemies, stunning base AI or introducing a parallel damage model.
- [x] Existing player EMP ammunition gains strategic network-disruption value through authoritative `Projectile.DamageResolved`, extending the real EMP hit path instead of simulating separate hits.
- [x] Packaged Windows tactical-counterplay gate boots the exact development EXE and verifies smoke/ECM/EMP bounds, coordination integration, authoritative Projectile linkage and version identity.

## v7.7 — EW Command Vehicles, Decoys & Counter-Countermeasures — COMPLETE
- [x] Promote bounded live Elite/Heavy/Sniper/Siege enemies into visible EW command nodes that harden part of the coordination network while reusing existing EnemyTank/Health authority.
- [x] Make coordinated AI react to player smoke with bounded flank/reposition orders, and reward destruction of the command node with a temporary tactical-superiority window.
- [x] Add a cooldown-limited player decoy that redirects bounded real enemy fire and movement pressure, with faster decoy resolution while the EW command node survives.
- [x] Packaged Windows EW-command gate boots the exact development EXE and verifies command/decoy/smoke-response bounds, tactical-system integration, authoritative projectile spawning and version identity.

## v7.8 — SIGINT, Recon Drones & Command Network Hunt — COMPLETE
- [x] Enemy command network expands into bounded relay nodes attached to live support-capable enemies, with network strength affecting command resilience without replacing EnemyTank/Health authority.
- [x] Player SIGINT scan and recon-drone sweep reveal command/relay targets for a limited window and create a playable hunt loop instead of permanent omniscient markers.
- [x] Destroying relays weakens EW command benefits and breaking the full network grants a bounded tactical-superiority/reward window integrated with existing v7.6–v7.7 counterplay.
- [x] Packaged Windows command-network-hunt gate boots the exact development EXE and verifies relay/SIGINT/recon bounds, authoritative Health integration, tactical-system linkage and version identity.

## v7.9 — Command Network Assault & Mobile HQ Warfare — COMPLETE
- [x] Selected late-round operations promote one existing Heavy/Siege/Elite enemy into a visible Mobile HQ with authoritative EnemyTank/Health, bounded durability and a relocation doctrine instead of a parallel boss entity.
- [x] Mobile HQ operations create a playable relay → escort → HQ assault loop with counterattack windows and bounded escort orders driven through existing TacticalNavigationAgent and TankGame projectile authority.
- [x] If the Mobile HQ or primary EW command node falls, a surviving Elite can perform bounded emergency command takeover; destroying the successor opens a longer command-collapse window instead of permanent AI shutdown.
- [x] Packaged Windows mobile-HQ gate boots the exact development EXE and verifies scheduling, HQ durability/relocation, counterattack budgets, emergency succession, command-network integration and version identity.

## v8.0 — Public Demo 2 Release Candidate & Full Campaign Integration — QUALIFIED RC1
- [x] Demo-facing integration layer introduces the advanced counterplay controls (`R` smoke, `C` ECM, `V` decoy, `G` SIGINT, `H` recon) progressively and compactly without restoring permanent tutorial clutter.
- [x] Campaign integration verification covers the full v7.1–v7.9 system stack and representative round bands, including EW/network warfare and Mobile HQ operation thresholds.
- [x] Dedicated non-development Demo 2 RC packaging emits an exact-commit manifest, Windows x64 ZIP and SHA-256 checksum with current v8.0 identity.
- [x] Fresh Windows runner boots the exact packaged Demo 2 RC and verifies all required v7.x gameplay/presentation directors are installed and configuration-valid.
- [x] The same packaged candidate passes integrated soak checkpoints 36/50/80/90/100 with blocking exception detection before v8.0 can be marked qualified.

## v8.1 — Campaign Pacing, Encounter Composition & 100-Round Director Reforge — COMPLETE
- [x] Deterministic 100-round pacing director assigns recovery, skirmish, offensive, special-operation, escalation and boss-climax beats with bounded pressure envelopes without replacing TankGame round authority.
- [x] TankGame wave size/concurrency/spawn tempo and CampaignEncounter composition consume the pacing profile so adjacent rounds have materially different intensity while boss, EW/network and Mobile HQ thresholds remain compatible.
- [x] Recovery and escalation windows are player-readable and prevent the advanced v7.x systems from operating at maximum pressure on every round while preserving a demanding late-game arc.
- [x] Packaged Windows campaign-pacing gate boots the exact development EXE and verifies the 1–100 schedule, pressure bounds, representative checkpoints, integration and version identity.

## v8.2 — Sector Identity, Encounter Decks & Campaign Replayability Reforge — COMPLETE
- [x] Ten 10-round sectors gain distinct combat doctrines and player-readable identities that materially change encounter emphasis without replacing TankGame, CampaignEncounter or v8.1 pacing authority.
- [x] Three bounded per-run encounter decks (Spearhead, Attrition, Disruption) rotate sector composition, champion pressure and fire-support emphasis so repeat campaigns do not replay the same 100-round sequence.
- [x] Sector/deck refinement remains compatible with boss cadence, recovery windows, EW/network thresholds and Mobile HQ operations while keeping health/fire-support/strike multipliers inside verified bounds.
- [x] Packaged Windows sector-identity gate boots the exact development EXE and verifies all 10 doctrines, all 3 decks, replay rotation, campaign checkpoints, integration bounds and v8.2 version identity.

## v8.3 — Operation Chains, Branching Objectives & Sector Campaign Arcs — COMPLETE
- [x] Every 10-round sector gains a deterministic three-stage operation chain whose Opening, Exploitation and Resolution rounds carry real outcomes forward instead of resetting strategically after every encounter.
- [x] Success/neutral/failure is derived from authoritative player and Orzełek health state; the next stage receives bounded encounter relief or escalation through CampaignEncounter without replacing TankGame, Health, Projectile, AI or economy authority.
- [x] Winning at least two stages closes the sector operation as a victory with existing War Bond rewards, while setbacks preserve enemy initiative; boss rounds remain explicitly protected from chain mutation.
- [x] Packaged Windows operation-chain gate boots the exact development EXE and verifies all 100 rounds, 30 chain rounds, consequence bounds, outcome rules, boss preservation, integration and v8.3 version identity.

## v8.4 — War State, Sector Consequences & Campaign Memory — COMPLETE
- [x] Completed three-stage operations persist as a sector war-state result, creating bounded strategic momentum that survives beyond the local v8.3 chain instead of resetting immediately after Resolution.
- [x] Sector victory/defeat materially changes the remaining sector and its boss preparation through CampaignEncounter refinement while preserving TankGame, Health, Projectile, AI and boss authority.
- [x] The next sector inherits the previous result through bounded reinforcement pressure plus a real victory logistics benefit using existing Orzełek repair and War Economy paths; run reset clears all campaign-memory state safely.
- [x] Packaged Windows war-state gate boots the exact development EXE and verifies 10-sector memory, momentum bounds, post-operation/next-sector/boss consequences, reset safety, integration and v8.4 version identity.

## v8.5 — Strategic Reserves, Reinforcement Pools & Campaign Attrition — COMPLETE
- [x] Enemy heavy armor, fire-support and electronic-warfare strength is represented by bounded strategic reserve pools tied to the existing enemy roster instead of a parallel spawn or damage authority.
- [x] Destroying real Heavy/Siege/Sniper/Elite units consumes the matching reserves; depleted pools materially reduce later endurance, strategic fire support and specialist/champion pressure while boss rounds remain bounded and playable.
- [x] Reserve exhaustion carries between sectors: prior remaining strength plus v8.4 Victory/Defeat state determines bounded replenishment, so successful operations create long-term attrition while enemy initiative enables partial recovery.
- [x] Packaged Windows strategic-reserves gate boots the exact development EXE and verifies ten-sector carryover, Victory/Neutral/Defeat ordering, class-to-reserve mapping, encounter bounds, runtime installation and v8.5 version identity.

## v8.6 — Logistics Network, Supply Depots & Strategic Interdiction — COMPLETE
- [x] Enemy strategic reserves gain physical battlefield logistics nodes (supply depot, mobile convoy and repair hub) with real Health/collision and deterministic non-boss scheduling, making v8.5 replenishment a playable target instead of an invisible number.
- [x] Destroying logistics nodes consumes matching Armor/Fire Support/EW reserves and builds bounded sector interdiction; surviving nodes restore a limited amount of the matching reserve, so battlefield outcomes directly control enemy replenishment.
- [x] Logistics integrity refines later CampaignEncounter pressure with bounded endurance/support/champion effects while preserving TankGame, Health, Projectile, boss and reserve authority; nodes remain readable through compact markers/briefs rather than permanent HUD clutter.
- [x] Packaged Windows logistics-network gate boots the exact development EXE and verifies node scheduling/types, destruction-vs-survival reserve ordering, interdiction bounds, encounter integration, runtime installation and v8.6 version identity.

## v8.7 — Supply Routes, Escort Doctrine & Counter-Interdiction Warfare — COMPLETE
- [x] Active enemy logistics nodes gain a bounded escort doctrine that assigns real existing combat units to defend depots, convoys and repair hubs through the existing tactical-navigation authority rather than spawning a parallel escort system.
- [x] Mobile supply convoys detect meaningful player pressure and execute deterministic emergency reroutes while repair teams can restore damaged surviving logistics nodes at a bounded cadence using authoritative Health healing.
- [x] Destroyed logistics nodes yield a limited captured-supplies benefit through existing player/Orzełek/economy paths, while surviving protected logistics strengthen enemy counter-interdiction without hidden damage or invulnerability.
- [x] Packaged Windows supply-routes gate boots the exact development EXE and verifies escort assignment, reroute/repair bounds, captured-supply rewards, runtime integration and v8.7 version identity.

## v8.8 — Battlefield Salvage, Field Resupply & Logistics Counteroffensive — COMPLETE
- [x] High-value destroyed enemy units and interdicted logistics create bounded battlefield salvage opportunities tied to real death/logistics outcomes instead of a parallel loot spawn economy.
- [x] Nearby salvage offers a player-readable FIELD vs STRATEGIC recovery choice: immediate repair/ammunition or War Bond/denial value through existing PlayerTank, Orzełek, ammo and economy authority.
- [x] Enemy counter-recovery teams retask real surviving combat units through TacticalNavigationAgent to reclaim unsecured salvage and restore a bounded matching strategic reserve, creating an active race over battlefield resources.
- [x] Packaged Windows salvage gate boots the exact development EXE and verifies salvage eligibility/budgets, recovery-choice rewards, enemy reclaim bounds, authoritative integration and v8.8 version identity.

## v8.9 — Forward Recovery Bases, Salvage Convoys & Frontline Control — COMPLETE
- [x] Strategic salvage can be committed to a bounded forward-recovery objective, creating a temporary frontline base only after a real delivery/hold sequence instead of instant conversion.
- [x] Forward Recovery Bases provide limited repair, reserve-matched ammunition and Orzełek support through existing Health, PlayerTank, ammo and economy authority, with hard per-round/cooldown budgets.
- [x] Enemy counteroffensive teams retask real surviving units through TacticalNavigationAgent to contest or destroy the recovery base while friendly salvage convoys reposition captured materiel without introducing a parallel combat roster.
- [x] Packaged Windows frontline-recovery gate boots the exact development EXE and verifies scheduling, delivery/hold/counterattack/reward bounds, runtime integration and v8.9 version identity.

## v9.0 — Dynamic Frontline, Territory Control & Multi-Objective Warfare — COMPLETE
- [x] Three persistent battlefield control lanes create a live friendly/contested/enemy frontline that reacts to real player presence, enemy presence and secured Forward Recovery Bases without replacing TankGame or movement authority.
- [x] Selected non-boss rounds run bounded multi-objective operations that combine territory capture, existing battlefield objectives and Orzełek/forward-base defense into one readable 2-of-3 combat decision instead of independent HUD tasks.
- [x] Territory ownership has real but bounded consequences for friendly recovery/logistics access, Orzełek support and enemy reinforcement/fire-support pressure through existing campaign, economy, Health and encounter authority.
- [x] Packaged Windows dynamic-frontline gate boots the exact development EXE and verifies 1–100 scheduling, control-state transitions, multi-objective scoring, consequence bounds, runtime integration and v9.0 version identity.

## v9.1 — Strongpoints, Field Fortifications & Territory Counteroffensives — COMPLETE
- [x] Friendly-controlled frontline lanes can be fortified into physical strongpoints with authoritative Health, bounded durability and readable field presentation instead of an abstract passive bonus.
- [x] The player can spend existing War Bonds to reinforce one selected lane with a bounded emplacement/support package, while enemy counteroffensives retask real surviving units through TacticalNavigationAgent to assault and destroy fortified positions.
- [x] Strongpoint control materially changes local frontline pressure, recovery/support access and counterattack behavior without replacing TankGame, Health, Projectile, DynamicFrontlineTerritoryDirector, WarEconomyDirector or tactical-navigation authority.
- [x] Packaged Windows strongpoint gate boots the exact development EXE and verifies fortification eligibility, bond spending, durability/repair bounds, enemy counteroffensive budgets, frontline integration and v9.1 version identity.

## v9.2 — Fortification Networks, Artillery Positions & Breakthrough Operations — COMPLETE
- [x] Multi-node fortification network connects strongpoints with artillery and repair positions across friendly frontline lanes using existing Health/frontline authority.
- [x] Artillery positions provide bounded real HE fire support while repair posts provide limited local recovery, with hard cadence and sustain caps.
- [x] Enemy breakthrough operations retask existing Heavy/Siege/Elite units through TacticalNavigationAgent to attack the weakest fortification node with bounded projectile pressure.
- [x] Packaged Windows v9.2 gate validates fortification-network scheduling, support/breakthrough safety bounds, runtime installation and exact EXE boot before qualification.

## v9.3 — Siege Lines, Counter-Battery Warfare & Breach Operations — COMPLETE
- [x] Enemy siege lines deploy bounded physical artillery batteries with authoritative Health/collision on selected non-boss rounds, creating destroyable battlefield objectives instead of invisible pressure multipliers.
- [x] Friendly artillery gains bounded counter-battery fire against live siege batteries while enemy batteries use authoritative HE projectiles to suppress fortified lanes and open temporary breach windows.
- [x] Breach operations retask existing Heavy/Siege/Elite units through TacticalNavigationAgent toward defended lanes, with strict actor/shot/cadence budgets and no parallel enemy roster or damage authority.
- [x] Packaged Windows v9.3 gate validates siege scheduling, battery/counter-battery/breach safety bounds, runtime installation and exact EXE boot before qualification.

## v9.4 — Siege Logistics, Fire-Control Recon & Mobile Batteries — COMPLETE
- [x] Eligible siege operations deploy one physical ammunition convoy with authoritative Health/collision, making battery sustain a destroyable battlefield objective rather than an invisible cadence modifier.
- [x] Existing live support-capable enemies provide bounded fire-control spotting; removing supply and/or the spotter materially reduces siege fire cadence through the existing v9.3 siege authority.
- [x] Counter-battery damage can force surviving batteries into bounded mobile displacement without healing, respawning or replacing authoritative Health/projectile/frontline state.
- [x] Packaged Windows v9.4 gate validates scheduling, supply/spotter/fire-control/relocation bounds, runtime installation and exact EXE boot before qualification.

## v9.5 — Fire Mission Networks, Decoy Batteries & Counter-Surveillance — COMPLETE
- [x] Existing SIGINT/recon becomes authoritative artillery targeting intelligence: true siege batteries require bounded observation/lock windows for reliable counter-battery fire instead of being permanently known targets.
- [x] Enemy siege doctrine deploys a hard-capped decoy-battery screen that can waste unguided counter-battery cycles until identified, while decoys remain non-damaging presentation/target-deception objects rather than a parallel combat roster.
- [x] Fire-control spotters gain bounded counter-surveillance/reacquisition behavior after reconnaissance exposure, while supply loss, spotter loss and existing battery relocation materially change targeting confidence without hidden damage or invulnerability.
- [x] Packaged Windows v9.5 gate validates 1–100 scheduling, lock/decoy/counter-surveillance safety bounds, v7.8 SIGINT/recon + v9.3/v9.4 integration, runtime installation and exact EXE boot before qualification.

## v10.0 — Combined Arms Campaign Command & 100-Round War Reforge — COMPLETE
- [x] Campaign-command layer orchestrates deterministic multi-stage operations across the 100-round war, sequencing reconnaissance, interdiction and decisive-action beats around existing frontline, logistics, SIGINT and fortification authorities instead of adding disconnected round modifiers.
- [x] Selected non-boss command operations create a physical destroyable command relay plus bounded specialist response waves using authoritative Health, collision, TankGame spawning and the existing enemy roster; success or failure carries bounded command momentum into later operations.
- [x] Command momentum materially changes later operation pressure, friendly support availability and War Bond payoff while preserving boss rounds, TankGame round authority, Projectile damage, existing campaign memory and strategic reserves; the player-facing command brief remains compact and readable.
- [x] Packaged Windows v10.0 campaign-command gate validates 1–100 scheduling, phase, momentum, pressure and reward safety bounds, required v8.x/v9.x integration services, runtime installation and exact EXE boot before qualification.

## v10.1 — Theater Orders, Operation Branching & Campaign Consequences — COMPLETE
- [x] Strategic command windows let the player choose ASSAULT, INTERDICTION or FORTIFY orders at bounded campaign checkpoints; every order persists for several subsequent rounds and changes live battlefield priorities instead of acting as a cosmetic menu choice.
- [x] ASSAULT commits bounded real projectile fire support against priority combat threats, INTERDICTION redirects fire missions toward enemy command/logistics infrastructure, and FORTIFY creates bounded Orzełek/player sustain and frontline defense benefits through existing Health, TankGame and war-economy authority.
- [x] Theater orders integrate with v10.0 campaign-command operations so active doctrine changes decisive support, command-relay pressure and post-operation payoff while preserving boss rounds, existing AI/spawn authority and strict late-round pressure caps.
- [x] Packaged Windows v10.1 theater-orders gate validates decision scheduling, order persistence/effect bounds, v10.0 integration, runtime installation and exact EXE boot before qualification.

## v10.2 — Theater Consequence Engine, Sector Doctrines & Branching War State — COMPLETE
- [x] Theater Consequence Engine records a bounded doctrine outcome for each 10-round sector from real v10.1 orders plus existing campaign-memory/command state, then carries that consequence into the following sector instead of discarding it after the short order window.
- [x] ASSAULT, INTERDICTION and FORTIFY branch into distinct next-sector gameplay: bounded breakthrough fire support, supply-starvation infrastructure pressure, or prepared-defense sustain through existing TankGame, Projectile, Health and War Bond authority.
- [x] Sector doctrine history and theater initiative remain hard-capped, boss-safe and player-readable, with outcomes integrated with v8.4 campaign memory and v10.0 command momentum so success/failure changes later pressure without runaway snowballing.
- [x] Packaged Windows v10.2 consequence-engine gate validates all ten sectors, branch transitions/caps, v8.4/v10.0/v10.1 integration, runtime installation and exact EXE boot before qualification.

## v10.3 — Adaptive Enemy High Command, Counter-Doctrines & Sector War Plans — COMPLETE
- [x] Adaptive Enemy High Command reads the rolling v10.2 sector-doctrine history and resolves a bounded enemy counter-doctrine for later sector rounds, with deterministic history weighting, boss safety and no replacement of existing campaign authority.
- [x] ARMOR TRAP, DISPERSED LOGISTICS and SIEGE BREACH create materially different counterplay by issuing temporary orders to existing TacticalNavigationAgent units, using bounded AP/HE counter-fire and limited existing-Health logistics sustain rather than parallel movement, damage or economy systems.
- [x] Sector war plans are hard-capped and player-readable: at most four retasked combatants and two counter-fire shells per response beat, active only in the mid/late sector window so v10.2 consequences and boss rounds retain their own authority.
- [x] Packaged Windows v10.3 high-command gate validates doctrine mapping/history weighting, round windows/caps, v10.2 and tactical-navigation integration, runtime installation and exact EXE boot before qualification.

## v10.4 — High Command Reserves, Feint Operations & Counter-Intelligence War — COMPLETE
- [x] High Command converts the v10.3 counter-doctrine into a bounded two-axis operation with one real main effort and one feint across the existing three-lane Dynamic Frontline, keeping boss/end-sector rounds and existing campaign authority isolated.
- [x] Strategic reserve commitment is gameplay-real and attritional: existing Armor/Fire Support/EW reserve snapshots gate the strength of Heavy/Siege/Elite commitments, while all movement, damage and reserve loss continue through existing TacticalNavigationAgent, Projectile/Health and StrategicReserveAttrition systems.
- [x] Existing G SIGINT / H Recon from Command Network Hunt can identify the real axis during a short intelligence window; successful identification degrades the coming main effort, while no intelligence leaves the feint credible and the bounded assault at full strength.
- [x] Packaged Windows v10.4 deception-war gate validates axis selection, reserve/counter-intelligence caps, v10.3/v9.0/v8.5/v7.8 integration, runtime installation and exact EXE boot before qualification.


## v10.5 — Player Counter-Orders, Reserve Traps & Operational Intelligence — COMPLETE
- [x] Confirmed v10.4 MAIN/FEINT intelligence opens one bounded player counter-order window with BLOCK, COUNTERATTACK and DEEP STRIKE choices instead of resolving the reveal automatically.
- [x] Counter-orders create materially different live battlefield consequences through existing Health, TankGame projectile, frontline and enemy-roster authority: main-axis defense, feint-axis reserve trap or high-value deep interdiction.
- [x] Operational intelligence remains boss-safe, hard-capped and player-readable; each deception operation accepts at most one order and all support fire/recovery/reward effects have explicit budgets and cooldowns.
- [x] Packaged Windows v10.5 counter-orders gate validates scheduling, choice/effect caps, v10.4/v7.8/v9.0 integration, runtime installation and exact EXE boot before qualification.


## v10.6 — Counter-Offensive Campaigns, Captured Intelligence & Command Collapse — COMPLETE
- [x] Counter-order execution is assessed against live battlefield results, producing bounded SUCCESS, STALEMATE or FAILURE outcomes instead of ending after scripted response beats.
- [x] Successful BLOCK, COUNTERATTACK and DEEP STRIKE outcomes seed distinct next-sector counter-offensive plans with real combat support, captured-intelligence effects and persistent but capped operational momentum.
- [x] Failed counter-orders can trigger bounded High Command evacuation/recovery pressure, while captured intelligence and command-collapse state remain integrated with existing enemy roster, Health, frontline, War Bonds and projectile authority.
- [x] Packaged Windows v10.6 counter-offensive gate validates outcome assessment, next-sector carryover, strict caps, v10.5/v10.4 integration, runtime installation and exact EXE boot before qualification.

## v10.7 — War State Director, Dynamic Operation Chains & Campaign Endgame Reforge — COMPLETE
- [x] A run-level War State Director converts v10.6 outcome, operational momentum and captured intelligence into bounded ADVANTAGE / CONTESTED / CRISIS campaign states without replacing existing combat authority.
- [x] Dynamic operation chains turn rounds 90–99 into a branching endgame arc whose objectives and support pressure depend on the live war state instead of a fixed late-game sequence.
- [x] The final sector gains materially different ENDGAME plans — COMMAND COLLAPSE, BREAKTHROUGH PURSUIT or DESPERATE DEFENSE — with authoritative projectile, Health, frontline, Orzełek and War Bond consequences under strict caps.
- [x] Round 100 receives a boss-safe final-war modifier and player-readable campaign outcome derived from the accumulated war state, while all existing boss/AI/damage authority remains intact.
- [x] Packaged Windows v10.7 endgame gate validates state resolution, rounds 90–100 branch scheduling, strict support/recovery caps, v10.6 integration, runtime installation and exact EXE boot before qualification.


## v10.8 — High Command HQ, Final Objectives & Campaign Epilogues — COMPLETE
- [x] The rounds 97–100 endgame gains a physical, destructible Enemy High Command HQ using authoritative Health/collision and lane placement instead of a text-only final-war modifier.
- [x] ADVANTAGE / CONTESTED / CRISIS produce materially different final objectives — HQ assault, command isolation or evacuation denial — with strict projectile, reinforcement and recovery caps.
- [x] HQ state is integrated with the existing v10.7 War State/Endgame plan and round-100 boss authority, so destroying, isolating or failing to stop High Command changes the campaign outcome without replacing the boss or core combat systems.
- [x] A player-readable campaign epilogue summarizes the real final objective result and accumulated war state after the final battle, including bounded War Bond reward consequences.
- [x] Packaged Windows v10.8 High Command HQ gate validates physical-HQ configuration, state/objective mapping, boss-safe round-100 integration, epilogue outcomes, strict caps, runtime installation and exact EXE boot before qualification.


## v11.0 — Tactical Combat Reforge, Cover, Suppression & Formation AI — COMPLETE
- [x] Existing squad/navigation/terrain systems are unified into bounded combat platoons with a real leader, class-aware assault/flank/suppress/break roles and coordinated objectives instead of independent per-tank behavior.
- [x] Real incoming damage drives a suppression model with decay, under-fire relocation and cover-aware regroup orders through TacticalNavigationAgent/TerrainIntelligence rather than a second movement AI.
- [x] Platoons react to leader loss with a short, bounded cohesion break, successor election and regroup/reformation behavior while respecting High Command directives and boss authority.
- [x] Tactical combat state becomes readable through lightweight world/HUD cues and fixed-cadence/capped processing so the reforge remains usable during late-round mass battles.
- [x] Packaged Windows v11.0 Tactical Combat Reforge gate validates role/platoon limits, suppression/decay, leader-loss recovery, directive arbitration, runtime installation and exact EXE boot before qualification.


## v11.1 — Armor Facings, Component Damage & Mobility Kills Reforge — COMPLETE
- [x] Directional armor is deepened into explicit front/side/rear tactical exposure with ammo-aware penetration, overmatch/ricochet behavior and readable impact outcomes while preserving ArmorSystem/Health authority.
- [x] Engine, tracks, gun and ammo-rack damage gain staged operational states including mobility kill, weapon impairment and catastrophic ammo-rack pressure, with bounded repair/recovery and no parallel HP model.
- [x] v11.0 platoons react to crippled members: damaged leaders trigger protection/reformation, mobile escorts screen mobility-killed Heavy/Siege units and flank-capable units exploit exposed armor through existing TacticalNavigationAgent authority.
- [x] Player/enemy combat presentation exposes concise armor-zone and module-state feedback while fixed cadences, strict actor caps and boss-safe rules preserve late-round performance and authority.
- [x] Packaged Windows v11.1 Armor & Component Damage gate validates facing/ammo mapping, module-state thresholds, mobility/weapon kill bounds, tactical integration, runtime installation and exact EXE boot before qualification.


## v11.2 — Fire Control, Stabilization & Ballistics Reforge — COMPLETE
- [x] Shared fire-control ballistics adds bounded movement/module stabilization, ammo-aware precision identity and predictive lead while preserving Projectile, Health and ArmorSystem authority.
- [x] Enemy gunnery consumes predictive lead and class-aware dispersion so Sniper/Elite/Heavy precision behavior materially differs from baseline enemies without hidden damage or extra projectiles.
- [x] Precision platoons gain bounded coordinated volley timing from round 35 with a hard 0.72 s hold cap, existing reload authority and no additional shots.
- [x] Player-facing Fire Control HUD exposes live LOCKED / STABILIZING / UNSTABLE state from real movement and weapon-module condition without adding permanent debug clutter.
- [x] Packaged Windows v11.2 Fire Control Ballistics gate validates campaign bootstrap, PlayerTank/ArmorSystem integration, predictive ballistics, HUD installation, volley eligibility/safety bounds and exact EXE boot before qualification.


## v11.3 — Advanced Gunnery Doctrine & Counter-Fire Warfare — QUALIFIED
- [x] Doctrine-aware gunnery maps live High Command strategy into bounded Hunter-Killer, Counter-Fire and Eagle-Breach targeting without replacing EnemyTank authority.
- [x] Incoming-fire memory makes Heavy/Siege precision elements react to real player damage events with bounded retaliation windows and no hidden damage path.
- [x] Counter-fire retaliation and doctrine-aware precision volleys materially alter target selection, aim quality and cadence while preserving reload/projectile authority.
- [x] v11.3 runtime smoke validates doctrine selection, incoming-fire memory expiry, class eligibility and all safety caps in the packaged Windows EXE.
- [x] Windows x64 qualification gate passes exact-candidate build, package and fresh-runner EXE smoke before v11.3 is marked complete.

## v11.4 — Dynamic Fire Missions & Platoon Target Assignment — QUALIFIED
- [x] Platoon target assignment coordinates Heavy/Sniper/Siege/Elite fire priorities through existing EnemyTank and v11.0 platoon authority without adding a second firing path.
- [x] Overkill control caps simultaneous precision commitments to one target and redistributes eligible gunners when another live strategic target exists.
- [x] Dynamic fire-mission doctrine links v9.5 Fire Mission Network intelligence, v11.3 counter-fire memory and High Command doctrine into bounded target scoring and reassignment.
- [x] Target reservations expire quickly, are actor-capped and boss-safe so late-round mass battles retain deterministic performance and existing projectile/reload authority.
- [x] Packaged Windows v11.4 gate validates assignment budgets, overkill redistribution, doctrine/fire-mission integration, runtime installation and exact EXE boot before qualification.

## v11.5 — Platoon Roles, Target Handoffs & Coordinated Assaults — QUALIFIED
- [x] Dynamic platoon roles assign Commander, Hunter, FireSupport and Breacher duties to Heavy/Sniper/Siege/Elite without replacing EnemyTank firing authority.
- [x] Target handoff releases stale reservations immediately on actor disable, target loss or strategic priority change so surviving units can reassign without waiting for reservation expiry.
- [x] Coordinated assault phases combine Heavy breach pressure, Sniper/Elite hunter screening and Siege fire support using existing High Command, Counter-Fire and Fire Mission Network intelligence.
- [x] Assault coordination remains bounded, boss-safe and deterministic under late-round mass battles, with capped platoon state and no additional projectile/damage path.
- [x] Packaged Windows v11.5 gate validates role assignment, target handoff, coordinated assault integration and exact EXE boot before qualification.

## v11.6 — Adaptive Platoon Maneuver & Battlefield Encirclement — QUALIFIED
- [x] Role-aware maneuver orders turn Commander/Hunter/FireSupport/Breacher assignments into bounded movement intent without replacing EnemyTank Rigidbody2D authority.
- [x] Hunter/Elite flank lanes and crossfire spacing react to player position while Heavy/Breacher units maintain frontal pressure and avoid deterministic stacking.
- [x] Siege FireSupport maintains bounded standoff bands and repositions when threatened by real Counter-Fire instead of camping indefinitely.
- [x] Commander casualty adaptation reorganizes surviving specialist roles and maneuver pressure after actor loss with capped deterministic platoon state.
- [x] Packaged Windows v11.6 gate validates maneuver bounds, encirclement integration, late-round safety and exact EXE boot before qualification.

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