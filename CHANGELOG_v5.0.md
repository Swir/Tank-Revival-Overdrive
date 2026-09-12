# Tank Revival: Orzeł Overdrive — v5.0.0-rc3

## PUBLIC DEMO CANDIDATE — PACKAGED 100-ROUND QUALIFICATION

RC3 upgrades the release gate again: the exact packaged Windows candidate must not only boot, but survive automated late-round combat qualification using the real standalone executable.

### Added
- `DemoCISoakProbe`, dormant for normal players and activated only with `-demo-ci-soak`.
- Automated packaged-runtime stages for rounds 80, 90 and 100.
- Heavy pressure injection at every stage using existing `TankGame.SpawnEnemy` authority and Heavy/Siege/Sniper/Elite enemy classes.
- Authoritative campaign-continuity checks while each late-round stage is active.
- Projectile-pool integrity validation after every stage.
- Runtime Stability Director health validation plus zero-new-warning, repair and pool-fault release criteria.
- Diagnostic capture for minimum observed FPS, maximum managed GC memory, projectile creation/reuse counts and complete `Player.log`.
- Separate Windows late-round qualification job that downloads the exact SHA-verified demo ZIP and launches `TankRevivalOverdrive.exe` with the soak probe.

### Existing RC gates retained
- Dedicated non-development Windows x64 build entry point: `CIBuild.BuildDemoCandidate`.
- Separate GitHub Actions workflow for candidate validation and packaging.
- Runtime package validation for EXE, Unity data directory, build information, demo README and candidate manifest.
- Exact GitHub commit embedded into `DEMO_MANIFEST.txt`.
- SHA-256 checksum generated alongside the final Windows ZIP.
- `DemoCISmokeProbe` packaged-EXE boot validation on a fresh Windows runner.
- Blocking crash/exception scan in captured runtime logs.

### Packaging
Expected artifact name:

`TankRevival-Orzel-Overdrive-DEMO-v5.0.0-rc3-Windows-x64`

The ZIP must contain:
- `TankRevivalOverdrive.exe`
- `TankRevivalOverdrive_Data/`
- `BUILD_INFO.txt`
- `DEMO_MANIFEST.txt`
- `README_DEMO.txt`

### Release gate
The candidate is not called a public demo until:
1. Linux Unity compile/package gate is green.
2. The exact produced ZIP passes SHA-256 verification on a fresh Windows runner.
3. The packaged EXE boots and produces `DEMO_SMOKE_PASS.txt`.
4. The same packaged EXE completes rounds 80/90/100 qualification and produces `DEMO_SOAK_PASS.txt`.
5. Projectile-pool integrity remains valid and runtime stability reports no new repairs/warnings/faults during qualification.
6. No blocking runtime exception signature is present in either captured Windows `Player.log`.

Only after those conditions are satisfied should reporting state:

**🎮 DEMO GOTOWE DO GRANIA — WINDOWS EXE**
