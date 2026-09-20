#!/usr/bin/env python3
"""Open v14.2 deception / emission-discipline counter-recon scope truthfully."""
from __future__ import annotations
import re
from pathlib import Path

ROADMAP=Path('ROADMAP.md'); README=Path('README.md'); VERSION=Path('VERSION')
HEADING='## v14.2 — Deception, Emission Discipline & Shoot-and-Scoot Warfare — IN DEVELOPMENT'
SCOPE=f'''\n\n{HEADING}\n- [ ] **Bounded decoy and false-emission authority** — add deterministic finite-charge decoy signatures that can enter enemy hunter-killer evaluation without spawning actors, applying damage or replacing canonical observer targeting.\n- [ ] **Emission-control silent-relocation state** — let the player trade sensor/designation throughput for a finite low-emission relocation window with explicit cooldown, exposure floors and no scripted immunity.\n- [ ] **Shoot-and-scoot reacquisition timing bridge** — connect verified observer disruption, handoff and relocation to a bounded counter-battery reacquisition delay while `CounterBatteryDirectorV140` remains fire-control authority.\n- [ ] **Enemy deception-break adaptation counterplay** — repeated decoy use builds bounded deterministic suspicion that reduces false-signature value and prevents permanent AI lockout.\n- [ ] **Terrain, cohesion and command resilience integration** — scale decoy credibility, silent-move exposure and reacquisition timing from qualified terrain, cohesion and command posture without parallel movement/AI authority.\n- [ ] **Readable budgeted deception presentation** — expose DECOY / EMCON / RELOCATING / REACQUIRING cues through fixed-cap HUD/world/audio feedback under existing mass-battle FX budgets.\n- [ ] **Deterministic runtime, authority and performance contracts** — verify fixed decoy caps, finite cooldowns, deterministic adaptation, no hot-path scene scans and unchanged Health/Projectile/TankGame/EnemyTank ownership.\n- [ ] **Exact-SHA Windows qualification** — package one Windows x64 candidate, pass v14.2 deception/EMCON smoke, rerun v14.1/v14.0/v13.9/v13.8 regressions plus rounds 80/90/100 soak on the same executable, then finalize checklist/progress SVGs.\n'''

def once(text, old, new, label):
    c=text.count(old)
    if c!=1: raise SystemExit(f'v14.2 scope opener FAIL: expected one {label}, found {c}')
    return text.replace(old,new,1)

def main():
    r=ROADMAP.read_text(encoding='utf-8'); m=README.read_text(encoding='utf-8')
    if HEADING not in r:
        if '## v14.1 — Counter-Observation & Hunter-Killer Warfare — QUALIFIED' not in r: raise SystemExit('v14.2 scope opener FAIL: v14.1 qualified anchor missing')
        if len(re.findall(r'^- \[x\] ',r,re.M))!=479 or re.search(r'^- \[ \] ',r,re.M): raise SystemExit('v14.2 scope opener FAIL: expected 479/479 baseline')
        for old,new,label in (
            ('badge.svg?branch=dev-v14-1','badge.svg?branch=dev-v14-2','CI branch badge'),
            ('ROADMAP-100.0%25-brightgreen','ROADMAP-98.4%25-blue','ROADMAP badge'),
            ('DONE-479%2F479-brightgreen','DONE-479%2F487-1f6feb','DONE badge'),
            ('STATUS-V14.1%20QUALIFIED-brightgreen','STATUS-V14.2%20IN%20DEVELOPMENT-blue','STATUS badge'),
            ('| **479** | **0** | **479** | **100.0%** |','| **479** | **8** | **487** | **98.4%** |','progress table')):
            r=once(r,old,new,label)
        r=r.rstrip()+SCOPE+'\n'
    if '**V14.2 IN DEVELOPMENT** on `dev-v14-2`' not in m:
        for old,new,label in (
            ('Roadmap-100.0%25%20V14.1%20Qualified','Roadmap-98.4%25%20V14.2%20In%20Development','README roadmap badge'),
            ('| Development milestone | **V14.1 QUALIFIED** on `dev-v14-1` |','| Development milestone | **V14.2 IN DEVELOPMENT** on `dev-v14-2` |','development row'),
            ('| Roadmap | **479 / 479 completed (100.0%)** — authoritative `ROADMAP.md` scope |','| Roadmap | **479 / 487 completed (98.4%)** — authoritative `ROADMAP.md` scope |','roadmap row')):
            m=once(m,old,new,label)
        anchor='| 🔭 **Counter-observation warfare v14.1 — qualified** | Verified sensor contact can expose one bounded observer-class hunter-killer target; canonical kill confirmation creates only a short counter-battery disruption window, while terrain/cohesion/command resilience and fixed-cap presentation preserve combat authority and late-round budgets. |'
        addition='\n| 🥷 **Counter-recon deception v14.2 — in development** | Finite decoys, emission discipline and shoot-and-scoot timing will let the player misdirect hostile observation without hidden immunity or parallel combat authority. |'
        m=once(m,anchor,anchor+addition,'v14.1 highlight')
        m=once(m,'The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **479 / 479 (100.0%) — V14.1 QUALIFIED**.','The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **479 / 487 (98.4%) — V14.2 IN DEVELOPMENT**.','roadmap narrative')
        m=once(m,'- **Development milestone:** v14.1 is qualified on `dev-v14-1`; this qualified development scope does not automatically create or replace a public release.','- **Development milestone:** v14.2 is in development on `dev-v14-2`; v14.1 remains the latest qualified milestone and this development scope does not automatically create or replace a public release.','release milestone')
        sec='''\n### Deception, Emission Discipline & Shoot-and-Scoot Warfare v14.2 — in development\n\nv14.2 adds finite counter-recon deception above the qualified v14.1 observer-hunt layer. Decoys and emission-control windows may alter only exposure, designation quality and reacquisition timing; canonical `Health`, `EnemyTank`, `Projectile`, `TankGame`, `CounterBatteryDirectorV140` and `CounterObservationDirectorV141` remain authoritative for combat outcomes.\n\n'''
        sk=m.find('\n## 🔎 Search Keywords')
        if sk<0: raise SystemExit('v14.2 scope opener FAIL: Search Keywords anchor missing')
        m=m[:sk].rstrip()+'\n\n'+sec+m[sk:].lstrip('\n')
    completed=len(re.findall(r'^- \[x\] ',r,re.M)); opened=len(re.findall(r'^- \[ \] ',r,re.M))
    if (completed,opened)!=(479,8): raise SystemExit(f'v14.2 scope opener FAIL: checklist {completed}/{opened}')
    for token in ('<!-- SWIR-ROADMAP-STANDARD:v1 -->','assets/readme/progress-mini.svg','| **479** | **8** | **487** | **98.4%** |',HEADING):
        if token not in r: raise SystemExit('v14.2 scope opener FAIL: ROADMAP missing '+token)
    for token in ('<!-- SWIR-README-STANDARD:v2 -->','assets/readme/progress-card.svg','## 🔎 Search Keywords','**V14.2 IN DEVELOPMENT** on `dev-v14-2`','**479 / 487 completed (98.4%)**','Latest qualified milestone | **v14.1**'):
        if token not in m: raise SystemExit('v14.2 scope opener FAIL: README missing '+token)
    ROADMAP.write_text(r,encoding='utf-8'); README.write_text(m,encoding='utf-8'); VERSION.write_text('v14.2.0-dev\n',encoding='utf-8')
    print('v14.2 scope opener: 479/487 (98.4%)')

if __name__=='__main__': main()
