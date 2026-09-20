#!/usr/bin/env python3
"""Close the first three source-verified v14.1 gameplay deliverables."""
from __future__ import annotations
import re
from pathlib import Path

ROADMAP = Path('ROADMAP.md')
README = Path('README.md')
HEADING = '## v14.1 — Counter-Observation & Hunter-Killer Warfare — IN DEVELOPMENT'
PHASE = (
    'Bounded counter-observation hunt authority',
    'Verified-contact observer designation across 100 rounds',
    'Counter-battery acquisition suppression bridge',
)


def once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count == 0 and new in text:
        return text
    if count != 1:
        raise SystemExit(f'v14.1 phase finalizer FAIL: expected one {label}, found {count}')
    return text.replace(old, new, 1)


def section_bounds(text: str) -> tuple[int, int]:
    start = text.find(HEADING)
    if start < 0:
        raise SystemExit('v14.1 phase finalizer FAIL: active milestone heading missing')
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
        raise SystemExit('v14.1 phase finalizer FAIL: open item missing ' + label)
    return text[:start] + section2 + text[end:]


def update_roadmap(text: str) -> str:
    completed = len(re.findall(r'^- \[x\] ', text, re.M))
    opened = len(re.findall(r'^- \[ \] ', text, re.M))
    if (completed, opened) == (474, 5):
        return text
    if (completed, opened) != (471, 8):
        raise SystemExit(f'v14.1 phase finalizer FAIL: expected 471/8 baseline, got {completed}/{opened}')
    for label in PHASE:
        text = close_item(text, label)
    for old, new, label in (
        ('ROADMAP-98.3%25-blue', 'ROADMAP-99.0%25-blue', 'ROADMAP badge'),
        ('DONE-471%2F479-1f6feb', 'DONE-474%2F479-1f6feb', 'DONE badge'),
        ('| **471** | **8** | **479** | **98.3%** |', '| **474** | **5** | **479** | **99.0%** |', 'progress table'),
    ):
        text = once(text, old, new, label)
    return text


def update_readme(text: str) -> str:
    if '**474 / 479 completed (99.0%)**' not in text:
        for old, new, label in (
            ('Roadmap-98.3%25%20V14.1%20In%20Development', 'Roadmap-99.0%25%20V14.1%20In%20Development', 'README roadmap badge'),
            ('| Roadmap | **471 / 479 completed (98.3%)** — authoritative `ROADMAP.md` scope |', '| Roadmap | **474 / 479 completed (99.0%)** — authoritative `ROADMAP.md` scope |', 'README roadmap row'),
            ('The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **471 / 471 (100.0%) — V14.0 QUALIFIED**.', 'The authoritative roadmap is [`ROADMAP.md`](ROADMAP.md). The current scoped state is **474 / 479 (99.0%) — V14.1 IN DEVELOPMENT**.', 'roadmap narrative'),
            ('- **Development milestone:** v14.0 is qualified on `dev-v14-0`; this qualified development scope does not automatically create or replace a public release.', '- **Development milestone:** v14.1 is in development on `dev-v14-1`; v14.0 remains the latest qualified milestone and this development scope does not automatically create or replace a public release.', 'release milestone'),
            ('| 🔭 **Counter-observation warfare v14.1 — in development** | Sensor-confirmed observer hunts will let the player identify and break the enemy fire-control network while canonical Health/EnemyTank lifecycle remains authoritative. |', '| 🔭 **Counter-observation warfare v14.1 — in development** | Source-verified observer hunts use tracked/verified sensor evidence, canonical Health kill confirmation and a bounded network-break multiplier that reduces v14.0 acquisition without deleting shells or granting immunity. |', 'v14.1 highlight'),
        ):
            text = once(text, old, new, label)
    return text


def verify(roadmap: str, readme: str) -> None:
    completed = len(re.findall(r'^- \[x\] ', roadmap, re.M))
    opened = len(re.findall(r'^- \[ \] ', roadmap, re.M))
    if (completed, opened) != (474, 5):
        raise SystemExit(f'v14.1 phase finalizer FAIL: checklist {completed}/{opened}, expected 474/5')
    if '| **474** | **5** | **479** | **99.0%** |' not in roadmap:
        raise SystemExit('v14.1 phase finalizer FAIL: roadmap table stale')
    if 'STATUS-V14.1%20IN%20DEVELOPMENT-blue' not in roadmap:
        raise SystemExit('v14.1 phase finalizer FAIL: status must remain IN DEVELOPMENT')
    start, end = section_bounds(roadmap)
    section = roadmap[start:end]
    for label in PHASE:
        if not re.search(rf'^- \[x\] \*\*{re.escape(label)}\*\*', section, re.M):
            raise SystemExit('v14.1 phase finalizer FAIL: checked item missing ' + label)
    if '**474 / 479 completed (99.0%)**' not in readme:
        raise SystemExit('v14.1 phase finalizer FAIL: README progress stale')
    if 'Latest qualified milestone | **v14.0**' not in readme:
        raise SystemExit('v14.1 phase finalizer FAIL: v14.0 must remain latest qualified milestone')


def main() -> None:
    roadmap = ROADMAP.read_text(encoding='utf-8')
    readme = README.read_text(encoding='utf-8')
    roadmap2 = update_roadmap(roadmap)
    readme2 = update_readme(readme)
    verify(roadmap2, readme2)
    ROADMAP.write_text(roadmap2, encoding='utf-8')
    README.write_text(readme2, encoding='utf-8')
    print('v14.1 source-progress finalizer: 474/479 (99.0%)')


if __name__ == '__main__':
    main()
