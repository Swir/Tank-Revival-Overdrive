# Tank Revival: Orzeł Overdrive — v5.0.0-rc2

## PUBLIC DEMO CANDIDATE — RUNTIME QUALIFICATION

RC2 upgrades the demo gate from file/package validation to an actual packaged Windows runtime boot test.

### Added
- Dedicated non-development Windows x64 build entry point: `CIBuild.BuildDemoCandidate`.
- Separate GitHub Actions workflow for candidate validation and packaging.
- Source gate requiring the demo shell, runtime stability director, late-round stress harness and projectile pool before candidate compilation.
- Runtime package validation for EXE, Unity data directory, build information, demo README and candidate manifest.
- Exact GitHub commit embedded into `DEMO_MANIFEST.txt`.
- SHA-256 checksum generated alongside the final Windows ZIP.
- `DemoCISmokeProbe`: opt-in standalone runtime probe activated only by `-demo-ci-smoke`.
- A second `windows-latest` CI job that downloads the exact packaged ZIP, validates its checksum, extracts it and launches the real `TankRevivalOverdrive.exe`.
- Runtime smoke PASS requires authoritative `TankGame`, the player-facing `DemoExperienceDirector` shell and `RuntimeStabilityDirector` to exist in the packaged non-development build.
- The Windows smoke job captures `Player.log`, rejects blocking crash/exception signatures and uploads diagnostics for failed qualification.
- Development-only `DemoAcceptanceGate` on F10 remains available for longer interactive menu/play/pause/resume/soak acceptance.

### Packaging
Expected artifact name:

`TankRevival-Orzel-Overdrive-DEMO-v5.0.0-rc2-Windows-x64`

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
3. The packaged EXE boots and produces `DEMO_SMOKE_PASS.txt` from the real standalone runtime.
4. No blocking runtime exception signature is present in the captured Windows `Player.log`.
5. Late-round 80/90/100 stress/soak does not expose a blocking runtime defect.

Only after those conditions are satisfied should reporting state:

**🎮 DEMO GOTOWE DO GRANIA — WINDOWS EXE**
