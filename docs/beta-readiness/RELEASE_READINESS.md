# Release readiness — 2026-11-09

Candidate branch `integration/beta-polish-20261109-v2` (base `main` `a99fd80c`, the PR #46 merge). Reviewer-facing
summary: [REVIEW_PACKET.md](REVIEW_PACKET.md). Verdict and gates below reflect the local runs recorded here plus the CI
runs re-read on the published branch; nothing was deployed, migrated on a shared target, signed or uploaded.

## Verdict: **NO-GO for external testers. NO-GO for internal TestFlight until device acceptance and the owner actions are complete.**

This revision is `integration/beta-polish-20261109-v2` from `a99fd80c`. It adds the E-04…E-07 code fixes, the
localization repairs and the A11 repository half; it does not change backend, schema or policy behaviour. CI is green at
the pre-packet tip `6ee9d53` (run 42677684566: 10 successful, 0 failing, 0 pending, including
`verify_ios_compile` on `xcode-27`); the docs-only packet commit is covered by the same workflow. The UI compiles, but
simulator acceptance is **not run**
([dated record](../release-audit/2026-11-09/SIMULATOR_ACCEPTANCE_2026-11-09.md)), PostgreSQL compatibility was exercised
only by CI (`postgres_verify`, green), and the live AWS posture is unchanged. The owner product decisions (`S-03`,
`S-04`, `S-05`) and physical-device QA01 remain open. Nothing here is device or simulator evidence.
Internal TestFlight becomes possible after the simulator checks and owner actions below; external testers additionally
need the live AWS items.

### This revision's executed checks

| Check | Command | Result |
| --- | --- | --- |
| Build, non-UI | `DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 dotnet build <project> -m:1 -nodeReuse:false` | Core.Tests / Mobile.Contracts.Tests / Registration.Tests / Server.Tests / Deployment.Tests projects: success. Core.Tests reports 4 xUnit analyzer warnings (T-21) |
| Unit suites | `dotnet test <project> --no-build` | Core 1325/1325; Mobile Contracts 183/183; Registration 54/54; all 0 failed, 0 skipped |
| Deployment policy | `python3 -m unittest discover -s tests/MuscleCuties.Deployment.Tests` | 17/17 OK |
| Localization | `dotnet test tests/MuscleCuties.Core.Tests --no-build --filter FullyQualifiedName~LocalizationCatalogTests` | 8/8 passed, 0 failed |
| Catalog parity | Python scan of all EN/RU family partials at this head | 2036 EN / 2036 RU keys; 0 missing, 0 extra, 0 format-argument mismatches; every key referenced by the edited view models resolves |
| Owner wizard | `bash -n scripts/beta-release-wizard.sh` | syntax OK; 5 stages, no secret entry |
| Edited XAML | `python3 -c "xml.etree.ElementTree.parse(...)"` | `ProfileSetupPage.xaml` OK |
| Changed template | PyYAML with CloudFormation tag constructors | `observability.yaml` parses; 8 resources, subscription present |

## Gates

| Gate | Status | Evidence / cause |
| --- | --- | --- |
| G01 Build | PASS (compile) | Non-UI projects build locally; Core.Tests has 4 analyzer warnings (T-21). The iOS simulator target was compiled by `verify_ios_compile` on `xcode-27` in run 42677684566 at the pre-packet tip `6ee9d53`, so the UI changes compile; no device install or launch was run. |
| G02 Unit tests | PASS (local, this revision) | Core 1325/1325 (the duplicate theory row is skipped by the runner); Mobile Contracts 183/183; Registration 54/54; Deployment policy 17/17. Server host-building tests were not run locally (sandbox file-watcher hang, per recipe) |
| G03 Workout rules | PASS with open items | Traceability in AUDIT.md; S-03 (policy load step-down) and S-04 (in-session pain report) open |
| G04 Canonical engine | PASS | Swap/reschedule now go through canonical eligibility and schedule validation; dead gating/readiness/generator code removed |
| G05 Scheduling | PASS (tests; prior-head simulator) | ≤2 activities/day, empty sessions withheld with disclosure, weekdays preserved; the 2026-11-09 simulator run at `7aa53789` observed the weekday behavior, but it was not re-run at this tip |
| G06 Safety | PARTIAL | Injury restrictions and the in-session swap path are exercised at the database level after `fe3f2356` (`WorkoutSwapAndRescheduleSafetyTests`: 3 passed, 0 skipped, including the whole-occurrence reschedule rule after `5d7477ae`); mid-session pain path missing (S-04) |
| G07 Personalization | PARTIAL | Energy now affects readiness; cycle/HIIT tested; in-session swap fixed `fe3f2356` (AUDIT G-07); policy decision 4/5 load step not in production path (S-03) |
| G08 Onboarding | PASS (tests + prior-head simulator; not re-run at this tip) | Journey 1 end to end at `7aa53789`; E-01 fixed after that run. Device re-check still pending (G12) |
| G09 Apply | PASS (tests + simulator first-plan auto-apply) | Replacement and reduced-volume plans require Apply; accept is transactional and idempotent |
| G10 Logging | PASS (tests + prior-head simulator; not re-run at this tip) | The 2026-11-09 simulator run observed per-set rows persisted in PostgreSQL; integration tests cover the same path |
| G11 Integration | PASS (CI) | `postgres_verify / verify_postgres_compatibility` green in runs 42675311877, 42676415439 and 42677684566; last local full run: `c7b3a1ff` (PostgreSQL 16.15, all five CI fixture variables set): **105 passed, 0 failed, 0 skipped**. The earlier 102/3 result was missing fixture provisioning, not product defects: `mc_test_nutrition_ci` (a clone of the seeded head-7 database) satisfies `GeneratedMealPostgresTests`' `mc_test_nutrition_` prefix, `mc_retry_disposable` satisfies the retry guard, and `mc_pg09_disposable` owned by non-superuser `mc_ci_migrator` satisfies `Pg09LeastPrivilegeAndLoadTests`, which also runs its 3-minute bounded-pool load; `MC_SCHEMA_COMPATIBILITY_POSTGRES` enables the disposable template fixture. Provisioning mirrors `.github/workflows/db-verify.yml`, whose `postgres_verify` job runs this same suite on PostgreSQL 17. `WorkoutSwapAndRescheduleSafetyTests` 3 passed; `CanonicalOccurrenceAdjustmentPersistenceTests` 2; `StrengthStyleOfferStoreIntegrationTests` 1; `WorkoutExperiencePromotionStoreTests` 1. Both swap and reschedule translate the day exercise's `ENGINE_<planningId>` code through the persisted `source_exercise_id` (migration 006; 59/60 definitions mapped, planning ids 101–182); before `5d7477ae` the reschedule validator filtered the definition-keyed muscle contributions by planning id and rejected every strength day. Migration chain contract 7/7 and model schema 4/4. Follow-up `7ca46512` removed the duplicate runtime localization key the copy change introduced: `LocalizationCatalogTests` 8/8 and `BetaPlanningCopyContractTests` 10/10 at that head |
| G12 Simulator | NOT RUN — BLOCKED (no macOS/Xcode) | Dated record [SIMULATOR_ACCEPTANCE_2026-11-09.md](../release-audit/2026-11-09/SIMULATOR_ACCEPTANCE_2026-11-09.md): every check NOT RUN, with the captured Linux blocker (`NETSDK1147`; no `xcrun`/`simctl`/`xcodebuild`) and the exact Mac procedure. CI `verify_ios_compile` at the pre-packet tip `6ee9d53` (run 42677684566) is compile evidence only. Prior run: journeys 1, 2, 3, 5 pass; 4 and 6–15 not run ([SIMULATOR_QA.md](SIMULATOR_QA.md)) |
| G13 Persistence | NOT RUN | Restart/retry/offline journeys not exercised on device |
| G14 Security | PASS (repo) / FAIL (live) | No secrets in tracked files; Debug cannot target Beta; live Lambda secrets in plaintext env (A6) |
| G15 Migrations | PASS (local disposable) | Empty DB bootstrap applies 7 ordered scripts (001–007); the ledger reports 7 applied, head 7, tree `746`, re-run applies 0. `ops.schema_metadata` records `schema_version=7` with fingerprint `25ff237d…8c982c4b`. `dbcheck` at head 7 and live Beta adoption are not proven (A4) |
| G16 Cleanup | PASS | Dead DI/gating/readiness code, GitLab stub, retired release scripts, Android/Windows/MacCatalyst trees removed; suites green |
| G17 CI | PASS (observed) | Run [42677684566](https://github.com/denis-petruk/MuscleCuties/actions/runs/42677684566) at the pre-packet tip `6ee9d53`: **completed successfully**; the commit page reports **10 successful, 0 failing, 0 pending**, and the branch badge reads `passing`. `verify_source`, `verify_plan_generation`, `verify_ios_compile` (xcode-27), `postgres_verify / verify_postgres_compatibility` and the aggregate `required` are green. Earlier runs [42675311877](https://github.com/denis-petruk/MuscleCuties/actions/runs/42675311877) at `1f71f83` and [42676415439](https://github.com/denis-petruk/MuscleCuties/actions/runs/42676415439) at `54d8826` were also green. The `CI` workflow triggers on `integration/**` pushes, so the docs-only packet commit is covered; the live run list is [here](https://github.com/denis-petruk/MuscleCuties/actions/workflows/ci.yml?query=branch%3Aintegration%2Fbeta-polish-20261109-v2). |
| G18 GitHub | Branch pushed; **no PR yet** | `integration/beta-polish-20261109-v2` carries this work at its published tip. On 2026-11-09 no `refs/pull/*/head` matched this branch's published tips (`1f71f83`, `54d8826`, `91886e2`, `6ee9d53`; 47 PR refs checked; PR #47 is unrelated), so the owner must open the PR from this branch into `main`; use [REVIEW_PACKET.md](REVIEW_PACKET.md) as the body. `gh` is unauthenticated in the sandbox. The earlier `integration/beta-polish-20261109` branch holds `4ee9b4f` and has failed run 42673683274; do not use it, delete it. |

## Owner actions

### Before internal TestFlight
1. Open and merge the PR from `integration/beta-polish-20261109-v2` into `main` (base `a99fd80c`, which already contains PR #46 and its #40–#45 merges). Use [REVIEW_PACKET.md](REVIEW_PACKET.md) as the body; no PR existed on 2026-11-09.
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

### This branch, `integration/beta-polish-20261109-v2`

Run `bash scripts/beta-release-wizard.sh` on the Mac checkout first: it opens the PR compare page and records the
number, walks this branch's CI, records the simulator checks in
`docs/release-audit/2026-11-09/OWNER_SIMULATOR_QA_<date>.md`, walks the live alarm subscription, and captures the four
policy answers. It writes nothing until you confirm each stage.

1. Run stage 3 of `scripts/beta-release-wizard.sh` on a Mac (or follow the commands in the dated record). It builds the
   branch, installs and launches it, and fills `OWNER_SIMULATOR_QA_<date>.md`. The dated record
   [SIMULATOR_ACCEPTANCE_2026-11-09.md](../release-audit/2026-11-09/SIMULATOR_ACCEPTANCE_2026-11-09.md) already lists every
   check and the exact commands; replace its `NOT RUN` values with real results and fold them into `G12`.
2. Re-check E-04 (validation visible under Continue), E-05 (Today shows the quiz phase), E-06 (no "Heavy" on a beginner
   general-strength day), E-07 (no empty-state flash) on the smallest iPhone and iPhone 17 Pro, light and dark, EN and RU.
3. Answer the four batched decisions in
   [WORKOUT_POLICY_RESEARCH.md](../release-audit/2026-11-09/WORKOUT_POLICY_RESEARCH.md): S-03 load step vs set-only,
   decision 5 cycle dose, S-04 pain surface, S-05 seven-day window. S-03, S-04 and S-05 stay BLOCKED until then.
4. Read CI on this branch: run 42677684566 at `6ee9d53` is green (10/0/0, including `verify_ios_compile`); the docs-only packet commit is covered by the same workflow. PR #46's green run is not evidence for this branch.
5. The A11 repository half (alarm subscription) ships here; deploying `observability.yaml` and confirming the email
   opt-in remain owner actions under the live change set in
   [INFRA_RULES_2026-11-09.md](../infra/INFRA_RULES_2026-11-09.md).

## Environment notes for local verification

The local sandbox blocked socket binding, SysV shared memory, CoreSimulator XPC and two Xcode paths; the owner
approved project-local sandbox settings for them during this task. Tests that start an ASP.NET host hang under the
sandbox's file watcher restrictions and were excluded locally only.
