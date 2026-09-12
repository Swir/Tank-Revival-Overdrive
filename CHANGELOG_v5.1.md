# Tank Revival: Orzeł Overdrive — v5.1.0-dev

## Resilient Player Profile & Recovery

This milestone starts the post-demo reliability track without modifying combat authority or the qualified v5.0 RC3 candidate.

### Added
- `PlayerProfileDirector` as a persistent runtime service created automatically after scene load.
- Versioned JSON profile stored under `Application.persistentDataPath`.
- SHA-256 integrity checksum around the serialized profile payload.
- Verified temporary write before promoting a save to the primary profile.
- Backup rotation for the previous primary profile.
- Automatic recovery from the backup when the primary profile is missing, malformed or fails checksum validation.
- Safe fallback to a fresh profile if both primary and backup are unusable.
- Legacy `TankRevival.HighScore` migration into the durable profile.
- Durable furthest-round, last-round, runs-started, round-transition and recovery counters.
- Autosave on round transitions plus focus loss, application pause and application quit.
- Failure isolation: profile I/O errors never block gameplay and retry later.

### Roadmap governance
- `ROADMAP.md` now follows `SWIR-ROADMAP-STANDARD:v1`.
- Added canonical CI/ROADMAP/DONE/STATUS dashboard, exact checklist counting and 20-segment ASCII progress bar.
- `ROADMAP_v03.txt` remains historical only.

### Release safety
- v5.0 RC3 remains untouched on `dev-v5-0`.
- v5.1 work is isolated on `dev-v5-1` and must pass Windows CI before roadmap deliverables are marked complete.
