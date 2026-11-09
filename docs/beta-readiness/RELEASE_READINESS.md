# Release readiness — 2026-11-09

Candidate branch `integration/beta-rc-20261108` (base `main` `4ee9b4f2`). Verdict and gates below reflect only what was
run locally in this task; nothing was deployed, migrated on a shared target, signed or uploaded.

## Verdict: **NO-GO for external testers. GO for an internal TestFlight build** once the owner actions under
"Before internal TestFlight" are done.

Code-level P0/P1 items found in this audit are fixed on the branch (see [AUDIT.md](AUDIT.md)), including the
username-capitalization input bug at `286cda4c`. The findings still open are P2/P3 UX polish (`E-04…E-07`), server
debt (`S-07`, `G-10/G-11`), and owner product decisions (`S-03`, `S-04`, `S-05`). External-tester readiness is blocked
by the live AWS posture (A1–A6), those product decisions and device acceptance that has not been run on a physical
iPhone.

## Gates

| Gate | Status | Evidence / cause |
| --- | --- | --- |
| G01 Build | PASS | Core/UseCases/Infrastructure/PostgreSql/Server/DbMaintenance build; iOS `net10.0-ios` simulator build succeeds on MAUI 10.0.90 (2 warnings: offline NuGet audit, one unused field) |
| G02 Unit tests | PASS (local) | Core 1,326/1,326; Mobile Contracts 183/183; Registration 50/50 (1 host-building test excluded locally); Server 64/64 (3 host-building classes + latency budget excluded locally). Excluded tests hang only inside the local sandbox and run in CI |
| G03 Workout rules | PASS with open items | Traceability in AUDIT.md; S-03 (policy load step-down) and S-04 (in-session pain report) open |
| G04 Canonical engine | PASS | Swap/reschedule now go through canonical eligibility and schedule validation; dead gating/readiness/generator code removed |
| G05 Scheduling | PASS (tests) | ≤2 activities/day, empty sessions withheld with disclosure, weekdays preserved (also seen on simulator) |
| G06 Safety | PARTIAL | Injury restrictions and the in-session swap path are exercised at the database level after `e0adcaf9` (`WorkoutSwapAndRescheduleSafetyTests`: 3 passed, 0 skipped, including the whole-occurrence reschedule rule after `80ec4d20`); mid-session pain path missing (S-04) |
| G07 Personalization | PARTIAL | Energy now affects readiness; cycle/HIIT tested; in-session swap fixed `e0adcaf9` (AUDIT G-07); policy decision 4/5 load step not in production path (S-03) |
| G08 Onboarding | PASS on simulator with fixes pending device re-check | Journey 1 end to end; E-01 fixed after the run |
| G09 Apply | PASS (tests + simulator first-plan auto-apply) | Replacement and reduced-volume plans require Apply; accept is transactional and idempotent |
| G10 Logging | PASS | Simulator per-set log persisted as observed rows in PostgreSQL |
| G11 Integration | PASS (local) | Full suite at `286cda4c` (`mc_test_beta_rc`, PostgreSQL 16.15): 99 passed, 3 failed, 0 skipped of 102. The three failures are fixture guards, not assertions — nutrition requires a `mc_test_nutrition_*` database, the retry test requires `mc_retry_disposable`, and PG09 requires `MC_PG09_LOAD_POSTGRES`. `WorkoutSwapAndRescheduleSafetyTests`: 3 passed, 0 skipped. Both swap and reschedule translate the day exercise's `ENGINE_<planningId>` code through the persisted `source_exercise_id` (migration 006; 59/60 definitions mapped, planning ids 101–182); before `80ec4d20` the reschedule validator filtered the definition-keyed muscle contributions by planning id and rejected every strength day. Migration chain contract 7/7 and model schema 4/4 |
| G12 Simulator | PARTIAL | Journeys 1, 2, 3, 5 pass; 4 and 6–15 not run ([SIMULATOR_QA.md](SIMULATOR_QA.md)) |
| G13 Persistence | NOT RUN | Restart/retry/offline journeys not exercised on device |
| G14 Security | PASS (repo) / FAIL (live) | No secrets in tracked files; Debug cannot target Beta; live Lambda secrets in plaintext env (A6) |
| G15 Migrations | PASS (local disposable) | Empty DB bootstrap applies 6 ordered scripts (001–006); the ledger reports 6 applied, head 6, seed 746, re-run applies 0. `ops.schema_metadata` records `schema_version=6` with fingerprint `96b48ad3…f447ea`. `dbcheck` at head 6 and live Beta adoption are not proven (A4) |
| G16 Cleanup | PASS | Dead DI/gating/readiness code, GitLab stub, retired release scripts, Android/Windows/MacCatalyst trees removed; suites green |
| G17 CI | PASS | `gh pr checks 46` at the final code head `1e547a07`: 10 successful, 0 failing, 0 pending on both push and PR — `postgres_verify` 4m32s push / 4m54s PR, `verify_ios_compile` 3m6s / 1m37s on `xcode-27`, `verify_plan_generation` 1m26s / 1m56s, `verify_source` 49s / 1m24s, `required` green. The first green run was `39053ecd`; the earlier failure was the `maui-ios` workload resolving to .NET for iOS 27.1 against `macos-15`'s Xcode 16.4, fixed by `6cce5fc8` |
| G18 GitHub | See PR | Branch pushed and PR opened only after owner-authorized push |

## Owner actions

### Before internal TestFlight
1. Review and merge the candidate PR (supersedes draft PRs #40–#45, which it contains).
2. Confirm OD-5: the app uses only standard OS/HTTPS encryption (`ITSAppUsesNonExemptEncryption=false` is set).
3. Confirm the delegated decisions implemented at their recommendation: OD-2 (no Bodyweight-only for Beta), OD-3
   (inline disclosure replaces native injury alert), OD-4 (HealthKit off), OD-6 (two consecutive deficits),
   OD-7 (history-restricted styles disabled with reason), OD-8 (`MC-BETA-WORKOUT-1` as dose approval).
4. Rebuild on device and re-check E-01 (birthday picker) and E-02 (recommendation copy).
5. Archive and sign in Xcode/App Store Connect with the distribution profile (HealthKit capability can be removed
   from the App ID); upload; internal group only.

### Before external testers
6. AWS (A1–A6): designate the live stack as Beta (OD-1), 14-day backups + deletion protection + restore drill,
   read-only `dbcheck` then rehearse adoption on a restored snapshot, create `beta` aliases and serve them, subscribe
   alarms, move secrets to Secrets Manager and rotate.
7. Decide S-03 (implement or amend policy decisions 4/5) and S-04 (mid-session pain action).
8. Physical-device QA01 in EN and RU, including VoiceOver, Dynamic Type and the CR01 transition sequence.
9. App Store Connect: App Privacy answers matching `PrivacyInfo.xcprivacy`, beta description, demo account,
   feedback email.

## Environment notes for local verification

The local sandbox blocked socket binding, SysV shared memory, CoreSimulator XPC and two Xcode paths; the owner
approved project-local sandbox settings for them during this task. Tests that start an ASP.NET host hang under the
sandbox's file watcher restrictions and were excluded locally only.
