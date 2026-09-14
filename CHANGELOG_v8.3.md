# v8.3.0-dev — Operation Chains, Branching Objectives & Sector Campaign Arcs

## Major gameplay milestone

v8.3 adds persistent three-round operation chains inside every 10-round sector so campaign encounters have consequences instead of resetting strategically after every round.

### Operation chains
- Every sector contains a deterministic three-stage operation on sector rounds 4–6: Opening, Exploitation and Resolution.
- Each stage reads authoritative player and Orzełek health state; it does not create a parallel objective-health model.
- Success, neutral and failure outcomes are carried into the next stage.

### Branching consequences
- Success reduces the next stage's bounded encounter endurance and fire-support pressure.
- Failure increases bounded endurance/support pressure, may preserve champions and can accelerate strategic-strike pressure in eligible late rounds.
- Boss rounds are explicitly excluded from chain mutation.
- Two or more successful stages close the operation as a victory and award existing War Bonds through `WarEconomyDirector`.

### Campaign integration
- `OperationChainDirector` executes after v8.1 pacing and v8.2 sector identity, refining the already-authoritative `CampaignEncounterDirector` profile instead of replacing TankGame spawning, Health, Projectile, AI or economy authority.
- Ten sector codenames give each chain a readable campaign arc while keeping UI messaging short and temporary.

### Qualification
- Dedicated `OperationChainCISmokeProbe` validates all 100 rounds, exactly 30 chain rounds, consequence ordering/bounds, outcome rules and boss preservation.
- `Operation Chain Windows Gate` builds the exact Windows x64 candidate and boots the packaged EXE on a fresh Windows runner with `-operation-chain-smoke`.
- Standard Dev Windows Build remains a required regression gate before roadmap completion.
