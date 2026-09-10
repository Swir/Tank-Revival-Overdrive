# TANK REVIVAL: OVERDRIVE

Modern 2D tank combat inspired by the feel of classic 8-bit console tank games, rebuilt from scratch for Unity 6.

## Target

- Windows 10/11 x64
- 100 progressively harder rounds
- boss fight every 10 rounds
- destructible brick walls, steel, water and base defence
- enemy classes: basic, fast, heavy, sniper and boss
- power-ups and score progression
- keyboard controls, with controller support planned

## Current milestone

**v0.1 – playable combat core**

The project deliberately uses procedural placeholder art for the first playable milestone. This lets the gameplay, AI, collision, round progression and Windows build pipeline be validated before final HD/2.5D art is introduced.

## Controls

- WASD / Arrow keys – move
- Space – fire
- P / Escape – pause
- Enter – start from title screen

## Windows releases without installing Unity locally

This repository contains a GitHub Actions workflow that builds `TankRevivalOverdrive.exe` on GitHub and publishes a ready-to-run Windows ZIP to GitHub Releases.

Unity licensing is required by the Unity Editor running in CI. Configure repository secrets described in `.github/workflows/windows-release.yml` before running the release workflow.

## Legal / creative direction

This is an original game inspired by the broad gameplay conventions of classic top-down tank games. Do not add ripped maps, sprites, sounds, trademarks or other copyrighted assets from existing games.
