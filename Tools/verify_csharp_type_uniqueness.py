#!/usr/bin/env python3
"""Fail CI when non-partial top-level C# type names collide inside a namespace.

This lightweight source guard catches a class of Unity compile failures before the expensive
Windows player build. It deliberately inspects declarations only; Unity remains the compile
authority. Partial declarations are allowed only when every duplicate declaration is partial.
"""
from __future__ import annotations

import re
import sys
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"

NAMESPACE_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*(?:\{|;)?\s*$")
TYPE_RE = re.compile(
    r"^(?P<indent>[ \t]*)(?P<mods>(?:(?:public|internal|protected|private|abstract|sealed|static|partial|readonly|ref|unsafe)\s+)*)"
    r"(?P<kind>class|struct|enum|interface|record)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\b"
)

@dataclass(frozen=True)
class Decl:
    path: str
    line: int
    namespace: str
    kind: str
    name: str
    partial: bool


def iter_decls(path: Path):
    namespace = "<global>"
    namespace_indent = 0
    for lineno, raw in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        ns = NAMESPACE_RE.match(raw)
        if ns:
            namespace = ns.group(1)
            namespace_indent = len(raw) - len(raw.lstrip(" \t"))
            continue
        match = TYPE_RE.match(raw)
        if not match:
            continue
        indent = len(match.group("indent").replace("\t", "    "))
        # In this repository namespace-block top-level types are indented one level (4 spaces).
        # Global/file-scoped declarations are accepted at column zero. Deeper declarations are nested.
        if namespace == "<global>":
            if indent != 0:
                continue
        elif indent > namespace_indent + 4:
            continue
        mods = match.group("mods").split()
        yield Decl(
            str(path.relative_to(ROOT)).replace("\\", "/"),
            lineno,
            namespace,
            match.group("kind"),
            match.group("name"),
            "partial" in mods,
        )


def main() -> int:
    declarations: dict[tuple[str, str], list[Decl]] = defaultdict(list)
    for path in sorted(ASSETS.rglob("*.cs")):
        for decl in iter_decls(path):
            declarations[(decl.namespace, decl.name)].append(decl)

    failures: list[str] = []
    for (namespace, name), items in sorted(declarations.items()):
        if len(items) < 2:
            continue
        if all(item.partial for item in items) and len({item.kind for item in items}) == 1:
            continue
        details = ", ".join(f"{item.path}:{item.line} ({item.kind}{' partial' if item.partial else ''})" for item in items)
        failures.append(f"duplicate top-level C# type {namespace}.{name}: {details}")

    legacy = (ROOT / "Assets/Scripts/TacticalTerrainDirector.cs").read_text(encoding="utf-8-sig")
    v134 = (ROOT / "Assets/Scripts/TacticalTerrainV134.cs").read_text(encoding="utf-8-sig")
    if "class LegacyTacticalTerrainDirectorV38" not in legacy:
        failures.append("legacy terrain runtime must retain the distinct LegacyTacticalTerrainDirectorV38 type")
    if "class TacticalTerrainDirector" not in v134:
        failures.append("v13.4 tactical terrain runtime must own the TacticalTerrainDirector type")
    if "class TacticalTerrainDirector" in legacy:
        failures.append("legacy terrain source reintroduced the v13.4 TacticalTerrainDirector type name")

    if failures:
        print("C# TYPE UNIQUENESS: FAIL")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print(f"C# TYPE UNIQUENESS: PASS ({len(declarations)} namespace/type keys scanned)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
