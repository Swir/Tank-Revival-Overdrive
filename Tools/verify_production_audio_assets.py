#!/usr/bin/env python3
"""Deterministic source contract for authored production audio assets.

This guard intentionally does not replace the packaged-player smoke probe. It
verifies that the source assets are valid RIFF/WAVE containers and that runtime
loading keeps the >=2-authored-clips contract before an expensive Unity build.
"""

from __future__ import annotations

import hashlib
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
AUDIO_DIR = ROOT / "Assets" / "Resources" / "TankRevivalProduction" / "Audio"
DIRECTOR = ROOT / "Assets" / "Scripts" / "ProductionBattleAudioDirector.cs"
SMOKE = ROOT / "Assets" / "Scripts" / "ProductionPresentationCISmokeProbe.cs"
REQUIRED = ("BossAlarm.wav", "HeavyCannon.wav")


def fail(message: str) -> None:
    raise SystemExit("production-audio contract failed: " + message)


def inspect_wave(path: Path) -> tuple[int, str]:
    data = path.read_bytes()
    if len(data) < 44:
        fail(f"{path.name} is too small to be a valid WAV ({len(data)} bytes)")
    if data[0:4] != b"RIFF" or data[8:12] != b"WAVE":
        fail(f"{path.name} is not a RIFF/WAVE container")

    position = 12
    chunks: dict[bytes, int] = {}
    while position + 8 <= len(data):
        chunk_id = data[position : position + 4]
        size = int.from_bytes(data[position + 4 : position + 8], "little", signed=False)
        payload_start = position + 8
        payload_end = payload_start + size
        if payload_end > len(data):
            fail(f"{path.name} has a truncated {chunk_id!r} chunk")
        chunks[chunk_id] = chunks.get(chunk_id, 0) + size
        position = payload_end + (size & 1)

    if chunks.get(b"fmt ", 0) < 16:
        fail(f"{path.name} has no valid fmt chunk")
    if chunks.get(b"data", 0) <= 0:
        fail(f"{path.name} has no audio data chunk")

    return len(data), hashlib.sha256(data).hexdigest()


def main() -> int:
    if not AUDIO_DIR.is_dir():
        fail(f"missing Resources audio directory: {AUDIO_DIR.relative_to(ROOT)}")

    discovered = sorted(p.name for p in AUDIO_DIR.glob("*.wav"))
    if discovered != sorted(REQUIRED):
        fail(f"authored WAV inventory drift: expected={sorted(REQUIRED)} actual={discovered}")

    print("Authored production audio inventory:")
    for name in REQUIRED:
        path = AUDIO_DIR / name
        size, digest = inspect_wave(path)
        print(f"  {name}: {size} bytes sha256={digest}")

    director = DIRECTOR.read_text(encoding="utf-8")
    required_director_tokens = (
        'ProductionAudioResourceFolder = "TankRevivalProduction/Audio"',
        "Resources.LoadAll<AudioClip>(ProductionAudioResourceFolder)",
        "StringComparer.OrdinalIgnoreCase",
        'ResolveAuthoredClip("HeavyCannon")',
        'ResolveAuthoredClip("BossAlarm")',
        "DiscoveredAuthoredResourceCount",
        "AuthoredResourceSummary",
    )
    for token in required_director_tokens:
        if token not in director:
            fail(f"runtime authored-resource index contract missing: {token}")

    smoke = SMOKE.read_text(encoding="utf-8")
    match = re.search(r"MinimumAuthoredClipCount\s*=\s*(\d+)\s*;", smoke)
    if not match:
        fail("smoke probe does not expose MinimumAuthoredClipCount")
    minimum = int(match.group(1))
    if minimum < 2:
        fail(f"smoke authored clip floor was weakened to {minimum}")
    if "audio.LoadedAuthoredClipCount < MinimumAuthoredClipCount" not in smoke:
        fail("packaged-player authored audio assertion is missing")
    if "audio.AuthoredResourceSummary" not in smoke:
        fail("packaged-player authored resource diagnostics are missing")

    print(f"Production audio source contract passed: {len(REQUIRED)} WAVs, runtime floor={minimum}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except OSError as exc:
        print(f"production-audio contract failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
