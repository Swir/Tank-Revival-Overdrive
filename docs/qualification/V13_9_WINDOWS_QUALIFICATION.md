# v13.9 Windows Qualification Evidence

Status: **QUALIFIED**

This document records the exact-candidate evidence used to close the v13.9 Suppression Counterplay & Tactical Fire-Support Command roadmap scope. Milestone qualification is not the same as public GitHub Release readiness.

| Field | Verified value |
|---|---|
| Candidate commit | `30511fb9452dfb47e481ecb57f87ccd51aded1fa` |
| Qualification workflow | `Fire Support v13.9 Windows Qualification Gate` |
| Qualification run | `35462501123` — SUCCESS |
| Source qualification run | `35462501080` — SUCCESS |
| C# type uniqueness run | `35462501077` — SUCCESS |
| Unity | `6000.3.17f1` |
| Platform | Windows x64 |
| Candidate artifact | `TankRevivalOverdrive-v13.9-fire-support-Windows-x64` |
| Candidate artifact ID | `10590695610` |
| Candidate artifact bytes | `60268470` |
| GitHub artifact digest | `sha256:7af94e0ef1032ccd7309740da061cd3e1351dde3e4fe06a139ab05a733db872d` |
| Finalizer run | `35465352256` |

## Same-executable qualification matrix

The exact packaged candidate passed the production authored-audio presentation preflight, v13.9 fire-support runtime smoke, v13.8 suppression/morale regression, v13.7 late-round-performance regression, v13.6 sensor-fusion regression and the rounds 80/90/100 soak regression on the same executable.

v13.9 remains an intent-and-counterplay layer. The director does not own projectile instantiation or Health damage; the execution bridge forwards qualified strike intent through canonical `TankGame.SpawnProjectile`, while enemy movement/fire and survivability remain under their existing authorities.
