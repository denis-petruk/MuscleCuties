#!/usr/bin/env bash
set -euo pipefail

# Prunes stale git worktree metadata and flags agent bloat inside the repo root.
git worktree prune

echo "Active worktrees:"
git worktree list

for dir in .agent-worktrees .claude/worktrees .cloud-attachments artifacts; do
  if [ -d "$dir" ]; then
    echo "WARNING: $dir exists inside repo root ($(du -sh "$dir" | cut -f1)). Move worktrees to ../MuscleCuties-worktrees/."
  fi
done

find docs -maxdepth 3 -iname "HANDOFF*.md" 2>/dev/null | while read -r f; do
  echo "WARNING: Dated handoff file found ($f). AGENTS.md requires overwriting STATUS.md instead."
done
