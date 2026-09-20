#!/usr/bin/env python3
"""Close the source-verified v14.1 observer-hunt presentation deliverable."""
from __future__ import annotations

import re
from pathlib import Path

ROADMAP = Path('ROADMAP.md')
README = Path('README.md')
HEADING = '## v14.1 — Counter-Observation & Hunter-Killer Warfare — IN DEVELOPMENT'
ITEM = 'Readable budgeted observer-hunt presentation'


def once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count == 0 and new in text:
        return text
    if count != 1:
        raise SystemExit(f'v14.1 presentation finalizer FAIL: expected one {label}, found {count}')
    return text.replace(old, new, 1)


def section_bounds(text: str) -> tuple[int, int]:
    start = text.find(HEADING)
    if start < 0:
        raise SystemExit('v14.1 presentation finalizer FAIL: active milestone heading missing')
    body = start + len(HEADING)
    tail = text[body:]
    nxt = re.search(r'^## ', tail, re.M)
    return body, body + (nxt.start() if nxt else len(tail))


def close_item(text: str) -> str:
    start, end = section_bounds(text)
    section = text[start:end]
    pattern = rf'^- \[ \] (\*\*{re.escape(ITEM)}\*\*.*)$'
    section2, count = re.subn(pattern, r'- [x] \1', section, count=1, flags=re.M)
    if count == 0 and re.search(rf'^- \[x\] \*\*{re.escape(ITEM)}\*\*', section, re.M):
        return text
    if count != 1:
        raise SystemExit('v14.1 presentation finalizer FAIL: presentation item missing')
    return text[:start] + section2 + text[end:]


def update_roadmap(text: str) -> str:
    completed = len(re.findall(r'^- \[x\] ', text, re.M))
    opened = len(re.findall(r'^- \[ \] ', text, re.M))
    if (completed, opened) == (477, 2):
        return text
    if (completed, opened) != (476, 3):
        raise SystemExit(f'v14.1 presentation finalizer FAIL: expected 476/3 baseline, got {completed}/{opened}')
    text = close_item(text)
    for old, new, label in (
        ('ROADMAP-99.4%25-blue', 'ROADMAP-99.6%25-blue', 'ROADMAP badge'),
        ('DONE-476%2F479-1f6feb', 'DONE-477%2F479-1f6feb', 'DONE badge'),
        ('| **476** | **3** | **479** | **99.4%** |', '| **477** | **2** | **479** | **99.6%** |', 'progress table'),
    ):
        text = once(text, old, new, label)
    return text


def update_readme(text: str) -> str:
    if '**477 / 479 completed (99.6%)**' not in text:
        for old, new, label in (
            ('Roadmap-99.4%25%20V14.1%20In%20Development', 'Roadmap-99.6%25%20V14.1%20In%20Development', 'README roadmap badge'),
            ('| Roadmap | **476 / 479 completed (99.4%)** — authoritative `ROADMAP.md` scope |', '| Roadmap | **477 / 479 completed (99.6%)** — authoritative `ROADMAP.md` scope |', 'README roadmap row'),
            ('The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **476 / 479 (99.4%) — V14.1 IN DEVELOPMENT**.', 'The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **477 / 479 (99.6%) — V14.1 IN DEVELOPMENT**.', 'roadmap narrative'),
            ('| 🔭 **Counter-observation warfare v14.1 — in development** | Source-verified observer hunts now publish one bounded hunter-killer target, confirm success only through canonical Health/runtime-registry lifecycle, and scale designation/disruption through existing terrain exposure, cohesion and command discipline without granting observer immunity. |', '| 🔭 **Counter-observation warfare v14.1 — in development** | Source-verified observer hunts publish one bounded hunter-killer target, confirm success only through canonical Health/runtime-registry lifecycle, scale designation/disruption through terrain/cohesion/command resilience, and add fixed-cap world-space target emphasis through the existing late-round FX budget. |', 'v14.1 highlight'),
        ):
            text = once(text, old, new, label)
    return text


def verify(roadmap: str, readme: str) -> None:
    completed = len(re.findall(r'^- \[x\] ', roadmap, re.M))
    opened = len(re.findall(r'^- \[ \] ', roadmap, re.M))
    if (completed, opened) != (477, 2):
        raise SystemExit(f'v14.1 presentation finalizer FAIL: checklist {completed}/{opened}, expected 477/2')
    if '| **477** | **2** | **479** | **99.6%** |' not in roadmap:
        raise SystemExit('v14.1 presentation finalizer FAIL: roadmap table stale')
    if 'STATUS-V14.1%20IN%20DEVELOPMENT-blue' not in roadmap:
        raise SystemExit('v14.1 presentation finalizer FAIL: status must remain IN DEVELOPMENT')
    start, end = section_bounds(roadmap)
    section = roadmap[start:end]
    if not re.search(rf'^- \[x\] \*\*{re.escape(ITEM)}\*\*', section, re.M):
        raise SystemExit('v14.1 presentation finalizer FAIL: presentation item still open')
    for label in ('Deterministic runtime, authority and performance contracts', 'Exact-SHA Windows qualification'):
        if not re.search(rf'^- \[ \] \*\*{re.escape(label)}\*\*', section, re.M):
            raise SystemExit('v14.1 presentation finalizer FAIL: later gate must remain open ' + label)
    if '**477 / 479 completed (99.6%)**' not in readme:
        raise SystemExit('v14.1 presentation finalizer FAIL: README progress stale')
    if 'Latest qualified milestone | **v14.0**' not in readme:
        raise SystemExit('v14.1 presentation finalizer FAIL: v14.0 must remain latest qualified milestone')


def main() -> None:
    roadmap = ROADMAP.read_text(encoding='utf-8')
    readme = README.read_text(encoding='utf-8')
    roadmap2 = update_roadmap(roadmap)
    readme2 = update_readme(readme)
    verify(roadmap2, readme2)
    ROADMAP.write_text(roadmap2, encoding='utf-8')
    README.write_text(readme2, encoding='utf-8')
    print('v14.1 presentation progress finalizer: 477/479 (99.6%)')


if __name__ == '__main__':
    main()
