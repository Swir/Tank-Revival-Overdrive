#!/usr/bin/env python3
"""Static checks for the headless-safe Unity bootstrap used by qualification CI."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / "Assets/Scenes/Bootstrap.unity"
CI_BUILD = ROOT / "Assets/Editor/CIBuild.cs"
TANK_GAME = ROOT / "Assets/Scripts/TankGame.cs"

FOLDER_METAS = (
    ROOT / "Assets/Editor.meta",
    ROOT / "Assets/Resources.meta",
    ROOT / "Assets/Scripts.meta",
    ROOT / "Assets/Resources/TankRevivalProduction.meta",
    ROOT / "Assets/Resources/TankRevivalProduction/Audio.meta",
    ROOT / "Assets/Scenes.meta",
)
SCENE_META = ROOT / "Assets/Scenes/Bootstrap.unity.meta"


def fail(message: str) -> None:
    raise SystemExit("Unity CI bootstrap contract failed: " + message)


def read(path: Path) -> str:
    if not path.is_file():
        fail(f"missing {path.relative_to(ROOT)}")
    return path.read_text(encoding="utf-8")


def guid_from_meta(path: Path, *, folder: bool) -> str:
    text = read(path)
    if folder and "folderAsset: yes" not in text:
        fail(f"{path.relative_to(ROOT)} must be a folder meta")
    if not folder and "folderAsset: yes" in text:
        fail(f"{path.relative_to(ROOT)} must be a file asset meta")
    match = re.search(r"(?m)^guid:\s*([0-9a-f]{32})\s*$", text)
    if not match:
        fail(f"{path.relative_to(ROOT)} has no stable 32-hex guid")
    return match.group(1)


def main() -> None:
    scene = read(SCENE)
    if not scene.startswith("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"):
        fail("Bootstrap.unity is not a Unity text scene")
    for required in ("OcclusionCullingSettings:", "RenderSettings:", "LightmapSettings:", "NavMeshSettings:"):
        if required not in scene:
            fail(f"Bootstrap.unity missing {required}")
    if "!u!1 " in scene or "\nGameObject:\n" in scene:
        fail("Bootstrap.unity must stay object-free; TankGame is installed by RuntimeInitializeOnLoadMethod")

    tank_game = read(TANK_GAME)
    if "[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]" not in tank_game:
        fail("TankGame no longer has its after-scene-load bootstrap")
    if 'new GameObject("TankGame")' not in tank_game:
        fail("TankGame runtime bootstrap no longer creates its root object")
    if "go.AddComponent<TankGame>()" not in tank_game:
        fail("TankGame bootstrap no longer installs TankGame")

    ci_build = read(CI_BUILD)
    required_ci_tokens = (
        'private const string ScenePath = "Assets/Scenes/Bootstrap.unity";',
        "ValidateStaticBootstrapScene();",
        "AssetDatabase.AssetPathToGUID(ScenePath)",
        "AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)",
        "scenes = new[] { ScenePath }",
    )
    for token in required_ci_tokens:
        if token not in ci_build:
            fail(f"CIBuild.cs missing static-scene contract token: {token}")
    forbidden = (
        "EditorSceneManager",
        "NewScene(",
        "SaveScene(",
        'Directory.CreateDirectory("Assets/Scenes")',
        "AssetDatabase.Refresh(",
    )
    for token in forbidden:
        if token in ci_build:
            fail(f"CIBuild.cs mutates or reimports Assets during headless build: {token}")

    guids = [guid_from_meta(path, folder=True) for path in FOLDER_METAS]
    guids.append(guid_from_meta(SCENE_META, folder=False))
    if len(guids) != len(set(guids)):
        fail("required Unity folder/scene metas contain duplicate GUIDs")

    print(
        "Unity CI bootstrap contract: PASS "
        f"(static scene, {len(FOLDER_METAS)} stable folder GUIDs, no headless scene mutation)"
    )


if __name__ == "__main__":
    main()
