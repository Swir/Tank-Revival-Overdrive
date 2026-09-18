#!/usr/bin/env python3
"""Fail fast when production-authored battle audio cannot be packaged safely."""

from __future__ import annotations

import hashlib
import re
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AUDIO_DIR = ROOT / "Assets/Resources/TankRevivalProduction/Audio"
EXPECTED = {
    "HeavyCannon.wav": {
        "resource": "HeavyCannon",
        "min_bytes": 20_000,
        "guid": "4e496dac21264c68a0c77e2597b08a72",
    },
    "BossAlarm.wav": {
        "resource": "BossAlarm",
        "min_bytes": 20_000,
        "guid": "a9423fa26ea64656a77b6a6552d46332",
    },
}


def parse_wav(path: Path) -> dict[str, int | str]:
    data = path.read_bytes()
    assert len(data) >= 44, f"{path}: WAV is too small"
    assert data[:4] == b"RIFF" and data[8:12] == b"WAVE", f"{path}: missing RIFF/WAVE header"

    cursor = 12
    fmt: bytes | None = None
    pcm_size = 0
    while cursor + 8 <= len(data):
        chunk_id = data[cursor : cursor + 4]
        chunk_size = struct.unpack_from("<I", data, cursor + 4)[0]
        payload_start = cursor + 8
        payload_end = payload_start + chunk_size
        assert payload_end <= len(data), f"{path}: truncated {chunk_id!r} chunk"
        if chunk_id == b"fmt ":
            fmt = data[payload_start:payload_end]
        elif chunk_id == b"data":
            pcm_size += chunk_size
        cursor = payload_end + (chunk_size & 1)

    assert fmt is not None and len(fmt) >= 16, f"{path}: missing fmt chunk"
    assert pcm_size > 0, f"{path}: missing PCM data"
    audio_format, channels, sample_rate, _, _, bits = struct.unpack_from("<HHIIHH", fmt)
    assert audio_format in (1, 3), f"{path}: unsupported WAV format {audio_format}"
    assert 1 <= channels <= 2, f"{path}: expected mono/stereo authored source"
    assert 8_000 <= sample_rate <= 192_000, f"{path}: invalid sample rate {sample_rate}"
    assert bits in (8, 16, 24, 32), f"{path}: invalid bit depth {bits}"
    return {
        "bytes": len(data),
        "pcm_bytes": pcm_size,
        "channels": channels,
        "sample_rate": sample_rate,
        "bits": bits,
        "sha256": hashlib.sha256(data).hexdigest(),
    }


def verify_meta(path: Path, expected_guid: str, seen_guids: set[str]) -> str:
    meta_path = path.parent / f"{path.name}.meta"
    assert meta_path.is_file(), f"{meta_path}: tracked Unity meta is required"
    text = meta_path.read_text(encoding="utf-8")
    match = re.search(r"(?m)^guid: ([0-9a-f]{32})\s*$", text)
    assert match is not None, f"{meta_path}: missing canonical 32-hex guid"
    guid = match.group(1)
    assert guid == expected_guid, f"{meta_path}: guid changed ({guid} != {expected_guid})"
    assert guid not in seen_guids, f"{meta_path}: duplicate production audio guid {guid}"
    seen_guids.add(guid)

    required_tokens = (
        "AudioImporter:",
        "  serializedVersion: 7",
        "  forceToMono: 0",
        "  preloadAudioData: 1",
        "  loadInBackground: 0",
    )
    for token in required_tokens:
        assert token in text, f"{meta_path}: missing stable importer setting {token.strip()}"
    return guid


def main() -> int:
    assert AUDIO_DIR.is_dir(), f"missing production audio directory: {AUDIO_DIR}"
    wavs = sorted(p.name for p in AUDIO_DIR.glob("*.wav"))
    assert wavs == sorted(EXPECTED), f"unexpected production audio inventory: {wavs}"

    seen_guids: set[str] = set()
    for filename, contract in EXPECTED.items():
        path = AUDIO_DIR / filename
        assert path.stat().st_size >= contract["min_bytes"], f"{path}: authored WAV unexpectedly small"
        info = parse_wav(path)
        guid = verify_meta(path, str(contract["guid"]), seen_guids)
        print(
            "[production-audio-source]"
            f" resource={contract['resource']}"
            f" guid={guid}"
            f" bytes={info['bytes']}"
            f" pcm={info['pcm_bytes']}"
            f" channels={info['channels']}"
            f" rate={info['sample_rate']}"
            f" bits={info['bits']}"
            f" sha256={info['sha256']}"
        )

    assert len(seen_guids) == len(EXPECTED), "production audio GUID inventory is incomplete"

    runtime = (ROOT / "Assets/Scripts/ProductionBattleAudioDirector.cs").read_text(encoding="utf-8")
    assert 'Resources.LoadAll<AudioClip>("TankRevivalProduction/Audio")' in runtime
    assert "LoadedAuthoredClipCount" in runtime
    assert "DiscoveredAuthoredResourceCount" in runtime
    assert '"BossAlarm"' in runtime and '"HeavyCannon"' in runtime

    smoke = (ROOT / "Assets/Scripts/ProductionPresentationCISmokeProbe.cs").read_text(encoding="utf-8")
    assert "director.LoadedAuthoredClipCount < 2" in smoke
    assert "director.DiscoveredAuthoredResourceCount < 2" in smoke
    assert "resources={director.AuthoredResourceSummary}" in smoke

    ci_build = (ROOT / "Assets/Editor/CIBuild.cs").read_text(encoding="utf-8")
    assert "ValidateProductionAudioAssets();" in ci_build
    assert "AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)" in ci_build
    assert "AssetDatabase.LoadAssetAtPath<AudioClip>(path)" in ci_build
    for filename, contract in EXPECTED.items():
        assert filename in ci_build, f"CIBuild missing required audio asset {filename}"
        assert str(contract["guid"]) in ci_build, f"CIBuild missing pinned guid for {filename}"

    print("[production-audio-source] PASS inventory=2 tracked_meta=2 unique_guids=2 resources=HeavyCannon,BossAlarm smoke_min=2")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
