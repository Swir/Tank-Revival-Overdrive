#!/usr/bin/env python3
"""Open v14.1 Counter-Observation & Hunter-Killer Warfare scope truthfully."""
from __future__ import annotations
import re
from pathlib import Path

ROADMAP=Path('ROADMAP.md'); README=Path('README.md'); VERSION=Path('VERSION')
HEADING='## v14.1 — Counter-Observation & Hunter-Killer Warfare — IN DEVELOPMENT'
SCOPE=f'''\n\n{HEADING}\n- [ ] **Bounded counter-observation hunt authority** — add a deterministic `CounterObservationDirectorV141` Idle / Searching / Designated / NetworkBroken / Cooldown state machine over existing registered enemies without spawning, moving, damaging or despawning actors.\n- [ ] **Verified-contact observer designation across 100 rounds** — select only eligible Sniper / Siege / Elite / Supply observers from the fixed runtime registry and require v13.6 tracked/verified sensor evidence plus finite sweep windows before exposing a hunter-killer target.\n- [ ] **Counter-battery acquisition suppression bridge** — let verified observer disruption and canonical observer death reduce v14.0 acquisition strength for a short bounded window without deleting shells, granting immunity or bypassing CounterBatteryDirectorV140 authority.\n- [ ] **Hunter-killer target handoff and kill confirmation** — publish one bounded high-value observer target to existing HUD/presentation and resolve success only when canonical `Health` / runtime-registry lifecycle removes that actor.\n- [ ] **Terrain, cohesion and command resilience** — scale designation time and disruption duration from v13.4 terrain exposure plus v13.3 cohesion / v13.2 command posture so disciplined observers resist but never become immune.\n- [ ] **Readable budgeted observer-hunt presentation** — expose SEARCH / DESIGNATED / NETWORK BROKEN / COOLDOWN cues, target class/range and counter-battery pressure with strict one-target world emphasis and late-round presentation budgets.\n- [ ] **Deterministic runtime, authority and performance contracts** — verify fixed hostile/observer caps, sensor thresholds, lifecycle cleanup, acquisition floors, no hot-path scene scans and unchanged Health/Projectile/TankGame/EnemyTank ownership.\n- [ ] **Exact-SHA Windows qualification** — package one Windows x64 candidate, pass v14.1 observer-hunt smoke, rerun v14.0/v13.9/v13.8/v13.7 regressions plus rounds 80/90/100 soak on the same executable, then finalize checklist/progress SVGs.\n'''

def once(text, old, new, label):
    c=text.count(old)
    if c!=1: raise SystemExit(f'v14.1 scope opener FAIL: expected one {label}, found {c}')
    return text.replace(old,new,1)

def main():
    r=ROADMAP.read_text(encoding='utf-8'); m=README.read_text(encoding='utf-8')
    if HEADING not in r:
        if '## v14.0 — Counter-Battery & Mobile Fire-Control Warfare — QUALIFIED' not in r: raise SystemExit('v14.1 scope opener FAIL: v14.0 qualified anchor missing')
        if len(re.findall(r'^- \[x\] ',r,re.M))!=471 or re.search(r'^- \[ \] ',r,re.M): raise SystemExit('v14.1 scope opener FAIL: expected 471/471 baseline')
        for old,new,label in (
            ('badge.svg?branch=dev-v14-0','badge.svg?branch=dev-v14-1','CI branch badge'),
            ('ROADMAP-100.0%25-brightgreen','ROADMAP-98.3%25-blue','ROADMAP badge'),
            ('DONE-471%2F471-brightgreen','DONE-471%2F479-1f6feb','DONE badge'),
            ('STATUS-V14.0%20QUALIFIED-brightgreen','STATUS-V14.1%20IN%20DEVELOPMENT-blue','STATUS badge'),
            ('| **471** | **0** | **471** | **100.0%** |','| **471** | **8** | **479** | **98.3%** |','progress table')):
            r=once(r,old,new,label)
        r=r.rstrip()+SCOPE+'\n'
    if '**V14.1 IN DEVELOPMENT** on `dev-v14-1`' not in m:
        for old,new,label in (
            ('Roadmap-100.0%25%20V14.0%20Qualified','Roadmap-98.3%25%20V14.1%20In%20Development','README roadmap badge'),
            ('| Development milestone | **V14.0 QUALIFIED** on `dev-v14-0` |','| Development milestone | **V14.1 IN DEVELOPMENT** on `dev-v14-1` |','development row'),
            ('| Roadmap | **471 / 471 completed (100.0%)** — authoritative `ROADMAP.md` scope |','| Roadmap | **471 / 479 completed (98.3%)** — authoritative `ROADMAP.md` scope |','roadmap row')):
            m=once(m,old,new,label)
        anchor='| 📍 **Counter-battery warfare v14.0 — qualified** | Player fire-support signatures now drive bounded SEARCHING/LOCKED/BARRAGE pressure with terrain/sensor relocation counterplay, capped observer acquisition and canonical enemy projectile execution; one exact Windows candidate passed runtime, historical regression and late-round soak gates. |'
        addition='\n| 🔭 **Counter-observation warfare v14.1 — in development** | Sensor-confirmed observer hunts will let the player identify and break the enemy fire-control network while canonical Health/EnemyTank lifecycle remains authoritative. |'
        m=once(m,anchor,anchor+addition,'v14.0 highlight')
        sec='''\n### Counter-Observation & Hunter-Killer Warfare v14.1 — in development\n\nv14.1 builds active hunter-killer counterplay above the qualified v14.0 counter-battery system. The player must use existing sensor information to identify real observer-class threats, destroy them through canonical combat, and earn only a short bounded disruption of enemy acquisition. The new layer may publish designation/disruption intent but never deals damage, deletes projectiles, teleports enemies or replaces `Health`, `EnemyTank`, `Projectile`, `TankGame` or `CounterBatteryDirectorV140` authority.\n\n'''
        sk=m.find('\n## 🔎 Search Keywords')
        if sk<0: raise SystemExit('v14.1 scope opener FAIL: Search Keywords anchor missing')
        m=m[:sk].rstrip()+'\n\n'+sec+m[sk:].lstrip('\n')
    completed=len(re.findall(r'^- \[x\] ',r,re.M)); opened=len(re.findall(r'^- \[ \] ',r,re.M))
    if (completed,opened)!=(471,8): raise SystemExit(f'v14.1 scope opener FAIL: checklist {completed}/{opened}')
    for token in ('<!-- SWIR-ROADMAP-STANDARD:v1 -->','assets/readme/progress-mini.svg','| **471** | **8** | **479** | **98.3%** |',HEADING):
        if token not in r: raise SystemExit('v14.1 scope opener FAIL: ROADMAP missing '+token)
    for token in ('<!-- SWIR-README-STANDARD:v2 -->','assets/readme/progress-card.svg','## 🔎 Search Keywords','**V14.1 IN DEVELOPMENT** on `dev-v14-1`','**471 / 479 completed (98.3%)**','Latest qualified milestone | **v14.0**'):
        if token not in m: raise SystemExit('v14.1 scope opener FAIL: README missing '+token)
    ROADMAP.write_text(r,encoding='utf-8'); README.write_text(m,encoding='utf-8'); VERSION.write_text('v14.1.0-dev\n',encoding='utf-8')
    print('v14.1 scope opener: 471/479 (98.3%)')

if __name__=='__main__': main()
