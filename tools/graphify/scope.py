"""Print what graphify would graph for this checkout, so the scope block is verifiable.

`graphify .` merges `.gitignore` first and `.graphifyignore` last, so the labelled
backend-audit block in `.graphifyignore` is the only thing that defines the
graph's corpus. This reads that decision back out without writing anything.

Usage (from anywhere inside the checkout):

    .tools/graphify-venv/bin/python tools/graphify/scope.py

Expected at audit time (2026-10-30): 437 code + 49 documents, ~397k words.
The document count grows as the repo gains Markdown; the code count is the
backend slice and must not include `src/MuscleCuties.UI`, `src/MuscleCuties.UseCases`,
`src/MuscleCuties.Infrastructure`, `src/MuscleCuties.App`, `tests/` or `artifacts/`.
"""

from __future__ import annotations

import collections
import sys
from pathlib import Path

from graphify.detect import detect

EXCLUDED_TREES = (
    "/src/MuscleCuties.UI/",
    "/src/MuscleCuties.UseCases/",
    "/src/MuscleCuties.Infrastructure/",
    "/src/MuscleCuties.App/",
    "/tests/",
    "/artifacts/",
)


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    result = detect(root)
    files = result["files"]
    detected = [str(path) for group in files.values() for path in group]

    print(f"root: {result.get('scan_root', root)}")
    print(f"total: {result['total_files']} files · {result['total_words']:,} words")
    for kind, group in files.items():
        print(f"  {kind:9} {len(group)}")

    counts = collections.Counter(Path(path).suffix.lower() for path in detected)
    print("by extension:", ", ".join(f"{ext or '<none>'}={n}" for ext, n in counts.most_common()))

    leaked = sorted(
        path
        for path in detected
        if any(f"MuscleCuties/{tree.lstrip('/')}" in f"{path}/" for tree in EXCLUDED_TREES)
    )
    if leaked:
        print(f"error: {len(leaked)} file(s) from excluded trees were detected:", file=sys.stderr)
        for path in leaked[:10]:
            print(f"  {path}", file=sys.stderr)
        return 1
    print("excluded trees: none detected (scope block honoured)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
