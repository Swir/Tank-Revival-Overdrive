#!/usr/bin/env python3
"""Close source-verified v14.1 hunter-killer handoff and resilience deliverables."""
from __future__ import annotations
import re
from pathlib import Path

ROADMAP = Path('ROADMAP.md')
README = Path('README.md')
HEADING = '## v14.1 — Counter-Observation & Hunter-Killer Warfare — IN DEVELOPMENT'
PHASE = (
    'Hunter-killer target handoff and kill confirmation',
    'Terrain, cohesion and command resilience',
)


def once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count == 0 and new in text:
        return text
    if count != 1:
        raise SystemExit(f'v14.1 phase2 finalizer FAIL: expected one {label}, found {count}')
    return text.replace(old, new, 1)


def section_bounds(text: str) -> tuple[int, int]:
    start = text.find(HEADING)
    if start < 0:
        raise SystemExit('v14.1 phase2 finalizer FAIL: active milestone heading missing')
    body = start + len(HEADING)
    tail = text[body:]
    nxt = re.search(r'^## ', tail, re.M)
    return body, body + (nxt.start() if nxt else len(tail))


def close_item(text: str, label: str) -> str:
    start, end = section_bounds(text)
    section = text[start:end]
    pattern = rf'^- \[ \] (\*\*{re.escape(label)}\*\*.*)$'
    section2, count = re.subn(pattern, r'- [x] \1', section, count=1, flags=re.M)
    if count == 0 and re.search(rf'^- \[x\] \*\*{re.escape(label)}\*\*', section, re.M):
        return text
    if count != 1:
        raise SystemExit('v14.1 phase2 finalizer FAIL: open item missing ' + label)
    return text[:start] + section2 + text[end:]


def update_roadmap(text: str) -> str:
    completed = len(re.findall(r'^- \[x\] ', text, re.M))
    opened = len(re.findall(r'^- \[ \] ', text, re.M))
    if (completed, opened) == (476, 3):
        return text
    if (completed, opened) != (474, 5):
        raise SystemExit(f'v14.1 phase2 finalizer FAIL: expected 474/5 baseline, got {completed}/{opened}')
    for label in PHASE:
        text = close_item(text, label)
    for old, new, label in (
        ('ROADMAP-99.0%25-blue', 'ROADMAP-99.4%25-blue', 'ROADMAP badge'),
        ('DONE-474%2F479-1f6feb', 'DONE-476%2F479-1f6feb', 'DONE badge'),
        ('| **474** | **5** | **479** | **99.0%** |', '| **476** | **3** | **479** | **99.4%** |', 'progress table'),
    ):
        text = once(text, old, new, label)
    return text


def update_readme(text: str) -> str:
    if '**476 / 479 completed (99.4%)**' not in text:
        for old, new, label in (
            ('Roadmap-99.0%25%20V14.1%20In%20Development', 'Roadmap-99.4%25%20V14.1%20In%20Development', 'README roadmap badge'),
            ('| Roadmap | **474 / 479 completed (99.0%)** — authoritative `ROADMAP.md` scope |', '| Roadmap | **476 / 479 completed (99.4%)** — authoritative `ROADMAP.md` scope |', 'README roadmap row'),
            ('The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **474 / 479 (99.0%) — V14.1 IN DEVELOPMENT**.', 'The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **476 / 479 (99.4%) — V14.1 IN DEVELOPMENT**.', 'roadmap narrative'),
            ('| 🔭 **Counter-observation warfare v14.1 — in development** | Source-verified observer hunts use tracked/verified sensor evidence, canonical Health kill confirmation and a bounded network-break multiplier that reduces v14.0 acquisition without deleting shells or granting immunity. |', '| 🔭 **Counter-observation warfare v14.1 — in development** | Source-verified observer hunts now publish one bounded hunter-killer target, confirm success only through canonical Health/runtime-registry lifecycle, and scale designation/disruption through existing terrain exposure, cohesion and command discipline without granting observer immunity. |', 'v14.1 highlight'),
        ):
            text = once(text, old, new, label)
    return text


def verify(roadmap: str, readme: str) -> None:
    completed = len(re.findall(r'^- \[x\] ', roadmap, re.M))
    opened = len(re.findall(r'^- \[ \] ', roadmap, re.M))
    if (completed, opened) != (476, 3):
        raise SystemExit(f'v14.1 phase2 finalizer FAIL: checklist {completed}/{opened}, expected 476/3')
    if '| **476** | **3** | **479** | **99.4%** |' not in roadmap:
        raise SystemExit('v14.1 phase2 finalizer FAIL: roadmap table stale')
    if 'STATUS-V14.1%20IN%20DEVELOPMENT-blue' not in roadmap:
        raise SystemExit('v14.1 phase2 finalizer FAIL: status must remain IN DEVELOPMENT')
    start, end = section_bounds(roadmap)
    section = roadmap[start:end]
    for label in PHASE:
        if not re.search(rf'^- \[x\] \*\*{re.escape(label)}\*\*', section, re.M):
            raise SystemExit('v14.1 phase2 finalizer FAIL: checked item missing ' + label)
    for label in ('Readable budgeted observer-hunt presentation', 'Deterministic runtime, authority and performance contracts', 'Exact-SHA Windows qualification'):
        if not re.search(rf'^- \[ \] \*\*{re.escape(label)}\*\*', section, re.M):
            raise SystemExit('v14.1 phase2 finalizer FAIL: later gate must remain open ' + label)
    if '**476 / 479 completed (99.4%)**' not in readme:
        raise SystemExit('v14.1 phase2 finalizer FAIL: README progress stale')
    if 'Latest qualified milestone | **v14.0**' not in readme:
        raise SystemExit('v14.1 phase2 finalizer FAIL: v14.0 must remain latest qualified milestone')


def main() -> None:
    roadmap = ROADMAP.read_text(encoding='utf-8')
    readme = README.read_text(encoding='utf-8')
    roadmap2 = update_roadmap(roadmap)
    readme2 = update_readme(readme)
    verify(roadmap2, readme2)
    ROADMAP.write_text(roadmap2, encoding='utf-8')
    README.write_text(readme2, encoding='utf-8')
    print('v14.1 phase2 progress finalizer: 476/479 (99.4%)')


if __name__ == '__main__':
    main()
