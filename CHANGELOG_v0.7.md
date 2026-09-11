# Tank Revival: Orzeł Overdrive — v0.7 WAR MACHINE

## Elite war-machines

From round 18 onward selected enemy tanks can become visible elite variants. These are not hidden stat rolls: each mutation has its own marker, color language and active battlefield ability.

- **Vanguard** — blue defensive specialist that periodically repairs itself and nearby armor.
- **Berserker** — red assault specialist that launches three-shell pressure salvos.
- **Engineer** — green support specialist that repairs multiple nearby enemy tanks.
- **Hunter** — purple marksman specialist that periodically fires fast precision shots at the player.

Mutation frequency scales with campaign progress and can exceed 50% during the final sector.

## Dynamic War Orders

Every round now receives a strategic enemy order that changes what kind of war-machines the director prefers:

- **Vanguard** — balanced armored pressure.
- **Hunter-Killer** — increases Hunter mutations among Fast, Sniper and Elite tanks.
- **Siege Network** — increases Engineer support among Heavy/Siege formations and makes relays prioritize Orzełek.
- **Total Overdrive** — late-game aggression with a high Berserker bias.
- **Iron Guard** — favors Vanguard armor among heavy formations.

The active order is shown in the top-right HUD so the player can adapt before committing to a fight.

## Enemy Support Relay

Starting at round 24, the enemy can deploy destructible battlefield installations. Relays:

- heal damaged enemy tanks in an area,
- fire pressure shells at the player or Orzełek depending on the active War Order,
- increase in number and activity through the campaign,
- provide a new priority-target decision during crowded late-game rounds,
- explode visibly when destroyed.

Relay caps scale from one installation to three in the final campaign sectors.

## Campaign integration

WAR MACHINE is layered on top of the existing Tactical Warfare, Real Armor and Battlefield Destruction systems. It does not replace EnemyTank movement or the proven boss controller. The design goal is to make late rounds change tactically without destabilizing early-round behavior.

## Technical

- Added `WarMachineDirector` runtime campaign director.
- Added `EliteWarMachine` additive enemy behavior component.
- Added `EnemySupportRelay` destructible enemy installation.
- Added `WarOrder` and `EliteMutation` runtime enums.
- Updated dev Windows CI to validate `dev-v0.7`.
- Development version bumped to `v0.7.0-dev`.
