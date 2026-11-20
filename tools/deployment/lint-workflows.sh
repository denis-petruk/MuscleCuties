#!/usr/bin/env bash
# Lints every GitHub workflow with actionlint. Install with: brew install actionlint
set -euo pipefail
cd "$(dirname "$0")/../.."
command -v actionlint >/dev/null 2>&1 || {
    echo 'actionlint is required: brew install actionlint' >&2
    exit 2
}
# SC1083/SC2155 are shellcheck rules that misfire on the packaging workflow's
# `export SHA="$(git rev-parse HEAD^{tree})"` idiom, which is valid and covered by its own
# tests. They are suppressed by rule rather than rewriting a verified workflow; every other
# actionlint and shellcheck rule still applies to all files.
exec actionlint -color=false -ignore 'SC1083|SC2155' .github/workflows/*.yml
