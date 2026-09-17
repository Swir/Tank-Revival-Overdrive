#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

HERE = Path(__file__).resolve().parent
MODULE_PATH = HERE / "package_v128_release_train.py"
spec = importlib.util.spec_from_file_location("package_v128_release_train", MODULE_PATH)
assert spec and spec.loader
pkg = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pkg)

SHA = "a" * 40


class ReleaseTrainPackagerTests(unittest.TestCase):
    def make_player(self, root: Path, leaf: str = "StandaloneWindows64") -> Path:
        runtime = root / leaf
        runtime.mkdir(parents=True)
        (runtime / pkg.EXE_NAME).write_bytes(b"MZ-fake-player")
        data = runtime / pkg.DATA_NAME
        data.mkdir()
        (data / "data.unity3d").write_bytes(b"unity-data")
        (runtime / "UnityPlayer.dll").write_bytes(b"unity-player")
        return runtime

    def call(self, build: Path, output: Path):
        return pkg.package_candidate(
            build_root=build,
            output_dir=output,
            candidate_sha=SHA,
            commit_sha=SHA,
            unity_version="6000.3.17f1",
            version="v12.8.0-dev",
        )

    def test_happy_path_stages_outside_source_and_writes_provenance(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            build = root / "build"
            runtime = self.make_player(build)
            output = root / "package"
            info = self.call(build, output)
            self.assertEqual(info["candidate_sha"], SHA)
            self.assertFalse((runtime / pkg.ATTRIBUTION_NAME).exists(), "source build tree must stay read-only")
            self.assertTrue((output / "stage" / pkg.ATTRIBUTION_NAME).is_file())
            self.assertTrue((output / pkg.SHA_NAME).is_file())
            self.assertTrue((output / pkg.PROVENANCE_NAME).is_file())
            with zipfile.ZipFile(output / pkg.DEFAULT_ZIP) as zf:
                names = set(zf.namelist())
            self.assertIn(pkg.EXE_NAME, names)
            self.assertIn(f"{pkg.DATA_NAME}/data.unity3d", names)
            self.assertIn("UnityPlayer.dll", names)
            self.assertIn(pkg.ATTRIBUTION_NAME, names)

    def test_missing_data_directory_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            build = root / "build"
            runtime = build / "StandaloneWindows64"
            runtime.mkdir(parents=True)
            (runtime / pkg.EXE_NAME).write_bytes(b"MZ")
            with self.assertRaises(pkg.PackageError):
                self.call(build, root / "package")

    def test_duplicate_player_pairs_are_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            build = root / "build"
            self.make_player(build, "A")
            self.make_player(build, "B")
            with self.assertRaises(pkg.PackageError):
                self.call(build, root / "package")

    def test_output_under_build_root_is_rejected_before_copy(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            build = root / "build"
            self.make_player(build)
            with self.assertRaises(pkg.PackageError):
                self.call(build, build / "candidate")

    def test_packages_are_reproducible(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            build = root / "build"
            self.make_player(build)
            first = self.call(build, root / "out-a")
            second = self.call(build, root / "out-b")
            self.assertEqual(first["sha256"], second["sha256"])
            self.assertEqual(first["bytes"], second["bytes"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
