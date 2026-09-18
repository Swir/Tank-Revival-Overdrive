#!/usr/bin/env python3
from pathlib import Path
import importlib.util
import json
import tempfile
import zipfile

HERE = Path(__file__).resolve().parent
MODULE = HERE / "package_v136_sensor_fusion.py"
spec = importlib.util.spec_from_file_location("v136_pack", MODULE)
mod = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(mod)

SHA = "a" * 40
UNITY = "6000.3.17f1"

def make_runtime(root: Path) -> Path:
    runtime = root / "nested" / "StandaloneWindows64"
    data = runtime / mod.DATA_NAME
    data.mkdir(parents=True)
    (runtime / mod.EXE_NAME).write_bytes(b"MZ-v136-test")
    (runtime / "UnityPlayer.dll").write_bytes(b"unity")
    (data / "globalgamemanagers").write_bytes(b"gm")
    (data / "resources.assets").write_bytes(b"resources")
    return runtime

def package_once(base: Path, name: str):
    build = base / (name + "-build")
    out = base / (name + "-out")
    make_runtime(build)
    return mod.package_candidate(build, out, SHA, SHA, UNITY, "v13.6.0-dev"), out

def main() -> int:
    with tempfile.TemporaryDirectory() as td:
        root = Path(td)
        p1, o1 = package_once(root, "one")
        p2, o2 = package_once(root, "two")
        z1 = o1 / mod.DEFAULT_ZIP
        z2 = o2 / mod.DEFAULT_ZIP
        assert p1["sha256"] == p2["sha256"] == mod.sha256(z1) == mod.sha256(z2)
        assert p1["candidate_sha"] == p1["commit_sha"] == SHA
        assert p1["version"] == "v13.6.0-dev"
        assert p1["schema"] == "tank-revival-v13.6-sensor-fusion/v1"
        assert p1["file_count"] >= 5

        with zipfile.ZipFile(z1) as z:
            names = z.namelist()
            assert names == sorted(names, key=str.lower)
            assert mod.EXE_NAME in names
            assert mod.DATA_NAME + "/globalgamemanagers" in names
            assert mod.ATTRIBUTION_NAME in names
            attribution = z.read(mod.ATTRIBUTION_NAME).decode("utf-8")
            assert f"candidate_sha={SHA}" in attribution
            assert "version=v13.6.0-dev" in attribution
            for info in z.infolist():
                assert info.date_time == mod.ZIP_TIME

        prov = json.loads((o1 / mod.PROVENANCE_NAME).read_text(encoding="utf-8"))
        assert prov["sha256"] == p1["sha256"]
        checksum = (o1 / mod.SHA_NAME).read_text(encoding="utf-8")
        assert p1["sha256"] in checksum and mod.DEFAULT_ZIP in checksum

        # Ambiguous runtime discovery must fail hard.
        bad = root / "ambiguous"
        make_runtime(bad / "a")
        make_runtime(bad / "b")
        try:
            mod.discover_runtime(bad)
        except mod.PackageError:
            pass
        else:
            raise AssertionError("ambiguous runtime discovery unexpectedly succeeded")

        # Provenance may never attribute a package to a different commit.
        mismatch_build = root / "mismatch-build"
        mismatch_out = root / "mismatch-out"
        make_runtime(mismatch_build)
        try:
            mod.package_candidate(mismatch_build, mismatch_out, SHA, "b" * 40, UNITY, "v13.6.0-dev")
        except mod.PackageError:
            pass
        else:
            raise AssertionError("candidate/commit mismatch unexpectedly succeeded")

    print("v13.6 deterministic packager tests: PASS")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
