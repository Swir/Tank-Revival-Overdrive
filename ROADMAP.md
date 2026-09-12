# Tank Revival: Orzeł Overdrive — Roadmap

<!-- SWIR-ROADMAP-STANDARD:v1 -->
<!-- ROADMAP-PROGRESS:START -->
<p align="center">
  <a href="https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/dev-windows-build.yml"><img alt="CI" src="https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/dev-windows-build.yml/badge.svg?branch=dev-v5-5"></a>
  <img alt="Roadmap progress" src="https://img.shields.io/badge/ROADMAP-100%25-2ea043?style=for-the-badge">
  <img alt="Completed" src="https://img.shields.io/badge/DONE-44%2F44-1f6feb?style=for-the-badge">
  <img alt="Status" src="https://img.shields.io/badge/STATUS-V5.5%20COMPLETE-2ea043?style=for-the-badge">
</p>

## 📊 Overall progress

```text
████████████████████ 100.0%
```

| ✅ Completed | ⏳ Remaining | 📦 Total | 🎯 Progress |
|---:|---:|---:|---:|
| **44** | **0** | **44** | **100.0%** |

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
