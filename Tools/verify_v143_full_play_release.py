#!/usr/bin/env python3
from pathlib import Path
import re
import sys

def require(text: str, token: str, label: str) -> None:
    if token not in text:
        raise SystemExit(f"v14.3 full-play release FAIL: {label}: missing {token!r}")

def main() -> int:
    version=Path("VERSION").read_text(encoding="utf-8").strip()
    if version!="v14.3.0": raise SystemExit(f"v14.3 full-play release FAIL: VERSION={version!r}")
    roadmap=Path("ROADMAP.md").read_text(encoding="utf-8")
    done=len(re.findall(r'^- \[x\] ',roadmap,re.M)); open_=len(re.findall(r'^- \[ \] ',roadmap,re.M))
    if (done,open_)!=(495,0): raise SystemExit(f"v14.3 full-play release FAIL: roadmap {done}/{done+open_}")
    require(roadmap,"## v14.3 — Smoke Screening & Break-Contact Warfare — QUALIFIED","qualified milestone")

    build=Path("Assets/Editor/CIBuild.cs").read_text(encoding="utf-8")
    player=Path("Assets/Scripts/PlayerTank.cs").read_text(encoding="utf-8")
    guard=Path("Assets/Scripts/FullPlayReleaseGuardV142.cs").read_text(encoding="utf-8")
    fullplay=Path("Assets/Scripts/FullPlayReleaseCISmokeProbeV142.cs").read_text(encoding="utf-8")
    smoke=Path("Assets/Scripts/BattlefieldSmokeCISmokeProbeV143.cs").read_text(encoding="utf-8")
    package=Path(".github/scripts/package_v143_full_play.py").read_text(encoding="utf-8")
    workflow=Path(".github/workflows/v143-full-play-release.yml").read_text(encoding="utf-8")

    for token in ("FullScreenMode.FullScreenWindow","BuildDemoCandidate","FULL-PLAY RELEASE CANDIDATE",
                  "v14.3/v14.2/v14.1/v14.0/v13.9/v13.8","gamepad left stick"):
        require(build,token,"release candidate build")
    for token in ("Input.GetJoystickNames()",'ReadLegacyAxis("Horizontal")','ReadLegacyAxis("Vertical")',
                  "KeyCode.JoystickButton0","KeyCode.JoystickButton4","KeyCode.JoystickButton5"):
        require(player,token,"gamepad gameplay input")
    for token in ("Display.main.systemWidth","CleanPresentationDepth","RenderTexture","DrawReleaseCombatHud",
                  "KeyCode.F9","Application.isBatchMode"):
        require(guard,token,"clean fullscreen presentation")
    for token in ("menu->play->pause->resume flow","TryCaptureScreenshot","legacy development shell remained visible during gameplay"):
        require(fullplay,token,"full-play runtime evidence")
    for token in ("-tr-v143-smoke","V14_3_SMOKE_OK.txt","MaxActiveSmokeZones == 1","MaxSmokeChargesPerRound == 2"):
        require(smoke,token,"v14.3 smoke warfare runtime evidence")
    require(package,'NOTICES_NAME = "THIRD_PARTY_NOTICES.txt"',"third-party notices")
    require(package,'"schema":"tank-revival-v14.3-full-play/v1"',"v14.3 provenance schema")
    require(workflow,"round 80/90/100 soak","late-round release soak")
    require(workflow,"post-release","public artifact verification")
    require(workflow,"V14_3_FULL_PLAY_SCREENSHOT.png","v14.3 visual evidence")
    print("v14.3 full-play release source qualification PASS")
    return 0

if __name__=="__main__":
    sys.exit(main())
