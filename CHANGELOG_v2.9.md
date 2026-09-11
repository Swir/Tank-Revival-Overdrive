# v2.9.0 — ORZEŁ FORTRESS COMMAND

Major player-side defensive strategy milestone built on the green v2.8 campaign branch.

## Fortress command layer
- Added three sector doctrines: **Bastion**, **Hunter Grid**, and **Recovery Corps**.
- Added persistent **Command Charges** earned between rounds and for perfect sector defense.
- Added four doctrine levels with escalating gameplay bonuses.
- Doctrine selection is locked per sector so the choice matters across a 10-round theater.

## Active defensive commands
- **F5 Aegis**: temporary Orzełek invulnerability plus defensive module reinforcement.
- **F6 Counter-Battery**: EMP disruption around the fortress; Hunter Grid can damage heavy/siege targets.
- **F7 Engineer Surge**: repairs Orzełek/fortress, feeds Defense Parts and can deploy Recovery Nodes.
- **F4 Doctrine Upgrade** spends Command Charges to deepen the selected specialization.

## Existing systems integrated
- Eagle Fortress armor HP now scales with Bastion doctrine.
- Bastion level 3 adds an inner armor line.
- Hunter Grid improves sentry range, fire rate and damage; level 3 adds a third fortress sentry.
- Recovery Corps improves repair relay charges, cadence and repair amount.
- Field Engineers receive doctrine-based Defense Parts.
- Bastion reduces minefield cost and strengthens barricades.
- Hunter Grid raises the anti-siege turret cap and improves anti-siege fire performance.
- Recovery improves emergency repairs and creates limited player-healing recovery nodes.

## 3D presentation
- Added real runtime 3D models for player barricades, anti-siege turrets, recovery nodes and the fortress command beacon.
- Anti-siege 3D turrets track real priority targets.
- Command beacon color and rank bars reflect current doctrine and level.
- Existing Rigidbody2D / Collider2D gameplay remains authoritative.

## Safety / compatibility
- Orzełek remains fully destructible.
- Existing Health, ArmorSystem, projectile, campaign and loss paths remain authoritative.
- No changes are merged to `main` until Windows x64 CI is green.
