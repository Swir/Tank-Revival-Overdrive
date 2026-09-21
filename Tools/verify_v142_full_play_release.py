#!/usr/bin/env python3
from pathlib import Path
import sys


def require(text: str, token: str, label: str) -> None:
    if token not in text:
        raise SystemExit(f"v14.2 full-play release FAIL: {label}: missing {token!r}")


def main() -> int:
    version = Path("VERSION").read_text(encoding="utf-8").strip()
    if version != "v14.2.0":
        raise SystemExit(f"v14.2 full-play release FAIL: VERSION={version!r}")

    build = Path("Assets/Editor/CIBuild.cs").read_text(encoding="utf-8")
    player = Path("Assets/Scripts/PlayerTank.cs").read_text(encoding="utf-8")
    guard = Path("Assets/Scripts/FullPlayReleaseGuardV142.cs").read_text(encoding="utf-8")
    smoke = Path("Assets/Scripts/FullPlayReleaseCISmokeProbeV142.cs").read_text(encoding="utf-8")
    package = Path(".github/scripts/package_v142_full_play.py").read_text(encoding="utf-8")
    workflow = Path(".github/workflows/v142-full-play-release.yml").read_text(encoding="utf-8")

    require(build, "FullScreenMode.FullScreenWindow", "fullscreen candidate build")
    require(build, "BuildDemoCandidate", "non-development candidate entrypoint")
    require(build, "V14.2 FULL-PLAY RELEASE CANDIDATE", "release build identity")
    require(build, "gamepad left stick", "packaged controls")

    for token in ("Input.GetJoystickNames()", 'ReadLegacyAxis("Horizontal")', 'ReadLegacyAxis("Vertical")',
                  "KeyCode.JoystickButton0", "KeyCode.JoystickButton4", "KeyCode.JoystickButton5"):
        require(player, token, "gamepad gameplay input")

    require(guard, "Display.main.systemWidth", "native monitor fullscreen")
    require(guard, "state != \"Playing\"", "clean gameplay shell policy")
    require(guard, "KeyCode.JoystickButton7", "gamepad pause bridge")
    require(guard, "CleanPresentationDepth", "release-clean compositor depth")
    require(guard, "RenderTexture", "release-clean world compositor")
    require(guard, "DrawReleaseCombatHud", "player-critical release HUD")
    require(guard, "KeyCode.F9", "explicit debug-presentation toggle")
    require(guard, "Application.isBatchMode", "headless qualification isolation")

    require(smoke, "menu->play->pause->resume flow", "packaged player-flow smoke")
    require(smoke, "TryCaptureScreenshot", "visual evidence capture entrypoint")
    require(smoke, "UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule", "module-safe screenshot lookup")
    require(smoke, 'GetMethod(\n                "CaptureScreenshot"', "runtime screenshot capture method")
    require(smoke, "fullscreen screenshot capture unavailable", "fail-closed screenshot evidence")
    require(smoke, "legacy development shell remained visible during gameplay", "clean-HUD runtime assertion")

    require(package, 'NOTICES_NAME = "THIRD_PARTY_NOTICES.txt"', "third-party notices filename")
    require(package, "Unity engine/runtime components", "Unity runtime notice")
    require(package, '"third_party_notices": NOTICES_NAME', "notices provenance")
    require(workflow, "THIRD_PARTY_NOTICES.txt", "packaged notices gate")
    require(workflow, "third_party_notices", "notices provenance gate")

    print("v14.2 full-play release source qualification PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
