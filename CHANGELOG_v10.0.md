# Tank Revival: Orzeł Overdrive — v10.0.0-dev

## Combined Arms Campaign Command & 100-Round War Reforge

v10.0 is a campaign-integration milestone. It does not add a disconnected minigame; it binds the existing frontline, logistics, SIGINT, fortification and artillery-era systems into bounded theater-command operations distributed through the 100-round war.

### Playable command operations
- Seven deterministic non-boss command operations are scheduled across rounds 41–97 without colliding with the existing Multi-Stage Operations or Combined Arms operations.
- Each operation has three readable phases: reconnaissance, interdiction and decisive hold.
- A physical enemy theater-command relay uses the authoritative `Health`, collider and projectile damage path. Destroying it is the decisive player objective.
- The relay coordinates bounded response waves from the existing enemy roster. No parallel enemy AI or damage model is introduced.

### Persistent command momentum
- Operation wins and losses update a hard-clamped command momentum from -2 to +3 for the current campaign run.
- Negative momentum raises enemy response pressure and relay durability; positive momentum lowers response pressure and increases friendly artillery support and War Bond payoff.
- Boss rounds, TankGame round ownership, campaign memory, reserves and earlier v8/v9 directors remain authoritative.

### Cross-system integration
- Active v9.5 fire-mission target intelligence can shorten the reconnaissance phase.
- Relay placement uses the v9.0 frontline lane map.
- v10.0 smoke qualification requires the v8 logistics/supply chain, v9 frontline/fortification stack and v9.5 fire-mission network to be installed in the packaged player.

### Qualification
- Dedicated Windows x64 candidate workflow builds and packages the exact executable, then boots that same artifact on a fresh Windows runner with `-campaign-command-smoke`.
- The smoke matrix validates all 100 rounds, operation collision exclusions, momentum clamps, response-wave/size caps, relay durability, reward/support bounds, integration services and application version.
- Standard Dev Windows CI remains a second independent compile/package plus packaged-EXE regression gate.
