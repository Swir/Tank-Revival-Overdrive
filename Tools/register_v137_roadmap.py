#!/usr/bin/env python3
from pathlib import Path
import re

ROADMAP = Path('ROADMAP.md')
README = Path('README.md')

section = '''\n\n## v13.7 — Late-Round Performance & Battle Density Reforge — IN DEVELOPMENT\n- [ ] **Deterministic late-round pressure planner** — classify Normal / Dense / Critical presentation pressure from round, live enemy/unit density, active explosion pressure and the existing performance-governor tier without changing spawn, movement, targeting, damage or economy authority.\n- [ ] **Proactive mass-battle FX budgets** — feed the v13.7 pressure profile into the existing `MassBattleFxBudget` so trails, micro FX, tactical cues and explosion detail step down before late-round presentation churn becomes a frame-time cliff.\n- [ ] **Allocation-aware runtime telemetry** — sample managed-memory / GC collection deltas at a bounded cadence and expose compact diagnostics without per-frame scene scans or unbounded collections.\n- [ ] **Battle-density hysteresis and recovery** — prevent budget thrash with bounded promotion/recovery timing while preserving player graphics-floor settings and the existing `WarfarePerformanceGovernor` escalation rules.\n- [ ] **Pool-preserving density policy** — keep projectile/combat pools warm and reuse-first under rounds 80/90/100 pressure; performance adaptation may reduce optional presentation density but must not reduce enemy counts or suppress gameplay events.\n- [ ] **Performance regression contracts** — verify monotonic Normal→Dense→Critical budgets, hard token/detail caps, stable configuration and unchanged combat-authority boundaries with deterministic source/runtime probes.\n- [ ] **Packaged-EXE v13.7 runtime smoke** — run the v13.7 performance probe on one packaged Windows EXE, then rerun v13.6/v13.5/v13.4/v13.3 regressions plus rounds 80/90/100 soak on that same executable.\n- [ ] **Exact-SHA Windows qualification** — deterministically package one Windows x64 candidate with provenance, require the complete v13.7 gate to pass and only then finalize roadmap/checklist/progress SVGs.\n'''


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f'{label}: expected exactly one match, found {count}')
    return text.replace(old, new, 1)


def main() -> int:
    s = ROADMAP.read_text(encoding='utf-8')
    r = README.read_text(encoding='utf-8')
    if '## v13.7 — Late-Round Performance & Battle Density Reforge' in s:
        print('v13.7 roadmap scope already registered')
        return 0

    done = len(re.findall(r'^- \[x\] ', s, re.M))
    open_ = len(re.findall(r'^- \[ \] ', s, re.M))
    if (done, open_) != (439, 0):
        raise SystemExit(f'unexpected pre-v13.7 roadmap counts: done={done} open={open_}')

    s = replace_once(s, 'badge.svg?branch=dev-v13-6', 'badge.svg?branch=dev-v13-7', 'CI branch badge')
    s = replace_once(s, 'ROADMAP-100.0%25-brightgreen', 'ROADMAP-98.2%25-blue', 'ROADMAP badge')
    s = replace_once(s, 'DONE-439%2F439-1f6feb', 'DONE-439%2F447-1f6feb', 'DONE badge')
    s = replace_once(s, 'STATUS-V13.6%20QUALIFIED-brightgreen', 'STATUS-V13.7%20IN%20DEVELOPMENT-blue', 'STATUS badge')
    s = replace_once(s, 'Roadmap progress: 439 / 439 completed (100.0%) — V13.6 QUALIFIED.', 'Roadmap progress: 439 / 447 completed (98.2%) — V13.7 IN DEVELOPMENT.', 'roadmap fallback')
    s = replace_once(s, '| **439** | **0** | **439** | **100.0%** |', '| **439** | **8** | **447** | **98.2%** |', 'roadmap table')
    s = s.rstrip() + section
    ROADMAP.write_text(s.rstrip() + '\n', encoding='utf-8')

    r = replace_once(r, 'Roadmap-100.0%25%20V13.6%20Qualified', 'Roadmap-98.2%25%20V13.7%20In%20Development', 'README roadmap badge')
    r = replace_once(r, 'Roadmap progress: 439 / 439 completed (100.0%) — V13.6 QUALIFIED.', 'Roadmap progress: 439 / 447 completed (98.2%) — V13.7 IN DEVELOPMENT.', 'README progress fallback')
    r = replace_once(r, '| Development milestone | **V13.6 QUALIFIED** on `dev-v13-6` |', '| Development milestone | **V13.7 IN DEVELOPMENT** on `dev-v13-7` |', 'README milestone')
    r = replace_once(r, '| Roadmap | **439 / 439 completed (100.0%)** — authoritative `ROADMAP.md` scope |', '| Roadmap | **439 / 447 completed (98.2%)** — authoritative `ROADMAP.md` scope |', 'README roadmap table')
    sensor_row = '| 📡 **Sensor fusion v13.6 — qualified** | Battlefield-wide Unknown/Detected/Tracked/Verified contact confidence fuses distance, class signature, weather, terrain and existing Recon/EW telemetry; a finite active sweep improves information only and never replaces targeting, movement or damage authority. |'
    perf_row = sensor_row + '\n| ⚙️ **Late-round performance v13.7 — in development** | Proactive battle-density pressure budgets reduce optional FX/presentation churn during rounds 80/90/100 while preserving enemy counts, gameplay events and canonical combat authority. |'
    r = replace_once(r, sensor_row, perf_row, 'README v13.7 highlight')
    qualified_para = 'The exact v13.6 candidate `6509b163ab0fa2f29d88f2f9c06188f7a1de1bdb` passed Windows qualification run `35388766882`: production authored-audio preflight, v13.6 sensor-fusion smoke, v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak on the same packaged executable.'
    perf_para = qualified_para + '\n\n### Late-Round Performance & Battle Density Reforge v13.7 — in development\n\nv13.7 targets late-campaign frame-pressure and presentation churn without lowering combat density. A deterministic Normal / Dense / Critical pressure profile will combine round band, live registered actors, active explosion pressure and the existing `WarfarePerformanceGovernor` tier, then tighten only optional presentation budgets. Projectile/Health/spawn/movement/targeting authority and enemy counts remain unchanged; the exact Windows gate must prove the same packaged EXE still passes v13.6+ regressions and rounds 80/90/100 soak before any v13.7 checkbox can close.'
    r = replace_once(r, qualified_para, perf_para, 'README v13.7 section')
    r = replace_once(r, 'The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **439 / 439 (100.0%) — V13.6 QUALIFIED**.', 'The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **439 / 447 (98.2%) — V13.7 IN DEVELOPMENT**.', 'README roadmap summary')
    r = replace_once(r, '- **Development milestone:** v13.6 is in development on `dev-v13-6`; v13.5 remains the latest qualified development milestone and neither state is automatically a new public release.', '- **Development milestone:** v13.7 is in development on `dev-v13-7`; v13.6 remains the latest qualified development milestone and neither state is automatically a new public release.', 'README release status')
    old_keywords = '`tank defense game` • `Windows tank game` • `2.5D tank action` • `top down tank game` • `Unity 6 game` • `C# Unity game` • `100 round campaign` • `adaptive enemy AI` • `tank command AI` • `squad command AI` • `destructible cover game` • `tactical terrain game` • `battlefield weather game` • `sensor fusion tank game` • `contact warfare game` • `boss tank battles` • `tank armor simulation` • `component damage system` • `special ammunition game` • `Orzelek defense` • `Unity GitHub Actions` • `headless Unity CI`'
    new_keywords = '`tank defense game` • `Windows tank game` • `2.5D tank action` • `top down tank game` • `Unity 6 game` • `C# Unity game` • `100 round campaign` • `adaptive enemy AI` • `squad command AI` • `tactical terrain game` • `battlefield weather game` • `sensor fusion tank game` • `late round performance` • `battle density optimization` • `boss tank battles` • `tank armor simulation` • `component damage system` • `special ammunition game` • `Orzelek defense` • `Unity GitHub Actions`'
    r = replace_once(r, old_keywords, new_keywords, 'README keywords')
    README.write_text(r, encoding='utf-8')

    print('registered v13.7 scope: 439/447 (98.2%)')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
