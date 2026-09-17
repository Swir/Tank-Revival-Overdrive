#!/usr/bin/env python3
"""Build a deterministic v12.8 release-train package from one Unity Windows player.

The packager never writes into the GameCI build tree. This matters because
container-created Unity outputs can be readable but not writable by the host
runner. All attribution, staging, hashing and ZIP work happens in an explicit
output directory outside the build root.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import stat
import sys
import zipfile

EXE_NAME = "TankRevivalOverdrive.exe"
DATA_NAME = "TankRevivalOverdrive_Data"
ATTRIBUTION_NAME = "V128_RELEASE_TRAIN_BUILD.txt"
SHA_NAME = "V128_RELEASE_TRAIN_SHA256.txt"
PROVENANCE_NAME = "V128_RELEASE_TRAIN_PROVENANCE.json"
TREE_NAME = "V128_RELEASE_TRAIN_BUILD_TREE.txt"
DEFAULT_ZIP = "TankRevivalOverdrive-v12.8-release-train-Windows-x64.zip"
ZIP_TIME = (1980, 1, 1, 0, 0, 0)


class PackageError(RuntimeError):
    pass


def _is_relative_to(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def describe_tree(root: Path) -> str:
    if not root.exists():
        return f"! missing {root.as_posix()}\n"
    rows: list[str] = []
    for path in sorted(root.rglob("*"), key=lambda p: p.as_posix().lower()):
        kind = "d" if path.is_dir() else "f"
        rel = path.relative_to(root)
        suffix = ""
        if path.is_file():
            try:
                suffix = f" bytes={path.stat().st_size}"
            except OSError:
                suffix = " bytes=?"
        rows.append(f"{kind} {rel.as_posix()}{suffix}")
    return "\n".join(rows) + ("\n" if rows else "")


def discover_runtime(build_root: Path) -> Path:
    exes = sorted(
        (p for p in build_root.rglob(EXE_NAME) if p.is_file()),
        key=lambda p: p.as_posix().lower(),
    )
    data_dirs = sorted(
        (p for p in build_root.rglob(DATA_NAME) if p.is_dir()),
        key=lambda p: p.as_posix().lower(),
    )
    pairs = [(exe, exe.parent / DATA_NAME) for exe in exes if (exe.parent / DATA_NAME).is_dir()]
    if len(exes) != 1 or len(data_dirs) != 1 or len(pairs) != 1:
        raise PackageError(
            "Expected exactly one Windows player pair; "
            f"exes={len(exes)} data_dirs={len(data_dirs)} complete_pairs={len(pairs)}"
        )
    exe, data_dir = pairs[0]
    if data_dir.resolve() != data_dirs[0].resolve():
        raise PackageError("The discovered data directory is not paired with the discovered EXE.")
    return exe.parent.resolve()


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _copy_runtime(source: Path, stage: Path) -> None:
    if stage.exists():
        shutil.rmtree(stage)
    stage.parent.mkdir(parents=True, exist_ok=True)
    shutil.copytree(source, stage, copy_function=shutil.copy2)
    if not (stage / EXE_NAME).is_file() or not (stage / DATA_NAME).is_dir():
        raise PackageError("Staged runtime lost the EXE or matching Unity data directory.")


def _write_deterministic_zip(stage: Path, zip_path: Path) -> int:
    files = sorted((p for p in stage.rglob("*") if p.is_file()), key=lambda p: p.as_posix().lower())
    if not files:
        raise PackageError("Staged runtime is empty.")
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(
        zip_path,
        "w",
        compression=zipfile.ZIP_DEFLATED,
        compresslevel=9,
        allowZip64=True,
    ) as archive:
        for path in files:
            rel = path.relative_to(stage).as_posix()
            info = zipfile.ZipInfo(rel, ZIP_TIME)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = (stat.S_IFREG | 0o644) << 16
            with path.open("rb") as src, archive.open(info, "w", force_zip64=True) as dst:
                shutil.copyfileobj(src, dst, length=1024 * 1024)
    if not zip_path.is_file() or zip_path.stat().st_size <= 0:
        raise PackageError("Candidate ZIP was not created or is empty.")
    return len(files)


def package_candidate(
    *,
    build_root: Path,
    output_dir: Path,
    candidate_sha: str,
    commit_sha: str,
    unity_version: str,
    version: str,
    zip_name: str = DEFAULT_ZIP,
    diagnostics_path: Path | None = None,
) -> dict[str, object]:
    build_root = build_root.resolve()
    output_dir = output_dir.resolve()
    if not build_root.is_dir():
        raise PackageError(f"Build root does not exist: {build_root}")
    if _is_relative_to(output_dir, build_root) or output_dir == build_root:
        raise PackageError("Output directory must be outside the Unity build root to prevent recursive packaging.")
    if _is_relative_to(build_root, output_dir):
        raise PackageError("Unity build root must not be nested inside the packaging output directory.")
    if len(candidate_sha) != 40 or any(c not in "0123456789abcdefABCDEF" for c in candidate_sha):
        raise PackageError("candidate_sha must be a 40-character hexadecimal Git SHA.")
    if commit_sha != candidate_sha:
        raise PackageError("commit_sha must exactly match candidate_sha for an exact-candidate package.")

    output_dir.mkdir(parents=True, exist_ok=True)
    tree = describe_tree(build_root)
    diag = diagnostics_path.resolve() if diagnostics_path else output_dir / TREE_NAME
    diag.parent.mkdir(parents=True, exist_ok=True)
    diag.write_text(tree, encoding="utf-8")

    runtime = discover_runtime(build_root)
    stage = output_dir / "stage"
    _copy_runtime(runtime, stage)

    attribution = stage / ATTRIBUTION_NAME
    attribution.write_text(
        f"version={version}\n"
        f"candidate_sha={candidate_sha}\n"
        f"commit={commit_sha}\n"
        f"unity={unity_version}\n"
        f"source_runtime={runtime.relative_to(build_root).as_posix()}\n"
        f"exe={EXE_NAME}\n",
        encoding="utf-8",
    )

    zip_path = output_dir / zip_name
    file_count = _write_deterministic_zip(stage, zip_path)
    digest = _sha256(zip_path)
    sha_path = output_dir / SHA_NAME
    sha_path.write_text(f"{digest}  {zip_name}\n", encoding="utf-8")

    provenance = {
        "schema": "tank-revival-v12.8-release-train/v1",
        "candidate_sha": candidate_sha,
        "commit_sha": commit_sha,
        "unity_version": unity_version,
        "version": version,
        "source_runtime": runtime.relative_to(build_root).as_posix(),
        "executable": EXE_NAME,
        "data_directory": DATA_NAME,
        "package": zip_name,
        "sha256": digest,
        "bytes": zip_path.stat().st_size,
        "file_count": file_count,
    }
    provenance_path = output_dir / PROVENANCE_NAME
    provenance_path.write_text(json.dumps(provenance, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return provenance


def _parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--build-root", required=True, type=Path)
    p.add_argument("--output-dir", required=True, type=Path)
    p.add_argument("--candidate-sha", required=True)
    p.add_argument("--commit-sha", required=True)
    p.add_argument("--unity-version", required=True)
    p.add_argument("--version-file", default="VERSION", type=Path)
    p.add_argument("--zip-name", default=DEFAULT_ZIP)
    p.add_argument("--diagnostics-path", type=Path)
    return p


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        version = args.version_file.read_text(encoding="utf-8").strip()
        provenance = package_candidate(
            build_root=args.build_root,
            output_dir=args.output_dir,
            candidate_sha=args.candidate_sha,
            commit_sha=args.commit_sha,
            unity_version=args.unity_version,
            version=version,
            zip_name=args.zip_name,
            diagnostics_path=args.diagnostics_path,
        )
    except (OSError, PackageError, ValueError) as exc:
        print(f"v12.8 packaging FAILED: {exc}", file=sys.stderr)
        try:
            print(describe_tree(args.build_root.resolve()), file=sys.stderr)
        except Exception:
            pass
        return 2
    print(json.dumps(provenance, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
