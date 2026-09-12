# Tank Revival: Orzeł Overdrive — v5.0.0-rc1

## PUBLIC DEMO CANDIDATE

This milestone converts the previous development build pipeline into a verifiable Windows demo-candidate pipeline.

### Added
- Dedicated non-development Windows x64 build entry point: `CIBuild.BuildDemoCandidate`.
- Separate GitHub Actions workflow for candidate validation and packaging.
- Source gate requiring the demo shell, runtime stability director, late-round stress harness and projectile pool before candidate compilation.
- Runtime package validation for EXE, Unity data directory, build information, demo README and candidate manifest.
- Exact GitHub commit embedded into `DEMO_MANIFEST.txt`.
- SHA-256 checksum generated alongside the final Windows ZIP.
- Development-only `DemoAcceptanceGate` on F10 that passively observes authoritative game states and records menu/play/pause/long-enough-play coverage without altering gameplay authority.

### Packaging
Expected artifact name:

`TankRevival-Orzel-Overdrive-DEMO-v5.0.0-rc1-Windows-x64`

The ZIP must contain:
- `TankRevivalOverdrive.exe`
- `TankRevivalOverdrive_Data/`
- `BUILD_INFO.txt`
- `DEMO_MANIFEST.txt`
- `README_DEMO.txt`

### Release gate
The candidate is not called a public demo until:
1. Windows demo-candidate CI is green.
2. The produced package passes a real Windows launch/menu/play/pause/settings/resume/end-or-soak smoke test.
3. Late-round 80/90/100 stress/soak does not expose a blocking runtime defect.

Only after those conditions are satisfied should reporting state:

**🎮 DEMO GOTOWE DO GRANIA — WINDOWS EXE**
