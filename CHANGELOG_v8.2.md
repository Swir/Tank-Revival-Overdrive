# v8.2.0-dev — Sector Identity, Encounter Decks & Campaign Replayability Reforge

## Major gameplay milestone

v8.2 builds on the v8.1 100-round pacing director by giving each ten-round sector a distinct doctrine and rotating encounter deck. The goal is to make the campaign read as ten different theaters rather than the same ten-beat rhythm repeated with rising stats.

### Ten sector doctrines
- Border Contact — readable first-contact pressure.
- Blitz Corridor — fast spearheads and flank denial.
- Hunter Grounds — sniper/hunter emphasis.
- Fortress Belt — heavy screens and siege pressure.
- Electronic Front — EW/network counterplay emphasis.
- Artillery Basin — fire-support pressure and movement discipline.
- Armored Reserve — heavy-column endurance fights.
- Broken Highway — mixed/mobile pressure.
- Command Depth — relay/command-network hunt emphasis.
- Final Redoubt — late-campaign siege and last-stand pressure.

### Replayable encounter decks
Each sector receives one of three bounded decks per run:
- **Spearhead** — faster direct pressure and aggressive archetypes.
- **Attrition** — stronger endurance/champion bias with slower support cadence.
- **Disruption** — lighter raw endurance but stronger fire-support/strategic-strike emphasis.

Deck rotation changes between runs while sector identity remains recognizable. Boss rounds are explicitly preserved and v8.1 recovery/special-operation/boss cadence remains authoritative.

### Integration and safety
- `SectorIdentityDirector` runs after `CampaignPacingDirector` and refines the existing `CampaignEncounterDirector` profile instead of spawning enemies or owning damage.
- Encounter health refinement is bounded to the existing campaign envelope; sector identity multipliers stay inside 0.94–1.08 health and 0.90–1.12 fire-support ranges.
- Existing TankGame, Health, Projectile, boss cadence, EW/network thresholds and Mobile HQ scheduling remain authoritative.
- Sector briefing is short-lived and only appears on round transitions.

### Qualification
A dedicated Windows gate builds the exact v8.2 candidate, boots the packaged EXE on a fresh Windows runner and validates all 10 doctrines × 3 decks, replay rotation, boss preservation, v8.1 pacing integration and version identity.
