# v14.0 Windows Qualification Evidence

- Milestone: **v14.0 — Counter-Battery & Mobile Fire-Control Warfare**
- Candidate SHA: `77213238077e999ed9b880c6e53bad0b0b5b1dfa`
- Source qualification run: `35482246038` — SUCCESS
- C# type-uniqueness run: `35482246036` — SUCCESS
- Windows qualification run: `35482246072` — SUCCESS
- Unity: `6000.3.17f1`
- Target: Windows x64
- Artifact: `TankRevivalOverdrive-v14.0-counter-battery-Windows-x64`
- Artifact ID: `10596580538`
- Artifact bytes: `60278167`
- Artifact digest: `sha256:2be0f4b745ed85f78be2869cdb56b63ecab05f3213f15754d0675abdbfe31c5d`

## Same-executable qualification matrix

The exact packaged candidate passed production authored-audio presentation preflight, the v14.0 counter-battery runtime/authority smoke, v13.9 fire-support regression, v13.8 suppression/morale regression, v13.7 late-round-performance regression, v13.6 sensor-fusion regression, and the rounds 80/90/100 soak on the same executable.

## Authority and release boundary

The v14.0 layer keeps enemy barrage execution behind the separate execution bridge into canonical `TankGame.SpawnProjectile`; it does not add direct `Health` damage, parallel movement authority, scene-scan hot paths or a second spawn authority. Roadmap qualification does not itself publish a public Release or merge this development branch to `main`.
