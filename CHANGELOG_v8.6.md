# Tank Revival: Orzeł Overdrive — v8.6.0-dev

## Logistics Network, Supply Depots & Strategic Interdiction

This milestone turns v8.5 strategic reserves into physical battlefield objectives that the player can actively interdict.

### Playable logistics network
- Deterministic non-boss logistics operations appear from round 12 onward on three sector slots.
- Three physical enemy infrastructure types use existing `Health` and collision authority: Fire-Support Depot, Armored Supply Convoy and EW Repair Hub.
- Mobile supply convoys physically traverse the battlefield while depots and repair hubs remain fixed high-value targets.
- Each node has bounded round-scaled HP, readable world presentation and compact temporary operation messaging.

### Strategic attrition integration
- Destroying a logistics node immediately consumes three points from the matching v8.5 Armor, Fire Support or Electronic Warfare reserve.
- Allowing a node to survive until the next round restores two points of the matching reserve, bounded by the existing sector capacity.
- Every destroyed node increases bounded sector interdiction; every escaped node recovers one interdiction step.
- Interdiction refines later CampaignEncounter pressure by reducing endurance/fire support and, at severe disruption, non-boss champion/strategic-strike availability.
- Boss rounds keep deliberately reduced logistics effects so campaign climaxes remain dangerous.

### Progression and authority
- Destroyed logistics nodes award existing War Bonds rather than introducing a new currency.
- No parallel enemy roster, projectile, damage or health system is introduced.
- `TankGame`, `Health`, `Projectile`, `CampaignEncounterDirector` and `StrategicReserveAttritionDirector` remain authoritative.

### Qualification
- Dedicated Logistics Network Windows Gate builds the exact Windows x64 candidate and boots it on a fresh Windows runner with `-logistics-network-smoke`.
- Runtime validation covers all 100 rounds for scheduling, all three node classes, destruction/survival reserve ordering, encounter-pressure ordering, boss preservation, runtime installation and v8.6 version identity.
