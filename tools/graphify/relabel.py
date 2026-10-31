"""Re-apply the backend audit's community labels and regenerate every derived artifact.

graphify's default community naming falls back to "App Repo Models (27)" for the
~30 repo models that share `AppRepoModels.cs`, which makes the report and wiki
unreadable. This script reproduces the naming used by the shipped audit graph:

* the 20 communities the audit names by inspection (`MANUAL`);
* every other community named from its dominant source file stem;
* duplicates disambiguated with their dominant type instead of a counter;
* `GRAPHIFY_VIZ_NODE_LIMIT` raised so `graph.html` renders the whole graph
  instead of graphify's aggregated community meta-graph above 5,000 nodes.

It is idempotent: running it on the shipped `graphify-out/` rewrites the same
labels, the same report, the same `graph.json`, `graph.html` and wiki.

It cannot build the graph itself. Inputs it expects to already exist:

* `graphify-out/graph.json` — the symbol-level graph (see §8 of
  `docs/audits/BACKEND_DB_CORE_WIRING_AUDIT_20261030.md` for which stages of
  that build were hand-performed and are not scripted);
* `graphify-out/.graphify_analysis.json` — cohesion and surprise edges.

Reproducing the committed snapshot exactly:

    PYTHONHASHSEED=0 .tools/graphify-venv/bin/python tools/graphify/relabel.py \
        --files 486 --words 397150

`PYTHONHASHSEED` pins graphify's equal-weight tie order (it iterates sets), and
`--files/--words` pin the corpus header to the audit's snapshot; without them the
header reports whatever `detect()` sees today, which grows as docs are added.

Usage (from anywhere inside the checkout):

    .tools/graphify-venv/bin/python tools/graphify/relabel.py
"""

from __future__ import annotations

import argparse
import collections
import json
import os
import re
import sys
from pathlib import Path

import networkx as nx
from networkx.readwrite import json_graph

from graphify.analyze import god_nodes, suggest_questions
from graphify.detect import detect
from graphify.export import to_html, to_json
from graphify.report import generate
from graphify.wiki import to_wiki

VIZ_NODE_LIMIT = 6000
OUTPUT = "graphify-out"

# Communities the audit names by inspection rather than by dominant file.
MANUAL = {
    0: "Use Case Interactors",
    1: "Cross-Module DTO Contracts",
    2: "DbContext and Model Factory",
    3: "Body Measurement Contracts",
    4: "Activity Preference Rules",
    5: "Activity Decision Builder",
    6: "EF Model Configuration",
    7: "Boundary DTOs and Health Checks",
    8: "Nutrition Overview Contracts",
    9: "Schema Introspection Model",
    10: "Cycle Observation Contracts",
    11: "Activity History Policy",
    12: "Server Profile Contracts",
    13: "Active Session Commands",
    14: "Weekly Plan Rule",
    15: "Strength Weekday Assignment",
    16: "Strength Frequency Resolution",
    17: "Strength Preview Contracts",
    18: "Injury Safety Contracts",
    19: "Exercise Eligibility Rule",
}


def humanize(stem: str) -> str:
    spaced = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", stem.replace("_", " ")).strip()
    return spaced[:40]


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--files", type=int, help="pin the report's file count (audit snapshot: 486)")
    parser.add_argument("--words", type=int, help="pin the report's word count (audit snapshot: 397150)")
    return parser.parse_args(argv)


def detection_snapshot(args: argparse.Namespace) -> dict:
    detected = detect(Path("."))
    if args.files is None:
        return detected
    detected["total_files"] = args.files
    detected["total_words"] = args.words or detected.get("total_words", 0)
    # A pinned snapshot reproduces the audit's report, which predates the detector's
    # "unclassified" line; drop it so the regenerated file stays comparable.
    detected.pop("unclassified", None)
    return detected


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv if argv is not None else sys.argv[1:])
    root = Path(__file__).resolve().parents[2]
    os.chdir(root)
    graph_path = Path(OUTPUT) / "graph.json"
    analysis_path = Path(OUTPUT) / ".graphify_analysis.json"
    for required in (graph_path, analysis_path):
        if not required.exists():
            print(f"error: {required} is missing; nothing to relabel", file=sys.stderr)
            return 1

    graph = json.loads(graph_path.read_text())
    analysis = json.loads(analysis_path.read_text())
    G = json_graph.node_link_graph(graph, edges="links")

    communities: dict[int, list[str]] = collections.defaultdict(list)
    for node_id, data in G.nodes(data=True):
        cid = data.get("community")
        if cid is not None:
            communities[int(cid)].append(node_id)
    communities = dict(communities)
    cohesion = {int(k): v for k, v in analysis["cohesion"].items()}

    base: dict[int, str] = {}
    for cid, members in communities.items():
        if cid in MANUAL:
            base[cid] = MANUAL[cid]
            continue
        files = collections.Counter()
        for member in members:
            source = G.nodes[member].get("source_file") or ""
            if source:
                files[Path(source).stem] += 1
        base[cid] = humanize(files.most_common(1)[0][0]) if files else "Symbols"

    counts = collections.Counter(base.values())
    labels: dict[int, str] = {}
    for cid, members in communities.items():
        name = base[cid]
        if counts[name] > 1 and cid not in MANUAL:
            types = collections.Counter()
            for member in members:
                label = G.nodes[member].get("label") or ""
                if label and " " not in label and not label.startswith("MuscleCuties."):
                    types[label] += 1
            if types:
                name = f"{name} · {types.most_common(1)[0][0][:36]}"
        labels[cid] = name

    seen = collections.Counter()
    for cid in sorted(communities, key=lambda c: -len(communities[c])):
        seen[labels[cid]] += 1
        if seen[labels[cid]] > 1:
            labels[cid] = f"{labels[cid]} ({seen[labels[cid]]})"

    questions = suggest_questions(G, communities, labels)
    report = generate(
        G,
        communities,
        cohesion,
        labels,
        god_nodes(G),
        analysis["surprises"],
        detection_snapshot(args),
        {"input": 0, "output": 0},
        ".",
        suggested_questions=questions,
    )
    Path(OUTPUT, "GRAPH_REPORT.md").write_text(
        "\n".join(line.rstrip() for line in report.splitlines()) + "\n"
    )
    Path(OUTPUT, ".graphify_labels.json").write_text(
        json.dumps({str(k): v for k, v in labels.items()}, indent=2)
    )
    Path(OUTPUT, ".graphify_analysis.json").write_text(
        json.dumps(
            {
                "communities": {str(k): v for k, v in communities.items()},
                "cohesion": {str(k): v for k, v in cohesion.items()},
                "gods": god_nodes(G),
                "surprises": analysis["surprises"],
                "questions": questions,
            },
            indent=2,
        )
    )
    to_json(G, communities, str(graph_path), force=True, community_labels=labels)
    os.environ.setdefault("GRAPHIFY_VIZ_NODE_LIMIT", str(VIZ_NODE_LIMIT))
    to_html(G, communities, str(Path(OUTPUT, "graph.html")), community_labels=labels)
    articles = to_wiki(
        G,
        communities,
        Path(OUTPUT, "wiki"),
        community_labels=labels,
        cohesion=cohesion,
        god_nodes_data=god_nodes(G),
    )
    print(
        f"labels {len(labels)} · articles {articles} · "
        f"nodes {G.number_of_nodes()} · edges {G.number_of_edges()}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
