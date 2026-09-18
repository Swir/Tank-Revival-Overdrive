#!/usr/bin/env python3
from pathlib import Path
import os
import re

ROADMAP = Path('ROADMAP.md')
README = Path('README.md')
EVIDENCE = Path('docs/qualification/V13_7_WINDOWS_QUALIFICATION.md')
CANDIDATE_SHA = 'dc646fc60be572e625f5ea310058aab10d2f26da'
QUAL_RUN_ID = '35393272731'
INNER_ZIP_SHA256 = '0328a6613dcc64dffb1f675282e27a798b0f3abcb561e13423b7b6de3d75c25f'
INNER_ZIP_BYTES = 60548408
ARTIFACT_ID = '10566756672'
ARTIFACT_DIGEST = 'sha256:e4b063898c0e516fda4064d183080a4219861c593e572153b327bd857f8b05c4'


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f'{label}: expected exactly one match, found {count}')
    return text.replace(old, new, 1)


def finalize_roadmap(text: str) -> str:
    done = len(re.findall(r'^- \[x\] ', text, re.M))
    open_ = len(re.findall(r'^- \[ \] ', text, re.M))
    if (done, open_, done + open_) != (439, 8, 447):
        raise SystemExit(f'unexpected pre-finalization roadmap state: {done}/{done + open_} with {open_} open')

    text = replace_once(text, 'ROADMAP-98.2%25-blue', 'ROADMAP-100.0%25-brightgreen', 'roadmap badge')
    text = replace_once(text, 'DONE-439%2F447-1f6feb', 'DONE-447%2F447-1f6feb', 'done badge')
    text = replace_once(text, 'STATUS-V13.7%20IN%20DEVELOPMENT-blue', 'STATUS-V13.7%20QUALIFIED-brightgreen', 'status badge')
    text = replace_once(text, 'Roadmap progress: 439 / 447 completed (98.2%) — V13.7 IN DEVELOPMENT.', 'Roadmap progress: 447 / 447 completed (100.0%) — V13.7 QUALIFIED.', 'roadmap fallback')
    text = replace_once(text, '| **439** | **8** | **447** | **98.2%** |', '| **447** | **0** | **447** | **100.0%** |', 'roadmap table')
    heading = '## v13.7 — Late-Round Performance & Battle Density Reforge — IN DEVELOPMENT'
    qualified = '## v13.7 — Late-Round Performance & Battle Density Reforge — QUALIFIED'
    text = replace_once(text, heading, qualified, 'v13.7 heading')

    start = text.index(qualified)
    prefix, section = text[:start], text[start:]
    if section.count('- [ ]') != 8 or section.count('- [x]') != 0:
        raise SystemExit('v13.7 section must contain exactly eight open and zero completed items before finalization')
    section = section.replace('- [ ]', '- [x]')
    return prefix + section


def finalize_readme(text: str) -> str:
    text = replace_once(text, 'Roadmap-98.2%25%20V13.7%20In%20Development', 'Roadmap-100.0%25%20V13.7%20Qualified', 'README roadmap badge')
    text = replace_once(
        text,
        '[![v13.6 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-sensor-fusion-v136-windows.yml/badge.svg?branch=dev-v13-6)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/battlefield-sensor-fusion-v136-windows.yml)',
        '[![v13.7 Windows Gate](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/late-round-performance-v137-windows.yml/badge.svg?branch=dev-v13-7)](https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/late-round-performance-v137-windows.yml)',
        'README Windows gate badge')
    text = replace_once(text, 'Roadmap progress: 439 / 447 completed (98.2%) — V13.7 IN DEVELOPMENT.', 'Roadmap progress: 447 / 447 completed (100.0%) — V13.7 QUALIFIED.', 'README progress fallback')
    text = replace_once(text, '| Development milestone | **V13.7 IN DEVELOPMENT** on `dev-v13-7` |', '| Development milestone | **V13.7 QUALIFIED** on `dev-v13-7` |', 'README milestone row')
    text = replace_once(text, '| Roadmap | **439 / 447 completed (98.2%)** — authoritative `ROADMAP.md` scope |', '| Roadmap | **447 / 447 completed (100.0%)** — authoritative `ROADMAP.md` scope |', 'README roadmap row')
    text = replace_once(text, '| Latest qualified milestone | **v13.6** — exact Windows candidate qualified |', '| Latest qualified milestone | **v13.7** — exact Windows candidate `dc646fc60be572e625f5ea310058aab10d2f26da` qualified in run `35393272731` |', 'README latest qualified row')
    text = replace_once(text, '| ⚙️ **Late-round performance v13.7 — in development** | Proactive battle-density pressure budgets reduce optional FX/presentation churn during rounds 80/90/100 while preserving enemy counts, gameplay events and canonical combat authority. |', '| ⚙️ **Late-round performance v13.7 — qualified** | Deterministic Normal/Dense/Critical pressure budgets reduce optional FX/presentation churn during rounds 80/90/100 while preserving enemy counts, gameplay events, pool integrity and canonical combat authority. |', 'README highlight')
    text = replace_once(text, '| 🧪 **Exact-candidate qualification** | v13.6 passed one packaged Windows EXE through production-audio preflight, v13.6 sensor-fusion smoke, v13.5/v13.4/v13.3 regressions and late-round 80/90/100 soak. |', '| 🧪 **Exact-candidate qualification** | v13.7 passed one packaged Windows EXE through production-audio preflight, v13.7 performance smoke, v13.6/v13.5/v13.4/v13.3 regressions and late-round 80/90/100 soak. |', 'README qualification highlight')
    text = replace_once(text, '### Late-Round Performance & Battle Density Reforge v13.7 — in development', '### Late-Round Performance & Battle Density Reforge v13.7 — qualified', 'README v13.7 heading')
    old_body = 'v13.7 targets late-campaign frame-pressure and presentation churn without lowering combat density. A deterministic Normal / Dense / Critical pressure profile will combine round band, live registered actors, active explosion pressure and the existing `WarfarePerformanceGovernor` tier, then tighten only optional presentation budgets. Projectile/Health/spawn/movement/targeting authority and enemy counts remain unchanged; the exact Windows gate must prove the same packaged EXE still passes v13.6+ regressions and rounds 80/90/100 soak before any v13.7 checkbox can close.'
    new_body = 'v13.7 targets late-campaign frame-pressure and presentation churn without lowering combat density. Its deterministic Normal / Dense / Critical pressure profile combines round band, live registered actors, active explosion pressure and the existing `WarfarePerformanceGovernor` tier, then tightens only optional presentation budgets. Projectile/Health/spawn/movement/targeting authority and enemy counts remain unchanged. Exact candidate `dc646fc60be572e625f5ea310058aab10d2f26da` passed Windows qualification run `35393272731`, including production authored-audio preflight, v13.7 performance smoke, v13.6/v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak on the same packaged executable.'
    text = replace_once(text, old_body, new_body, 'README v13.7 body')
    text = replace_once(text, 'The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **439 / 447 (98.2%) — V13.7 IN DEVELOPMENT**.', 'The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **447 / 447 (100.0%) — V13.7 QUALIFIED**.', 'README roadmap summary')

    v135 = 'The qualified v13.5 candidate is `9dca37bb93d10fda44bcfb17b82a8ea0f92f065e`; Windows qualification run `35371602843` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.5 Battlefield Weather smoke, v13.4 tactical-terrain regression, v13.3 battlefield-cohesion regression and rounds 80/90/100 soak.'
    v137 = v135 + '\n\nThe qualified v13.6 candidate is `6509b163ab0fa2f29d88f2f9c06188f7a1de1bdb`; Windows qualification run `35388766882` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.6 sensor-fusion smoke, v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak.\n\nThe qualified v13.7 candidate is `dc646fc60be572e625f5ea310058aab10d2f26da`; Windows qualification run `35393272731` completed successfully. The same packaged EXE passed production authored-audio preflight, v13.7 late-round-performance smoke, v13.6/v13.5/v13.4/v13.3 regressions and rounds 80/90/100 soak. Its deterministic candidate ZIP SHA-256 is `0328a6613dcc64dffb1f675282e27a798b0f3abcb561e13423b7b6de3d75c25f`.'
    text = replace_once(text, v135, v137, 'README qualification evidence')
    text = replace_once(text, '- **Development milestone:** v13.7 is in development on `dev-v13-7`; v13.6 remains the latest qualified development milestone and neither state is automatically a new public release.', '- **Development milestone:** v13.7 is qualified on `dev-v13-7`; this qualification does not automatically create or replace a public release.', 'README release status')
    return text


def write_evidence(finalizer_run_id: str) -> None:
    EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
    EVIDENCE.write_text(f'''# v13.7 Windows Qualification Evidence\n\nStatus: **QUALIFIED**\n\nThis document records the exact-candidate evidence used to close the v13.7 Late-Round Performance & Battle Density Reforge roadmap scope. Milestone qualification is not the same as public GitHub Release readiness.\n\n| Field | Verified value |\n|---|---|\n| Candidate commit | `{CANDIDATE_SHA}` |\n| Qualification workflow | `Late-Round Performance v13.7 Windows Qualification Gate` |\n| Qualification run | `{QUAL_RUN_ID}` — SUCCESS |\n| Unity | `6000.3.17f1` |\n| Platform | Windows x64 |\n| Candidate ZIP SHA-256 | `{INNER_ZIP_SHA256}` |\n| Candidate ZIP bytes | `{INNER_ZIP_BYTES}` |\n| GitHub candidate artifact ID | `{ARTIFACT_ID}` |\n| GitHub artifact digest | `{ARTIFACT_DIGEST}` |\n| Finalizer run | `{finalizer_run_id}` |\n\n## Same-executable qualification matrix\n\nThe exact packaged candidate passed, in order, the production authored-audio presentation preflight, v13.7 late-round performance smoke, v13.6 sensor-fusion regression, v13.5 battlefield-weather regression, v13.4 tactical-terrain regression, v13.3 battlefield-cohesion regression and the rounds 80/90/100 soak regression.\n\nThe v13.7 performance layer changes optional presentation budgets only. The qualification does not authorize hidden enemy deletion, spawn-count reduction, alternate movement/targeting/damage authorities or suppression of gameplay events.\n''', encoding='utf-8')


def main() -> int:
    roadmap = ROADMAP.read_text(encoding='utf-8')
    readme = README.read_text(encoding='utf-8')
    if 'V13.7%20QUALIFIED' in roadmap and '| **447** | **0** | **447** | **100.0%** |' in roadmap:
        print('v13.7 roadmap already finalized')
        return 0

    ROADMAP.write_text(finalize_roadmap(roadmap), encoding='utf-8')
    README.write_text(finalize_readme(readme), encoding='utf-8')
    write_evidence(os.environ.get('GITHUB_RUN_ID', 'local'))
    print('finalized v13.7 roadmap scope: 447/447 (100.0%) V13.7 QUALIFIED')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
