# Fresh-chat resume instruction

Work on the MuscleCuties GitLab release branch
`release/musclecuties-testflight-beta-hardening`. Read `AGENTS.md`, then
`docs/beta-orchestration/{WORK_CONTRACT.md,CURRENT_CHECKPOINT.md,STATE.json,TASK_LEDGER.md,RELEASE_READINESS.md}`.
Fetch GitLab and verify branch, remote tip, dirty worktrees and completed evidence.
Acquire the GitLab-backed state lease by fast-forward push before editing; exit if
another active owner holds it. Continue the first verified unfinished B1/B2 task,
implement and test it, and update the durable checkpoint. Never deploy to
production, mutate production data, distribute TestFlight or activate the disabled
production job without separate explicit authorization. At a bounded handoff,
commit, push, verify remote SHA and release the lease as `HANDOFF_READY`. If `DONE`,
exit immediately and disable the hourly automation if possible.
