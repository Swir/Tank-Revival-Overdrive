#!/usr/bin/env python3
from __future__ import annotations
import importlib.util, tempfile, unittest, zipfile
from pathlib import Path

HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location("package_v131_objective_warfare",HERE/"package_v131_objective_warfare.py")
assert spec and spec.loader
pkg=importlib.util.module_from_spec(spec); spec.loader.exec_module(pkg)
SHA="d"*40

class V131PackagerTests(unittest.TestCase):
    def make_player(self,root:Path,leaf="StandaloneWindows64"):
        runtime=root/leaf; runtime.mkdir(parents=True); (runtime/pkg.EXE_NAME).write_bytes(b"MZ-v131")
        data=runtime/pkg.DATA_NAME; data.mkdir(); (data/"data.unity3d").write_bytes(b"unity-data"); (runtime/"UnityPlayer.dll").write_bytes(b"unity-player")
        return runtime
    def call(self,build:Path,out:Path): return pkg.package_candidate(build,out,SHA,SHA,"6000.3.17f1","v13.1.0-dev")
    def test_happy_path_and_provenance(self):
        with tempfile.TemporaryDirectory() as td:
            r=Path(td); source=self.make_player(r/"build"); out=r/"out"; info=self.call(r/"build",out)
            self.assertEqual(info["candidate_sha"],SHA); self.assertEqual(info["version"],"v13.1.0-dev"); self.assertFalse((source/pkg.ATTRIBUTION_NAME).exists()); self.assertTrue((out/pkg.PROVENANCE_NAME).is_file())
            with zipfile.ZipFile(out/pkg.DEFAULT_ZIP) as z: names=set(z.namelist())
            self.assertIn(pkg.EXE_NAME,names); self.assertIn(f"{pkg.DATA_NAME}/data.unity3d",names); self.assertIn(pkg.ATTRIBUTION_NAME,names)
    def test_missing_data_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            r=Path(td); runtime=r/"build"/"StandaloneWindows64"; runtime.mkdir(parents=True); (runtime/pkg.EXE_NAME).write_bytes(b"MZ")
            with self.assertRaises(pkg.PackageError): self.call(r/"build",r/"out")
    def test_duplicate_players_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            r=Path(td); self.make_player(r/"build","A"); self.make_player(r/"build","B")
            with self.assertRaises(pkg.PackageError): self.call(r/"build",r/"out")
    def test_output_inside_build_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            r=Path(td); self.make_player(r/"build")
            with self.assertRaises(pkg.PackageError): self.call(r/"build",r/"build"/"out")
    def test_deterministic_zip(self):
        with tempfile.TemporaryDirectory() as td:
            r=Path(td); self.make_player(r/"build"); a=self.call(r/"build",r/"a"); b=self.call(r/"build",r/"b")
            self.assertEqual(a["sha256"],b["sha256"]); self.assertEqual(a["bytes"],b["bytes"]); self.assertEqual(a["file_count"],b["file_count"])

if __name__=="__main__": unittest.main(verbosity=2)