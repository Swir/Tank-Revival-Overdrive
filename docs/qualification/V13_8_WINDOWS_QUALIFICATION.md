# v13.8 Windows Qualification Evidence

Status: **QUALIFIED**

This document records the exact-candidate evidence used to close the v13.8 Battlefield Suppression & Morale Warfare roadmap scope. Milestone qualification is not the same as public GitHub Release readiness.

| Field | Verified value |
|---|---|
| Candidate commit | `0805a27a7a69a0d620e96f64e6cac66185be6547` |
| Qualification workflow | `Battlefield Suppression v13.8 Windows Qualification Gate` |
| Qualification run | `35413636894` — SUCCESS |
| Source qualification run | `35394513424` — SUCCESS |
| C# type uniqueness run | `35394513425` — SUCCESS |
| Unity | `6000.3.17f1` |
| Platform | Windows x64 |
| Candidate artifact | `TankRevivalOverdrive-v13.8-suppression-morale-Windows-x64` |
| Candidate artifact ID | `10575925003` |
| Candidate artifact bytes | `60261926` |
| GitHub artifact digest | `sha256:4f6e98ee70dfb6d888bff7aba70075fd26977006b5833967da82dbc99b9ce6fa` |
| Finalizer run | `35424987413` |

## Same-executable qualification matrix

The exact packaged candidate passed the production authored-audio presentation preflight, v13.8 suppression/morale smoke, v13.7 late-round-performance regression, v13.6 sensor-fusion regression, v13.5 battlefield-weather regression, v13.4 tactical-terrain regression, v13.3 battlefield-cohesion regression and the rounds 80/90/100 soak regression.

The v13.8 layer changes bounded suppression/morale intent only. Qualification does not authorize bonus damage, hidden Health mutation, alternate projectile/spawn/movement authority, permanent crowd control, enemy deletion, reduced enemy counts or suppression of gameplay events.
