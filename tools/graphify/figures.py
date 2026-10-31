"""Emit the audit's measured figures from the graph artifacts, and keep the document honest.

Every number in the generated block is read from `graphify-out/graph.json`,
`.graphify_labels.json`, `.graphify_analysis.json` or `GRAPH_REPORT.md` at run
time, plus the corpus `detect()` sees on this checkout. Nothing is typed by
hand, so a regenerated graph or a changed checkout cannot leave the audit
quoting stale values.

Usage (from anywhere inside the checkout):

    .tools/graphify-venv/bin/python tools/graphify/figures.py            # print the block
    .tools/graphify-venv/bin/python tools/graphify/figures.py --write F  # rewrite it in F
    .tools/graphify-venv/bin/python tools/graphify/figures.py --check F  # exit 1 if F drifts

The document carries the block between `<!-- figures:begin -->` and
`<!-- figures:end -->`; `--check` compares that region byte-for-byte with fresh
output and lists the differing lines. Output is deterministic: two runs on the
same artifacts are identical.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path

from networkx.readwrite import json_graph

from graphify.detect import detect

OUTPUT = Path("graphify-out")
BEGIN = "<!-- figures:begin -->"
END = "<!-- figures:end -->"
SCHEMA_PREFIX = "Database Schema Compatibility · "

# The communities the audit reads. Ordering and sizes come from the artifacts; this
# list only decides which rows are quoted, so a renamed or vanished community fails
# the run instead of silently leaving a stale row behind.
DISCUSSED = [
    "Use Case Interactors",
    "Cross-Module DTO Contracts",
    "DbContext and Model Factory",
    "Body Measurement Contracts",
    "Activity Preference Rules",
    "EF Model Configuration",
    "Schema Introspection Model",
]


def read(path: Path, *, required: bool = True) -> str | None:
    if not path.exists():
        if required:
            raise SystemExit(
                f"error: {path} is missing. The graph artifacts are git-ignored; see §8 of the audit "
                "for which build stages are scripted and which were hand-performed."
            )
        return None
    return path.read_text()


def parse(pattern: str, text: str, what: str) -> str:
    match = re.search(pattern, text)
    if match is None:
        raise SystemExit(f"error: could not read {what} from GRAPH_REPORT.md")
    return match.group(1)


def thousands(value: int) -> str:
    return f"{value:,}"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--write", metavar="FILE", help="rewrite the block in FILE")
    mode.add_argument("--check", metavar="FILE", help="exit 1 when the block in FILE is stale")
    args = parser.parse_args(argv)

    graph = json.loads(read(OUTPUT / "graph.json"))
    labels = {int(k): v for k, v in json.loads(read(OUTPUT / ".graphify_labels.json")).items()}
    analysis = json.loads(read(OUTPUT / ".graphify_analysis.json"))
    report = read(OUTPUT / "GRAPH_REPORT.md")

    G = json_graph.node_link_graph(graph, edges="links")
    cohesion = {int(k): v for k, v in analysis["cohesion"].items()}
    community_of = {node["id"]: node.get("community") for node in graph["nodes"]}
    members = Counter(community_of.values())

    report_files = parse(r"- ([\d,]+) files · ~[\d,]+ words", report, "the corpus file count")
    report_words = parse(r"- [\d,]+ files · ~([\d,]+) words", report, "the corpus word count")
    report_communities = parse(
        r"- \d+ nodes · \d+ edges · \d+ communities \((\d+) shown", report, "the shown-community count"
    )
    report_thin = parse(r"\(\d+ shown, (\d+) thin omitted\)", report, "the thin-community count")
    isolated = int(parse(r"\*\*([\d,]+) isolated node\(s\)", report, "the isolated-symbol count")
                  .replace(",", ""))
    isolated_total = int(
        parse(r"\(Counts symbols only; ([\d,]+) node\(s\) total have", report,
              "the total-isolated count").replace(",", "")
    )

    wiki = OUTPUT / "wiki"
    articles = len([p for p in wiki.glob("*.md") if p.name != "index.md"])
    html = OUTPUT / "graph.html"
    html_mb = f"{html.stat().st_size / 1e6:.1f}"

    by_label = {name: cid for cid, name in labels.items()}
    missing = [name for name in DISCUSSED if name not in by_label]
    if missing:
        raise SystemExit(f"error: community label(s) no longer in the graph: {missing}")
    top = sorted((by_label[name] for name in DISCUSSED), key=lambda cid: (-members[cid], labels[cid]))
    schema = sorted((cid for cid, name in labels.items() if name.startswith(SCHEMA_PREFIX)),
                    key=lambda cid: (-members[cid], labels[cid]))

    detected = detect(Path("."))
    corpus = detected["files"]
    code_suffixes = Counter(Path(path).suffix.lower() for path in corpus["code"])
    breakdown = ", ".join(f"{n} {ext or '<none>'}" for ext, n in code_suffixes.most_common())

    god_rows = []
    for rank, god in enumerate(analysis["gods"][:10], start=1):
        cid = community_of.get(god["id"])
        god_rows.append(
            f"| {rank} | `{god['label']}` | {thousands(god['degree'])} | {labels.get(cid, '—')} |"
        )

    community_rows = [
        f"| {labels[cid]} | {thousands(members[cid])} | {cohesion.get(cid, 0):.3f} |" for cid in top
    ]

    block = "\n".join(
        [
            f"**Artifacts.** {thousands(G.number_of_nodes())} nodes · "
            f"{thousands(G.number_of_edges())} edges · {thousands(len(labels))} communities "
            f"({report_communities} in the report, {report_thin} thin omitted) · "
            f"{articles} wiki articles · `graph.html` {html_mb} MB · "
            f"`GRAPH_REPORT.md` {thousands(len(report.splitlines()))} lines.",
            "",
            # Word counts are deliberately absent here: this document is part of the corpus, so a
            # word total would change every time the block is written and `--check` could never pass.
            f"**Corpus on this checkout.** {thousands(detected['total_files'])} files — "
            f"{len(corpus['code'])} code ({breakdown}), {len(corpus['document'])} documents. "
            f"The graph was built from the snapshot in the report header: "
            f"{report_files} files, {report_words} words.",
            "",
            "**God nodes** (most connected first)",
            "",
            "| # | Node | Edges | Community |",
            "| --- | --- | --- | --- |",
            *god_rows,
            "",
            f"The three schema-contract nodes account for "
            f"{thousands(sum(g['degree'] for g in analysis['gods'] if g['label'] in {'SchemaRequirements', 'DatabaseSchemaCompatibility', 'CatalogSnapshot'}))} "
            "edges between them.",
            "",
            f"**Communities the audit reads** ({len(top)} of {thousands(len(labels))})",
            "",
            "| Community | Nodes | Cohesion |",
            "| --- | --- | --- |",
            *community_rows,
            "",
            f"**Schema-contract family.** {len(schema)} communities under "
            f"`{SCHEMA_PREFIX.strip()}` · {thousands(sum(members[cid] for cid in schema))} nodes · "
            f"largest three {', '.join(thousands(members[cid]) for cid in schema[:3])}.",
            "",
            f"**Knowledge gaps.** {thousands(isolated)} isolated symbols (≤ 1 connection); "
            f"{thousands(isolated_total)} nodes "
            "have ≤ 1 connection once file, concept and rationale nodes are included.",
        ]
    )

    if args.write:
        path = Path(args.write)
        text = path.read_text()
        if BEGIN not in text or END not in text:
            print(f"error: {path} has no {BEGIN} … {END} region", file=sys.stderr)
            return 1
        head, rest = text.split(BEGIN, 1)
        _, tail = rest.split(END, 1)
        path.write_text(f"{head}{BEGIN}\n{block}\n{END}{tail}")
        print(f"wrote {len(block.splitlines())} lines into {path}")
        return 0

    if args.check:
        path = Path(args.check)
        text = path.read_text()
        if BEGIN not in text or END not in text:
            print(f"error: {path} has no {BEGIN} … {END} region", file=sys.stderr)
            return 1
        current = text.split(BEGIN, 1)[1].split(END, 1)[0].strip("\n")
        if current == block:
            print(f"figures block matches the artifacts ({path})")
            return 0
        print(f"figures block is stale in {path}:", file=sys.stderr)
        for line in __import__("difflib").unified_diff(
            current.splitlines(), block.splitlines(), "document", "artifacts", lineterm="", n=1
        ):
            print(line, file=sys.stderr)
        return 1

    print(block)
    return 0


if __name__ == "__main__":
    sys.exit(main())
