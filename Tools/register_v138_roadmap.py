#!/usr/bin/env python3
from pathlib import Path
import re

ROADMAP = Path('ROADMAP.md')
README = Path('README.md')

section = '''\n\n## v13.8 — Battlefield Suppression & Morale Warfare — IN DEVELOPMENT\n- [ ] **Deterministic suppression-state model** — add bounded Steady / Suppressed / Pinned pressure states with fixed-cap runtime storage, deterministic decay and no hidden Health, damage or spawn authority.\n- [ ] **Ammo-aware impact and near-miss pressure** — derive suppression only from existing player-owned Projectile damage/impact events, with HE/EMP/Plasma receiving readable bounded pressure profiles and zero bonus damage.\n- [ ] **Canonical enemy-behavior integration** — feed suppression multipliers into existing EnemyTank movement, reload and spread paths while EnemyTank/Rigidbody2D/Projectile remain the only movement/fire authorities and no state can immobilize an actor.\n- [ ] **Cohesion-aware rally and boss resistance** — consume v13.3 cohesion intent plus enemy class to accelerate disciplined recovery, preserve finite boss resistance and avoid permanent crowd control or morale snowballs.\n- [ ] **Readable budgeted suppression feedback** — expose compact fixed-cap telemetry and threshold-change world cues through the existing presentation budget instead of permanent per-enemy UI clutter.\n- [ ] **Performance and authority contracts** — verify fixed actor caps, bounded event fan-out, deterministic pressure/decay/scales, zero scene scans in the hot path and unchanged Health/Projectile/TankGame authority.\n- [ ] **Packaged-EXE v13.8 runtime smoke** — validate direct-hit/near-miss/ammo profiles, suppression transitions, rally/fairness floors and runtime installation, then rerun v13.7/v13.6/v13.5/v13.4 regressions plus rounds 80/90/100 soak on the same executable.\n- [ ] **Exact-SHA Windows qualification** — deterministically package one Windows x64 candidate with provenance, require the complete v13.8 gate to pass and only then finalize roadmap/checklist/progress SVGs.\n'''


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f'{label}: expected exactly one match, found {count}')
    return text.replace(old, new, 1)


def main() -> int:
    s = ROADMAP.read_text(encoding='utf-8')
    r = README.read_text(encoding='utf-8')
    if '## v13.8 — Battlefield Suppression & Morale Warfare' in s:
        print('v13.8 roadmap scope already registered')
        return 0

    done = len(re.findall(r'^- \[x\] ', s, re.M))
    open_ = len(re.findall(r'^- \[ \] ', s, re.M))
    if (done, open_) != (447, 0):
        raise SystemExit(f'unexpected pre-v13.8 roadmap counts: done={done} open={open_}')

    s = replace_once(s, 'badge.svg?branch=dev-v13-7', 'badge.svg?branch=dev-v13-8', 'CI branch badge')
    s = replace_once(s, 'ROADMAP-100.0%25-brightgreen', 'ROADMAP-98.2%25-blue', 'ROADMAP badge')
    s = replace_once(s, 'DONE-447%2F447-1f6feb', 'DONE-447%2F455-1f6feb', 'DONE badge')
    s = replace_once(s, 'STATUS-V13.7%20QUALIFIED-brightgreen', 'STATUS-V13.8%20IN%20DEVELOPMENT-blue', 'STATUS badge')
    s = replace_once(s, 'Roadmap progress: 447 / 447 completed (100.0%) — V13.7 QUALIFIED.', 'Roadmap progress: 447 / 455 completed (98.2%) — V13.8 IN DEVELOPMENT.', 'roadmap fallback')
    s = replace_once(s, '| **447** | **0** | **447** | **100.0%** |', '| **447** | **8** | **455** | **98.2%** |', 'roadmap table')
    s = s.rstrip() + section
    ROADMAP.write_text(s.rstrip() + '\n', encoding='utf-8')

    r = replace_once(r, 'Roadmap-100.0%25%20V13.7%20Qualified', 'Roadmap-98.2%25%20V13.8%20In%20Development', 'README roadmap badge')
    r = replace_once(r, 'Roadmap progress: 447 / 447 completed (100.0%) — V13.7 QUALIFIED.', 'Roadmap progress: 447 / 455 completed (98.2%) — V13.8 IN DEVELOPMENT.', 'README progress fallback')
    r = replace_once(r, '| Development milestone | **V13.7 QUALIFIED** on `dev-v13-7` |', '| Development milestone | **V13.8 IN DEVELOPMENT** on `dev-v13-8` |', 'README milestone')
    r = replace_once(r, '| Roadmap | **447 / 447 completed (100.0%)** — authoritative `ROADMAP.md` scope |', '| Roadmap | **447 / 455 completed (98.2%)** — authoritative `ROADMAP.md` scope |', 'README roadmap table')
    perf_row = '| ⚙️ **Late-round performance v13.7 — qualified** | Deterministic Normal/Dense/Critical pressure budgets reduce optional FX/presentation churn during rounds 80/90/100 while preserving enemy counts, gameplay events, pool integrity and canonical combat authority. |'
    suppression_row = perf_row + '\n| 💢 **Battlefield suppression v13.8 — in development** | Player impacts and near misses build bounded enemy suppression that affects only existing movement/reload/spread paths, with ammo-aware pressure, cohesion recovery and no bonus damage or permanent stun. |'
    r = replace_once(r, perf_row, suppression_row, 'README v13.8 highlight')
    qualified_para = 'v13.7 targets late-campaign frame-pressure and presentation churn without lowering combat density. Its deterministic Normal / Dense / Critical pressure profile combines round band, live registered actors, active explosion pressure and the existing `WarfarePerformanceGovernor` tier, then tightens only optional presentation budgets. Projectile/Health/spawn/movement/targeting authority and enemy counts remain unchanged. Exact candidate `dc646fc60be572e625f5ea310058aab10d2f26da` passed Windows qualification run `35393272731`, including production authored-audio preflight, v13.7 performance smoke, v13.6/v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak on the same packaged executable.'
    suppression_para = qualified_para + '\n\n### Battlefield Suppression & Morale Warfare v13.8 — in development\n\nv13.8 adds a bounded information-and-behavior layer for enemy suppression without creating a second damage, movement or firing authority. Existing player-owned Projectile impacts and near misses build finite pressure with ammo-aware profiles; EnemyTank consumes only clamped movement/reload/spread multipliers, while v13.3 cohesion and class resistance govern recovery. The implementation is required to stay fixed-cap, avoid hot-path scene scans, preserve enemy counts and keep bosses resistant rather than immune. The exact Windows gate must prove the same packaged EXE still passes v13.7+ regressions and rounds 80/90/100 soak before any v13.8 checkbox can close.'
    r = replace_once(r, qualified_para, suppression_para, 'README v13.8 section')
    r = replace_once(r, 'The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **447 / 447 (100.0%) — V13.7 QUALIFIED**.', 'The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **447 / 455 (98.2%) — V13.8 IN DEVELOPMENT**.', 'README roadmap summary')
    r = replace_once(r, '- **Development milestone:** v13.7 is qualified on `dev-v13-7`; this qualification does not automatically create or replace a public release.', '- **Development milestone:** v13.8 is in development on `dev-v13-8`; v13.7 remains the latest qualified development milestone and neither state automatically creates or replaces a public release.', 'README release status')
    old_keywords = '`tank defense game` • `Windows tank game` • `2.5D tank action` • `top down tank game` • `Unity 6 game` • `C# Unity game` • `100 round campaign` • `adaptive enemy AI` • `squad command AI` • `tactical terrain game` • `battlefield weather game` • `sensor fusion tank game` • `late round performance` • `battle density optimization` • `boss tank battles` • `tank armor simulation` • `component damage system` • `special ammunition game` • `Orzelek defense` • `Unity GitHub Actions`'
    new_keywords = '`tank defense game` • `Windows tank game` • `2.5D tank action` • `top down tank game` • `Unity 6 game` • `C# Unity game` • `100 round campaign` • `adaptive enemy AI` • `squad command AI` • `tactical terrain game` • `battlefield weather game` • `sensor fusion tank game` • `late round performance` • `suppression warfare` • `boss tank battles` • `tank armor simulation` • `component damage system` • `special ammunition game` • `Orzelek defense` • `Unity GitHub Actions`'
    r = replace_once(r, old_keywords, new_keywords, 'README keywords')
    README.write_text(r, encoding='utf-8')

    print('registered v13.8 scope: 447/455 (98.2%)')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
