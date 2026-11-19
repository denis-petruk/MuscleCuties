#!/usr/bin/env bash
set -euo pipefail

# Read-only, deterministic local repository status helper for MuscleCuties.
# Performs no network fetch, no Git mutations, and prints no secrets or file contents.

if ! command -v git >/dev/null 2>&1; then
  echo "error: git is not installed or not in PATH" >&2
  exit 1
fi

if ! repo_root="$(git rev-parse --show-toplevel 2>/dev/null)"; then
  echo "error: current directory is not inside a Git work tree" >&2
  exit 1
fi

timestamp="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"

if branch="$(git symbolic-ref --quiet --short HEAD 2>/dev/null)"; then
  branch_display="$branch"
else
  branch_display="(detached HEAD)"
fi

head_sha="$(git rev-parse --short=12 HEAD 2>/dev/null || echo "unborn")"

porcelain="$(git status --porcelain 2>/dev/null || true)"
staged_count=0
modified_count=0
untracked_count=0

if [ -n "$porcelain" ]; then
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    xy="${line:0:2}"
    if [ "$xy" = "??" ]; then
      untracked_count=$((untracked_count + 1))
    else
      x="${xy:0:1}"
      y="${xy:1:1}"
      [ "$x" != " " ] && staged_count=$((staged_count + 1))
      [ "$y" != " " ] && modified_count=$((modified_count + 1))
    fi
  done <<< "$porcelain"
fi

if [ "$staged_count" -eq 0 ] && [ "$modified_count" -eq 0 ] && [ "$untracked_count" -eq 0 ]; then
  tree_state="clean"
else
  tree_state="dirty"
fi

if upstream="$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null)"; then
  if counts="$(git rev-list --left-right --count "HEAD...${upstream}" 2>/dev/null)"; then
    ahead="$(awk '{print $1}' <<< "$counts")"
    behind="$(awk '{print $2}' <<< "$counts")"
    sync_display="ahead ${ahead}, behind ${behind} (local tracking ref; no network fetch)"
  else
    sync_display="unavailable (missing tracking ref data)"
  fi
else
  upstream="(none configured)"
  sync_display="not tracked against a remote branch"
fi

cat <<EOF
timestamp_utc: ${timestamp}
repo_root:     ${repo_root}
branch:        ${branch_display}
head:          ${head_sha}
working_tree:  ${tree_state} (staged=${staged_count}, modified=${modified_count}, untracked=${untracked_count})
upstream:      ${upstream}
remote_sync:   ${sync_display}
EOF
