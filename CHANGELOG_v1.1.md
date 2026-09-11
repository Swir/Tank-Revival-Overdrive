# Tank Revival: Orzeł Overdrive — v1.1.0 BOSS LEGENDS

## Major milestone

Boss rounds are no longer anonymous stat checks. Every tenth round now introduces a named legendary commander with a distinct signature attack package layered on top of the existing four-phase boss system.

## Ten legendary bosses

- Round 10 — IRON WOLF: escalating fan volleys and radial pressure.
- Round 20 — STORM VIPER: chained predictive fan attacks.
- Round 30 — SIEGE KING: delayed bombardment around player and Orzełek approach lanes.
- Round 40 — BLACK COMET: radial crossfire with phase-based offset patterns.
- Round 50 — RED BARON: multi-wave sweeping salvo sequences.
- Round 60 — FROST MAMMOTH: temporary armored stance and expanding ring attacks.
- Round 70 — THUNDER PRIEST: telegraphed impact lances mixed with precision fire.
- Round 80 — NIGHT REAPER: high-speed sweeping projectile walls.
- Round 90 — BURNING TITAN: inferno radial bursts plus delayed area detonations.
- Round 100 — OVERDRIVE PRIME: combined endgame pattern using shielding, radial pressure, targeted fans and delayed blasts.

## Boss presentation

- Dedicated LEGEND boss HUD with name, phase and armor bar.
- Boss-specific color identity and pulsing aura.
- Readable telegraphs before major attacks.
- Campaign-scaled extra boss armor without replacing the existing armor/module system.

## Legendary rewards

Defeating a legend now grants a guaranteed package tied to campaign progression:
- special ammunition,
- field repair or combat upgrade,
- Orzełek repair,
- persistent Legends Defeated statistics.

## Architecture

The new `BossLegendController` is intentionally composable with `MultiPhaseBossController`, `BossWeaponController`, Tactical Warfare, War Machine, Battlefield Command and Operation Overdrive. Existing boss logic is preserved and augmented rather than replaced.

## Validation

Windows x64 CI is required before this milestone can be considered merge-ready.
