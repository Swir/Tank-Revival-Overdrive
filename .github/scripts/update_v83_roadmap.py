from pathlib import Path

path = Path('ROADMAP.md')
text = path.read_text(encoding='utf-8')
mode = Path('.roadmap-v83-trigger').read_text(encoding='utf-8').strip().lower()

marker = '<!-- SWIR-ROADMAP-STANDARD:v1 -->'
required = [marker, '<!-- ROADMAP-PROGRESS:START -->', '<!-- ROADMAP-PROGRESS:END -->', '## 📊 Overall progress']
for token in required:
    if token not in text:
        raise SystemExit(f'missing locked roadmap token: {token}')

section = '''## v8.3 — Operation Chains, Branching Objectives & Sector Campaign Arcs — IN PROGRESS
- [ ] Every 10-round sector gains a deterministic three-stage operation chain whose Opening, Exploitation and Resolution rounds carry real outcomes forward instead of resetting strategically after every encounter.
- [ ] Success/neutral/failure is derived from authoritative player and Orzełek health state; the next stage receives bounded encounter relief or escalation through CampaignEncounter without replacing TankGame, Health, Projectile, AI or economy authority.
- [ ] Winning at least two stages closes the sector operation as a victory with existing War Bond rewards, while setbacks preserve enemy initiative; boss rounds remain explicitly protected from chain mutation.
- [ ] Packaged Windows operation-chain gate boots the exact development EXE and verifies all 100 rounds, 30 chain rounds, consequence bounds, outcome rules, boss preservation, integration and v8.3 version identity.

'''

if '## v8.3 — Operation Chains, Branching Objectives & Sector Campaign Arcs' not in text:
    text = text.replace('## Development rules', section + '## Development rules')

start = text.index('<!-- ROADMAP-PROGRESS:START -->')
end = text.index('<!-- ROADMAP-PROGRESS:END -->') + len('<!-- ROADMAP-PROGRESS:END -->')

if mode == 'scope':
    dashboard = '''<!-- ROADMAP-PROGRESS:START -->
<p align="center">
  <a href="https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/dev-windows-build.yml"><img alt="CI" src="https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/dev-windows-build.yml/badge.svg?branch=dev-v8-3"></a>
  <img alt="Roadmap progress" src="https://img.shields.io/badge/ROADMAP-97.5%25-f59e0b?style=for-the-badge">
  <img alt="Completed" src="https://img.shields.io/badge/DONE-154%2F158-1f6feb?style=for-the-badge">
  <img alt="Status" src="https://img.shields.io/badge/STATUS-V8.3%20IN%20PROGRESS-f59e0b?style=for-the-badge">
</p>

## 📊 Overall progress

```text
███████████████████░ 97.5%
```

| ✅ Completed | ⏳ Remaining | 📦 Total | 🎯 Progress |
|---:|---:|---:|---:|
| **154** | **4** | **158** | **97.5%** |

> **Progress rule:** calculate progress from explicit roadmap deliverables only: `[x] / ([x] + [ ])`. Update the checklist first, then badges, numbers, percentage and the 20-segment bar. Never estimate progress from version numbers, commit count, elapsed time or activity.
<!-- ROADMAP-PROGRESS:END -->'''
elif mode == 'complete':
    text = text.replace('## v8.3 — Operation Chains, Branching Objectives & Sector Campaign Arcs — IN PROGRESS', '## v8.3 — Operation Chains, Branching Objectives & Sector Campaign Arcs — COMPLETE')
    v83_start = text.index('## v8.3 — Operation Chains, Branching Objectives & Sector Campaign Arcs')
    rules = text.index('## Development rules', v83_start)
    block = text[v83_start:rules].replace('- [ ]', '- [x]')
    text = text[:v83_start] + block + text[rules:]
    start = text.index('<!-- ROADMAP-PROGRESS:START -->')
    end = text.index('<!-- ROADMAP-PROGRESS:END -->') + len('<!-- ROADMAP-PROGRESS:END -->')
    dashboard = '''<!-- ROADMAP-PROGRESS:START -->
<p align="center">
  <a href="https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/dev-windows-build.yml"><img alt="CI" src="https://github.com/Swir/Tank-Revival-Overdrive/actions/workflows/dev-windows-build.yml/badge.svg?branch=dev-v8-3"></a>
  <img alt="Roadmap progress" src="https://img.shields.io/badge/ROADMAP-100.0%25-22c55e?style=for-the-badge">
  <img alt="Completed" src="https://img.shields.io/badge/DONE-158%2F158-1f6feb?style=for-the-badge">
  <img alt="Status" src="https://img.shields.io/badge/STATUS-V8.3%20QUALIFIED-22c55e?style=for-the-badge">
</p>

## 📊 Overall progress

```text
████████████████████ 100.0%
```

| ✅ Completed | ⏳ Remaining | 📦 Total | 🎯 Progress |
|---:|---:|---:|---:|
| **158** | **0** | **158** | **100.0%** |

> **Progress rule:** calculate progress from explicit roadmap deliverables only: `[x] / ([x] + [ ])`. Update the checklist first, then badges, numbers, percentage and the 20-segment bar. Never estimate progress from version numbers, commit count, elapsed time or activity.
<!-- ROADMAP-PROGRESS:END -->'''
else:
    raise SystemExit(f'unknown roadmap mode: {mode}')

text = text[:start] + dashboard + text[end:]
path.write_text(text, encoding='utf-8')

# Lock verification after mutation.
check = path.read_text(encoding='utf-8')
for token in required:
    if token not in check:
        raise SystemExit(f'roadmap lock lost after update: {token}')
if check.count('█') + check.count('░') < 20:
    raise SystemExit('progress bar is not 20 segments')
