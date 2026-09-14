# Tank Revival: Orzeł Overdrive — v10.1.0-dev

## Theater Orders, Operation Branching & Campaign Consequences

- Adds five strategic command windows across the late campaign (rounds 42/56/70/84/98).
- Player can choose ASSAULT, INTERDICTION or FORTIFY with keys 1/2/3; if no choice is made, the default doctrine adapts to current v10.0 command momentum.
- Orders persist for up to four subsequent rounds instead of acting as one-frame bonuses.
- ASSAULT commits bounded real HE support through TankGame projectile authority, prioritising Heavy/Siege and then Elite/Sniper threats.
- INTERDICTION commits an AP fire mission against a live v10.0 theater relay first, then a live v8.6 logistics node if available.
- FORTIFY provides tightly capped player/Orzełek sustain through existing Health authority.
- Active theater orders participate in v10.0 resolved command operations and pay a bounded bonus through the existing War Bonds economy.
- No boss scheduling, enemy roster, Health, Projectile, TankGame spawn authority or existing campaign-memory authority is replaced.
- Dedicated Windows x64 qualification builds the exact candidate and boots the packaged EXE with `-theater-orders-smoke`.
