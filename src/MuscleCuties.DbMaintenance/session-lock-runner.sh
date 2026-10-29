#!/usr/bin/env bash
# Session-held advisory lock runner for the release critical section.
# Invocation contract: session-lock-runner.sh -- <command> [args...]
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$script_dir/MuscleCuties.DbMaintenance.csproj"

if [ "${MC_RELEASE_OFFLINE_TEST:-0}" = "1" ]; then
    while [ "$#" -gt 0 ] && [ "$1" != "--" ]; do shift; done
    if [ "$#" -gt 0 ] && [ "$1" = "--" ]; then shift; fi
    if [ "$#" -eq 0 ]; then
        echo "session-lock-runner: missing child command after --" >&2
        exit 2
    fi
    exec "$@"
fi

exec dotnet run --project "$project" --configuration Release --no-build --no-restore -- session-lock-runner "$@"
