# v8.4.0-dev — War State, Sector Consequences & Campaign Memory

## Major gameplay milestone

v8.4 turns the local three-stage operation consequences from v8.3 into a campaign-scale war state that survives beyond the Resolution round.

### Persistent sector war state
- Every completed sector operation is recorded as Victory or Defeat for the remainder of the run.
- Strategic momentum accumulates inside strict [-6,+6] bounds instead of growing without limit.
- Rounds after operation Resolution use the current sector result; early rounds of the next sector inherit the previous sector result until the new operation resolves.

### Real sector consequences
- Victory reduces bounded enemy endurance and support pressure through the existing CampaignEncounter profile.
- Defeat increases bounded endurance/support pressure and can preserve champion/strategic-strike initiative.
- Boss rounds are no longer strategically isolated: their preparation is lightly weakened after a sector victory or strengthened after a sector defeat while Boss authority remains unchanged.

### Logistics continuity
- Entering a new sector after victory repairs Orzełek by 2, repairs the player by 1 and awards 4 War Bonds through existing TankGame/Health/WarEconomy authority.
- Defeat never applies hidden direct damage; its cost is carried by encounter pressure instead.
- Starting a new run clears every sector result, momentum and entry-reward sentinel.

### Qualification
- Dedicated Windows gate builds the exact v8.4 development candidate and boots the packaged EXE on a fresh Windows runner with `-war-state-smoke`.
- Runtime matrix verifies all ten sector-memory slots, inherited/current consequence ordering, boss consequence behavior, encounter bounds, momentum clamps and v8.4 version identity.
- Standard Dev Windows Build remains required before roadmap completion.
