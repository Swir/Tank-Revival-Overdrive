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
    require(guard, "GUI.depth = -3000", "development-label cleanup overlay")

    require(smoke, "menu->play->pause->resume flow", "packaged player-flow smoke")
    require(smoke, "TryCaptureScreenshot", "visual evidence capture entrypoint")
    require(smoke, "UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule", "module-safe screenshot lookup")
    require(smoke, 'GetMethod(\n                "CaptureScreenshot"', "runtime screenshot capture method")
    require(smoke, "fullscreen screenshot capture unavailable", "fail-closed screenshot evidence")
    require(smoke, "legacy development shell remained visible during gameplay", "clean-HUD runtime assertion")

    print("v14.2 full-play release source qualification PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
