# Current beta checkpoint

Date: 2026-10-30 (America/Toronto). Status: `HANDOFF_READY` after a bounded
database and release-control integration. Integration branch:
`release/musclecuties-testflight-beta-hardening`; source integrated through
`f404ba1c`. The branch is based on GitLab `infra/aws-lambda-postgres` at
`8cade1f87abff7753d0e9ffdf47fea90b5a00b0b`.

## Completed in this session

- Added a read-only migration-runner role prerequisite command and fail-closed gate
  in `migrate --confirm`, preventing the observed 31→49 partial upgrade when the
  owner-transfer role membership is missing.
- Added and tested the production commit/tree HMAC release guard. The production
  CI job remains disabled.
- Corrected the PITR runbook to the current 59-migration staging state. No restore
  was performed.
- Added a read-only candidate runtime-principal preflight and documented the
  unresolved tenant/auth role design. No live login, grant or Lambda secret changed.
- Preserved the original `/Users/denispetruk/RiderProjects/MuscleCuties` dirty
  `ROADMAP.md`, `STATUS.md` and tracked deletion without modification.

## Verification

- Disposable PostgreSQL 17: migration prerequisite, 31→59 rehearsal and runtime
  principal tests 9/9 PASS. The SQL catalog query ran with effective app role and
  correctly rejected a privileged session login.
- Rollout engine 11/11 PASS; release failure-injection checks PASS.
- `git diff --check` PASS. Broader Core/Server/iOS suites, GitLab pipeline, signed
  IPA, live restore and physical-device acceptance are NOT RUN in this session.

## Next actions

1. Fetch and verify the release branch remote tip and clean worktree; acquire the
   shared lease described in `WORK_CONTRACT.md` before editing.
2. Continue B1/B2 source and disposable-database validation using current code.
3. Keep production disabled; the restore drill and runtime principal cutover remain
   separate reviewed operations. Do not infer production approval from staging.
4. Update this checkpoint and ledger after each verified milestone; commit, push,
   confirm the remote SHA, and return the state to `HANDOFF_READY` when handing off.

No active specialist agent assignment remains. The current source lacks a verified
full beta release candidate; see `RELEASE_READINESS.md`.
