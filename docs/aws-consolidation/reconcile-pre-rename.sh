#!/usr/bin/env bash
# Repeatable pre-rename parity check.
#
#   PGPASSWORD_SOURCE=... PGPASSWORD_TARGET=... \
#   ./reconcile-pre-rename.sh "host=... dbname=... user=..." \
#                             "host=... dbname=... user=..." out-dir
#
# Credentials never appear in argv or output: psql reads PGPASSWORD from the environment,
# so callers export it per invocation. Exit code 0 means the two databases are identical
# across every measure in reconcile-metrics.sql.
set -uo pipefail
SRC=${1:?source conninfo}; DST=${2:?target conninfo}; OUT=${3:-reconcile-out}
HERE=$(cd "$(dirname "$0")" && pwd)
mkdir -p "$OUT"

PGPASSWORD=${PGPASSWORD_SOURCE:?} psql "$SRC" -w -v ON_ERROR_STOP=1 \
  -f "$HERE/reconcile-metrics.sql" > "$OUT/source.metrics" 2>&1 || { echo "SOURCE QUERY FAILED"; exit 2; }
PGPASSWORD=${PGPASSWORD_TARGET:?} psql "$DST" -w -v ON_ERROR_STOP=1 \
  -f "$HERE/reconcile-metrics.sql" > "$OUT/target.metrics" 2>&1 || { echo "TARGET QUERY FAILED"; exit 3; }

if grep -qi '^ERROR' "$OUT/source.metrics" "$OUT/target.metrics"; then
  echo "METRICS CONTAIN ERRORS"; exit 4
fi
if diff -u "$OUT/source.metrics" "$OUT/target.metrics" > "$OUT/diff.txt"; then
  echo "PARITY: IDENTICAL"
else
  echo "PARITY: DIFFERENCES"; cat "$OUT/diff.txt"
fi
