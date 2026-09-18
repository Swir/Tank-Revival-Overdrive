# v13.7 Windows Qualification Evidence

Status: **QUALIFIED**

This document records the exact-candidate evidence used to close the v13.7 Late-Round Performance & Battle Density Reforge roadmap scope. Milestone qualification is not the same as public GitHub Release readiness.

| Field | Verified value |
|---|---|
| Candidate commit | `dc646fc60be572e625f5ea310058aab10d2f26da` |
| Qualification workflow | `Late-Round Performance v13.7 Windows Qualification Gate` |
| Qualification run | `35393272731` — SUCCESS |
| Unity | `6000.3.17f1` |
| Platform | Windows x64 |
| Candidate ZIP SHA-256 | `0328a6613dcc64dffb1f675282e27a798b0f3abcb561e13423b7b6de3d75c25f` |
| Candidate ZIP bytes | `60548408` |
| GitHub candidate artifact ID | `10566756672` |
| GitHub artifact digest | `sha256:e4b063898c0e516fda4064d183080a4219861c593e572153b327bd857f8b05c4` |
| Finalizer run | `35394445864` |

## Same-executable qualification matrix

The exact packaged candidate passed, in order, the production authored-audio presentation preflight, v13.7 late-round performance smoke, v13.6 sensor-fusion regression, v13.5 battlefield-weather regression, v13.4 tactical-terrain regression, v13.3 battlefield-cohesion regression and the rounds 80/90/100 soak regression.

The v13.7 performance layer changes optional presentation budgets only. The qualification does not authorize hidden enemy deletion, spawn-count reduction, alternate movement/targeting/damage authorities or suppression of gameplay events.
