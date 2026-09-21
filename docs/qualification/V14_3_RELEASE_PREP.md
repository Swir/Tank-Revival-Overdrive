# v14.3 Full-Play Release Preparation

- Scope is frozen at ROADMAP `495/495 = 100.0%`; no v14.4 or additional gameplay denominator is introduced by this release branch.
- Verified predecessor baseline: public `v14.2.0` Full-Play Beta and the qualified `dev-v14-2` merge head `0200dd011a7794f8d1443143d79ce2bcd051d7a0` are ancestors of this candidate line.
- Candidate requirements: self-contained Windows x64 package, exact SHA/provenance/SHA-256/notices, fullscreen-first clean HUD, menu/play/pause/resume player flow, production audio, v14.3 smoke/break-contact runtime gate, v14.2-v13.8 regression smokes, rounds 80/90/100 soak, visual screenshot evidence and downloaded-public-artifact post-release smoke.
- Public release is blocked until every job in `.github/workflows/v143-full-play-release.yml` succeeds on the same candidate SHA.
