# v8.0.0-rc1 — Public Demo 2 Release Candidate & Full Campaign Integration

## Major integration milestone

- Adds a compact full-campaign integration layer that introduces advanced counterplay progressively instead of front-loading more tutorial text.
- Round 12 introduces smoke/ECM/decoy controls (`R/C/V`).
- Round 20 explains electronic-warfare counterplay and EMP disruption.
- Round 28 introduces SIGINT/recon (`G/H`) and relay hunting.
- Round 36 introduces the Mobile HQ assault loop.

## Demo 2 RC packaging

- Current non-development identity: `v8.0.0-rc1`.
- BUILD_INFO and README_DEMO now include the advanced tactical controls and late-campaign EW/Mobile-HQ objectives.
- DEMO_MANIFEST records exact commit identity and the integrated 36/50/80/90/100 qualification profile.
- Candidate workflow creates a Windows x64 ZIP and SHA-256 checksum.

## Release qualification

- Full-stack packaged runtime smoke verifies frontend, squad tactics, tactical navigation, terrain intelligence, adaptive fire control, counterplay, EW command, command-network hunt, Mobile HQ and the v8.0 integration layer.
- Integrated packaged soak drives the same candidate through rounds 36/50/80/90/100 under Heavy/Siege/Sniper/Elite pressure.
- Projectile-pool integrity, runtime stability and blocking exception signatures remain hard release gates.

This release candidate must remain off `main` until both standard Dev Windows Build and the dedicated Demo 2 v8 Candidate Windows workflow are green.
