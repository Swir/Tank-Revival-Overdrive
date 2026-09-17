#!/usr/bin/env python3
"""Deterministically stage and package one exact v13.1 objective-warfare Unity Windows candidate."""
from __future__ import annotations
import argparse, hashlib, json, shutil, stat, sys, zipfile
from pathlib import Path

EXE_NAME="TankRevivalOverdrive.exe"
DATA_NAME="TankRevivalOverdrive_Data"
ATTRIBUTION_NAME="V131_OBJECTIVE_WARFARE_BUILD.txt"
SHA_NAME="V131_OBJECTIVE_WARFARE_SHA256.txt"
PROVENANCE_NAME="V131_OBJECTIVE_WARFARE_PROVENANCE.json"
TREE_NAME="V131_OBJECTIVE_WARFARE_BUILD_TREE.txt"
DEFAULT_ZIP="TankRevivalOverdrive-v13.1-objective-warfare-Windows-x64.zip"
ZIP_TIME=(1980,1,1,0,0,0)

class PackageError(RuntimeError): pass

def describe_tree(root: Path) -> str:
    if not root.exists(): return f"! missing {root.as_posix()}\n"
    rows=[]
    for p in sorted(root.rglob("*"),key=lambda x:x.as_posix().lower()):
        kind="d" if p.is_dir() else "f"; suffix=""
        if p.is_file():
            try: suffix=f" bytes={p.stat().st_size}"
            except OSError: suffix=" bytes=?"
        rows.append(f"{kind} {p.relative_to(root).as_posix()}{suffix}")
    return "\n".join(rows)+("\n" if rows else "")

def _inside(path: Path,parent: Path)->bool:
    try: path.relative_to(parent); return True
    except ValueError: return False

def discover_runtime(root: Path)->Path:
    exes=sorted((p for p in root.rglob(EXE_NAME) if p.is_file()),key=lambda p:p.as_posix().lower())
    datas=sorted((p for p in root.rglob(DATA_NAME) if p.is_dir()),key=lambda p:p.as_posix().lower())
    pairs=[(e,e.parent/DATA_NAME) for e in exes if (e.parent/DATA_NAME).is_dir()]
    if len(exes)!=1 or len(datas)!=1 or len(pairs)!=1:
        raise PackageError(f"expected exactly one EXE/Data pair; exes={len(exes)} data={len(datas)} pairs={len(pairs)}")
    if pairs[0][1].resolve()!=datas[0].resolve(): raise PackageError("EXE/Data pairing mismatch")
    return pairs[0][0].parent.resolve()

def sha256(path: Path)->str:
    h=hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda:f.read(1024*1024),b""): h.update(chunk)
    return h.hexdigest()

def package_candidate(build_root: Path,output_dir: Path,candidate_sha: str,commit_sha: str,unity_version: str,version: str,zip_name: str=DEFAULT_ZIP,diagnostics_path: Path|None=None):
    build_root=build_root.resolve(); output_dir=output_dir.resolve()
    if not build_root.is_dir(): raise PackageError(f"missing build root {build_root}")
    if output_dir==build_root or _inside(output_dir,build_root) or _inside(build_root,output_dir): raise PackageError("build and package roots must be disjoint")
    if len(candidate_sha)!=40 or any(c not in "0123456789abcdefABCDEF" for c in candidate_sha): raise PackageError("invalid candidate SHA")
    if commit_sha!=candidate_sha: raise PackageError("commit SHA must equal candidate SHA")
    output_dir.mkdir(parents=True,exist_ok=True)
    diag=diagnostics_path.resolve() if diagnostics_path else output_dir/TREE_NAME
    diag.parent.mkdir(parents=True,exist_ok=True); diag.write_text(describe_tree(build_root),encoding="utf-8")
    runtime=discover_runtime(build_root); stage=output_dir/"stage"
    if stage.exists(): shutil.rmtree(stage)
    shutil.copytree(runtime,stage,copy_function=shutil.copy2)
    (stage/ATTRIBUTION_NAME).write_text(f"version={version}\ncandidate_sha={candidate_sha}\ncommit={commit_sha}\nunity={unity_version}\nsource_runtime={runtime.relative_to(build_root).as_posix()}\nexe={EXE_NAME}\n",encoding="utf-8")
    files=sorted((p for p in stage.rglob("*") if p.is_file()),key=lambda p:p.as_posix().lower())
    if not files: raise PackageError("staged runtime is empty")
    zp=output_dir/zip_name
    if zp.exists(): zp.unlink()
    with zipfile.ZipFile(zp,"w",compression=zipfile.ZIP_DEFLATED,compresslevel=9,allowZip64=True) as z:
        for p in files:
            info=zipfile.ZipInfo(p.relative_to(stage).as_posix(),ZIP_TIME); info.compress_type=zipfile.ZIP_DEFLATED; info.create_system=3; info.external_attr=(stat.S_IFREG|0o644)<<16
            with p.open("rb") as src,z.open(info,"w",force_zip64=True) as dst: shutil.copyfileobj(src,dst,1024*1024)
    digest=sha256(zp); (output_dir/SHA_NAME).write_text(f"{digest}  {zip_name}\n",encoding="utf-8")
    provenance={"schema":"tank-revival-v13.1-objective-warfare/v1","candidate_sha":candidate_sha,"commit_sha":commit_sha,"unity_version":unity_version,"version":version,"source_runtime":runtime.relative_to(build_root).as_posix(),"executable":EXE_NAME,"data_directory":DATA_NAME,"package":zip_name,"sha256":digest,"bytes":zp.stat().st_size,"file_count":len(files)}
    (output_dir/PROVENANCE_NAME).write_text(json.dumps(provenance,indent=2,sort_keys=True)+"\n",encoding="utf-8")
    return provenance

def main(argv=None):
    p=argparse.ArgumentParser(description=__doc__); p.add_argument("--build-root",required=True,type=Path); p.add_argument("--output-dir",required=True,type=Path); p.add_argument("--candidate-sha",required=True); p.add_argument("--commit-sha",required=True); p.add_argument("--unity-version",required=True); p.add_argument("--version-file",default="VERSION",type=Path); p.add_argument("--zip-name",default=DEFAULT_ZIP); p.add_argument("--diagnostics-path",type=Path); a=p.parse_args(argv)
    try: result=package_candidate(a.build_root,a.output_dir,a.candidate_sha,a.commit_sha,a.unity_version,a.version_file.read_text(encoding="utf-8").strip(),a.zip_name,a.diagnostics_path)
    except (OSError,PackageError,ValueError) as exc:
        print(f"v13.1 packaging FAILED: {exc}",file=sys.stderr)
        try: print(describe_tree(a.build_root.resolve()),file=sys.stderr)
        except Exception: pass
        return 2
    print(json.dumps(result,indent=2,sort_keys=True)); return 0

if __name__=="__main__": raise SystemExit(main())