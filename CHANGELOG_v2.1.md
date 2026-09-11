# Tank Revival: Orzeł Overdrive — v2.1 COMMAND CAMPAIGN & ENDGAME

## Major milestone goals

v2.1 turns the late campaign into a more authored command experience instead of only increasing enemy numbers. It adds strategic act choices, five dedicated elite encounters, and adaptive endgame pressure that reacts to the state of the player and Orzełek.

## Command Campaign

- Acts II-V now begin with a real doctrine decision.
- `8` selects **Fortress Doctrine**: immediate Orzełek/tank recovery plus periodic defensive sustain.
- `9` selects **Hunter Doctrine**: offensive AP/EMP/Plasma reserves and recurring combat resupply.
- If the player does not choose, Fortress is selected automatically after the decision window so the campaign cannot deadlock.
- The selected doctrine is visible in the combat HUD layer and persists in PlayerPrefs.

## Elite Encounters

- Rounds **15 / 35 / 55 / 75 / 95** now contain named mini-boss encounters:
  - Iron Jackal
  - Ash Executioner
  - Frost Hammer
  - Fortress Breaker
  - Black Eagle Hunter
- The system promotes an existing Heavy/Siege/Elite-class battlefield threat instead of spawning disconnected enemies outside TankGame authority.
- Elite encounters gain additional durability, three health-driven escalation phases, telegraphed special volleys and later AP/Plasma pressure.
- Defeating one repairs Orzełek, restores the player and grants campaign-scaled special ammunition.
- Defeats are recorded persistently for future meta progression.

## Adaptive Endgame 60-100

- A new endgame director continuously selects one of three readable pressure states:
  - **Eagle Breakthrough** — heavy units prioritize the defense line when Orzełek is healthy.
  - **Tank Hunter** — elite threats shift pressure toward a weakened player tank.
  - **Last Stand** — Orzełek remains vulnerable, but extra siege attacks are spaced farther apart to reduce unfair overlapping burst damage at critical HP.
- Heavy/Sniper/Siege/Elite/Boss units receive telegraphed adaptive AP/HE/EMP/Plasma pressure attacks.
- Late Elite/Boss units can perform paired volleys outside Last Stand mode.
- This layer composes with existing Eagle Siege, factions, armor hunting and boss systems; it does not replace their authority.

## Stability / integration

- Existing Orzełek destruction rules remain unchanged: enemy damage can still destroy the core and immediately end the campaign.
- No environmental or artificial immunity was added to Orzełek.
- New systems reuse `Health`, `EnemyTank`, `PlayerTank`, `AmmoDatabase`, `TankGame.SpawnProjectile`, `RepairEagle`, audio and visual factories.
- Stable `main` is untouched until Windows x64 CI and runtime review are satisfactory.
