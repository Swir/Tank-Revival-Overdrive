<!-- SWIR-README-STANDARD:v2 -->

<div align="center">

# TANK REVIVAL: ORZEŁ OVERDRIVE

**A 2.5D top-down tank-defense action game built in Unity 6 for Windows.**

Defend the **Orzełek stronghold** through a 100-round campaign of armored assaults, specialist formations, supply warfare, electronic operations and multi-phase boss battles.

[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011%20x64-02050A?style=for-the-badge&logo=windows11&logoColor=62E5FF)](https://github.com/Swir/Tank-Revival-Overdrive/releases)
[![Unity](https://img.shields.io/badge/Unity-6000.3.17f1-02050A?style=for-the-badge&logo=unity&logoColor=62E5FF)](https://unity.com/)
[![Roadmap](https://img.shields.io/badge/Roadmap-100.0%25%20V13.2%20Qualified-02050A?style=for-the-badge&logo=github&logoColor=62E5FF)](ROADMAP.md)
[![v13.0 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/encounter-warfare-v130-windows.yml/badge.svg?branch=dev-v13-0)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/encounter-warfare-v130-windows.yml)

[**Highlights**](#-highlights) · [**Download**](#-quick-start--download) · [**Controls**](#-controls) · [**Roadmap**](#-roadmap--quality-gates) · [**Releases**](#-releases)

</div>


<!-- SWIR-PROGRESS-SVG-PRO:v1 -->
<p align="center"><img src="assets/readme/progress-card.svg" alt="SWIR project roadmap progress" width="760"></p>
<p align="center"><sub>Roadmap progress: 407 / 407 completed (100.0%) — V13.2 QUALIFIED. Release readiness is tracked separately by Windows qualification gates.</sub></p>

---

## 📌 Project Status

| Item | Status |
|---|---|
| Development milestone | **V13.2 QUALIFIED** on `dev-v13-2` |
| Roadmap | **407 / 407 completed (100.0%)** — authoritative `ROADMAP.md` scope |
| Primary platform | **Windows 10 / 11 x64** |
| Development engine | **Unity 6000.3.17f1** |
| Latest public demo | **v6.3.0-demo** — prerelease |
| Latest stable release | **v2.2.0** |
| Main branch | Kept separate from active milestone development until integration is intentionally performed |

`ROADMAP.md` is the authoritative source for milestone completion. A checkbox is completed only after the corresponding implementation exists and its required verification has passed.

## 🎮 What is Tank Revival?

Tank Revival: Orzeł Overdrive is an original top-down armored action game focused on defending the Orzełek stronghold while the battlefield becomes increasingly complex across 100 rounds.

The campaign combines direct tank combat with directional armor, component damage, special ammunition, field repair, tactical enemy roles, supply and route pressure, electronic warfare, fire-support intelligence and deterministic encounter escalation. Every tenth round becomes a boss assault, while late-game encounter pressure is kept inside explicit runtime and CI budgets instead of relying on uncontrolled spawn growth.

## ⚡ Highlights

| Feature | What it changes in play |
|---|---|
| 🦅 **Orzełek defense** | Protect a persistent stronghold whose defenses and battlefield pressure escalate through the campaign. |
| 💯 **100-round encounter campaign** | Deterministic encounter plans, five campaign acts and eight doctrine families create controlled escalation from round 1 to 100. |
| 👑 **Multi-phase bosses** | Boss behavior escalates through health/component-driven phases while the existing movement, projectile and survivability authorities remain canonical. |
| 🛡️ **Directional armor & modules** | Front/side/rear armor, ricochets and engine/tracks/gun/ammo-rack degradation affect how vehicles move and fight. |
| 🔧 **Emergency repair warfare** | Limited, interruptible field repair can recover damaged modules without healing vehicle HP. |
| 💥 **7 ammunition modes** | Standard, AP, HE, Incendiary, EMP, Twin Shot and Plasma provide distinct anti-armor, area, disruption and penetration roles. |
| 🚜 **8 enemy classes** | Basic, Fast, Heavy, Sniper, Siege, Elite, Supply and Boss units create different target and positioning priorities. |
| 📡 **Operational warfare stack** | Combined Arms, sustainment, route intelligence, Recon/EW, Mobile Signal and SIGINT feed bounded read-only encounter pressure/relief decisions. |
| 🎯 **Independent hull and turret control** | Drive, aim and fire independently instead of locking the cannon to chassis direction. |
| ✨ **Procedural 2.5D presentation** | Animated tracks, recoil, muzzle flashes, trails, smoke, sparks, explosions, tactical telegraphs and pooled combat feedback. |
| 🧪 **Exact-candidate Windows qualification** | CI builds one Windows executable, verifies provenance, runs v13.0 smoke plus v12.9/v12.8 regressions and late-round 80/90/100 soak on that same binary. |

## 🔫 Ammunition

| Key | Ammunition | Battlefield role | First available |
|---:|---|---|---:|
| 1 | Standard | Unlimited general-purpose shell | Round 1 |
| 2 | AP Piercing | Faster anti-armor shot with stronger penetration and reduced ricochet risk | Round 3 |
| 3 | HE Explosive | Area pressure against enemies and destructible brick cover | Round 8 |
| 4 | Incendiary | Damage-over-time pressure and rear/engine threat | Round 15 |
| 5 | EMP Shock | Temporary movement/weapon disruption with strong subsystem pressure | Round 25 |
| 6 | Twin Shot | Two parallel projectiles for wider close/mid-range pressure | Round 35 |
| 7 | Plasma | Fast, high-damage, multi-penetrating anti-armor projectile | Round 50 |

Use **Q / E** to cycle through ammunition currently in inventory. Supply Tanks can provide additional ammunition during the campaign.

## 🚀 Quick Start / Download

### Recommended — packaged Windows build

1. Open the repository **Releases** page.
2. Download a Windows x64 ZIP from the release you want to run.
3. Extract the **entire** archive to its own folder.
4. Run `TankRevivalOverdrive.exe`.
5. Keep `TankRevivalOverdrive_Data` beside the executable.

Unity is **not required** to play a packaged Windows build.

- Public demo: [`v6.3.0-demo`](https://github.com/Swir/Tank-Revival-Overdrive/releases/tag/v6.3.0-demo)
- Stable release: [`v2.2.0`](https://github.com/Swir/Tank-Revival-Overdrive/releases/tag/v2.2.0)
- All releases: https://github.com/Swir/Tank-Revival-Overdrive/releases

### From source

Clone the repository and open the Unity project with the editor version used by current CI:

```bash
git clone https://github.com/Swir/Tank-Revival-Overdrive.git
cd Tank-Revival-Overdrive
```

Active milestone work lives on dedicated `dev-vX-Y` / `dev-vX.Y` branches. For reproducible development builds, match the Unity version declared by the branch CI instead of assuming the latest editor is compatible.

## 💻 Requirements / Compatibility

| Requirement | Current project target |
|---|---|
| OS | Windows 10 / 11 x64 |
| Packaged game | Portable ZIP containing EXE + `_Data` runtime |
| Unity required to play | No |
| Source development | Unity 6000.3.17f1 for the current v13.0 CI path |
| Input | Keyboard + mouse |

Other platforms are not advertised as supported unless a dedicated verified build exists.

## 🎮 Controls

| Input | Action |
|---|---|
| **WASD / Arrow keys** | Move and turn the hull |
| **Mouse** | Aim the turret independently |
| **Left Mouse / Space / Left Ctrl** | Fire |
| **Q / E** | Previous / next available ammunition |
| **1–7** | Directly select ammunition type |
| **R** | Attempt emergency field repair when the repair system permits it |
| **P / Escape** | Pause / resume |
| **Enter / Space** | Start/restart and deploy from the Command Center where applicable |
| **1–6 in Command Center** | Purchase the corresponding permanent upgrade |
| **7 in Command Center** | Repair Orzełek when that command action is available |

## 🧠 Combat & Campaign Systems

### Armor and component warfare

Vehicle survivability stays under the canonical `Health` authority, while `ArmorSystem` resolves directional protection and progressive module degradation. Engine, tracks, gun and ammunition-rack state can affect mobility, reload, weapon function and AI casualty behavior without creating a second HP model.

### Encounter Director v13.0

`EncounterPlannerV130` deterministically plans all 100 rounds with explicit enemy-count, concurrency and spawn-cadence limits. The live campaign consumes those plans through the existing `TankGame` round/spawn authority.

The v13.0 cross-system doctrine layer reads bounded public state from Mobile Front, Operational Sustainment, Route Intelligence, Recon/EW, Mobile Signal and SIGINT. It can adjust live pressure only inside small hard limits; it does not take control of those systems, movement, damage, projectiles or rewards.

### Orzełek escalation

Late-campaign defense can strengthen the flanking shoulders while preserving a destructible center approach. This keeps a visible breach route for standard loadouts instead of turning high-tier defense into an inaccessible objective wall.

## 🧱 Technology & Authority Model

| Area | Implementation |
|---|---|
| Engine | Unity 6 / C# |
| Rendering | Procedural 2.5D gameplay presentation |
| Player/enemy survivability | Canonical `Health` |
| Projectile collision/damage flow | Canonical `Projectile` / existing spawn path |
| Round and spawn ownership | `TankGame` |
| Enemy movement/weapon ownership | Existing `EnemyTank` / Rigidbody2D combat path |
| Tactical/operational layers | Bounded directors and read-only integration snapshots |
| Windows CI | GitHub Actions + Unity builder + deterministic packaging/provenance checks |
| Regression strategy | Packaged-EXE smoke plus late-round 80/90/100 soak |

The project intentionally avoids parallel damage, movement and economy authorities when new tactical layers are introduced.

## 🗺️ Roadmap & Quality Gates

The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **407 / 407 (100.0%) — V13.2 QUALIFIED**. Release readiness is tracked separately by exact-candidate Windows qualification gates.

Recent qualified milestone layers include full-stack v12.8 integration, v12.9 component damage/emergency repair warfare and v13.0 100-round encounter/boss phase warfare.

Qualification is not based on version numbers or commit count. The v13.0 gate requires source/authority contracts, a Unity Windows x64 build, deterministic packaging and one exact packaged EXE running:

- v13.0 encounter/cross-stack/boss smoke;
- v12.9 component/repair/casualty/presentation regression;
- v12.8 full-stack integration regression;
- late-round soak covering rounds 80 / 90 / 100.

## 📦 Releases

The development branch being qualified does **not** automatically replace the public release line.

- **Latest public demo:** `v6.3.0-demo` (prerelease)
- **Latest stable release:** `v2.2.0`
- **Development milestone:** v13.0 is qualified on its development branch but has not been published here as a new public v13.0 release.

This separation keeps public downloads distinct from experimental or not-yet-integrated milestone work.

## 🎨 Creative Direction

Tank Revival: Orzeł Overdrive is an original project with its own code and procedural game presentation. It uses broad top-down tank-action conventions but does not depend on ripped maps, sprites, audio or other assets from existing games.

## 🔎 Search Keywords

`tank defense game` • `Windows tank game` • `2.5D tank action` • `top down tank game` • `Unity 6 game` • `C# Unity game` • `100 round campaign` • `boss tank battles` • `tank armor simulation` • `component damage system` • `special ammunition game` • `Orzelek defense` • `procedural combat effects` • `Windows x64 game` • `GitHub Actions Unity build`

---

<div align="center">

### `DEFEND • ADAPT • OVERDRIVE`

Built and maintained by **SWIR**.

⭐ **If Tank Revival is useful or fun to follow, consider leaving a star.**

[**← SWIR profile**](https://github.com/Swir) · [**All projects →**](https://github.com/Swir?tab=repositories)

</div>
