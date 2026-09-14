# v11.0.0-dev — Tactical Combat Reforge, Cover, Suppression & Formation AI

## Major gameplay changes

- Existing squad tactics, tactical navigation, terrain intelligence and regroup systems are now coordinated by bounded combat platoons instead of acting as isolated layers.
- Up to three six-vehicle platoons elect class-aware leaders and assign assault, flank, suppress, breaker, escort and hunter roles without replacing `EnemyTank`, `Health` or `TacticalNavigationAgent` authority.
- Real HP loss now creates a bounded suppression state. Suppressed vehicles temporarily break their normal formation and relocate through the existing navigation and terrain-refinement path, then naturally recover as suppression decays.
- Losing a platoon leader produces a short cohesion break. Survivors regroup around the elected successor before reforming, making high-value kills change enemy battlefield behavior rather than only removing one tank.
- High Command retasking retains priority: the combat reforge yields when an enemy is under an active High Command counter-doctrine directive.
- Late-round load is bounded to 18 managed combatants, fixed decision cadences and capped platoon sizes.
- Lightweight tactical HUD/world cues expose active platoons, suppression and cohesion breaks without adding another permanent debug wall.

## Qualification

- New packaged Windows x64 smoke probe: `-tactical-combat-reforge-smoke`.
- New dedicated Windows gate verifies exact candidate packaging/boot plus platoon, role, suppression, leader-loss and authority bounds.
- Standard Dev Windows Build remains a required regression gate before roadmap qualification.

## Roadmap discipline

The five v11.0 deliverables were registered in `ROADMAP.md` as unchecked before implementation. They may only be closed after both Windows gates are green.
