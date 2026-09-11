# v2.2.0 — OPERATIONS & META PROGRESSION

## Command Career

- Added a persistent Command XP and Command Rank career spanning multiple campaigns.
- Existing progression now contributes to one unified career: contracts, Boss Legends, Strategic Directives, Elite Encounters, Special Operations, medals, best act and sector progress.
- Eleven ranks from Cadet through Eagle Marshal provide visible long-term goals.
- Rank perks are functional: deployment AP/HE/EMP/Plasma reserves, recovery, short deployment protection and high-rank act support for Orzelek.
- Career status is visible from the pre-campaign screen.

## Special Operations

- Added ten deterministic operation opportunities across the 100-round campaign on rounds 8, 18, 28 ... 98.
- SPEARHEAD DECAPITATION promotes existing wave enemies into tougher assault leaders that must be destroyed.
- EAGLE LOCKDOWN promotes a dedicated Eagle breaker and requires preserving Orzelek HP.
- ARMORED PURGE requires eliminating campaign-scaled heavy priority armor.
- Promoted operation targets gain extra durability, telegraphed visual identity and additional AP/Plasma-capable fire as the campaign escalates.
- Successful operations award Bronze, Silver or Gold medals based on Eagle and player condition.
- Operation rewards reuse the real combat economy: Orzelek repair, player recovery and special ammunition.
- Operation completions and medal totals feed directly into Command XP.

## Director Performance Pass

- Added CombatRoster, one bounded shared 0.30-second snapshot of enemies, health units, player and the Orzelek core.
- New v2.2 systems use the shared roster instead of performing independent scene-wide scans.
- TotalOverdriveCampaignDirector now consumes CombatRoster for enemy hooks and player acquisition, reducing redundant scans during high-density late rounds.
- The cache is intentionally read-only to combat directors; TankGame remains authoritative for spawning, destruction and campaign state.

## Compatibility / Safety

- Existing War Garage, armor, factions, siege engineering, Eagle Fortress, Boss Legends, ammunition, directives and round-100 finale remain authoritative.
- Orzelek is still fully destructible. Meta rank and medals never grant permanent invulnerability to the core.
- All persistent values use new TankRevival PlayerPrefs keys and preserve existing saves.
