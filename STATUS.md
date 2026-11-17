# Status — Workout Engine V2 QA, integration and shared reset

## 1. Changed files

Started on `fix/qa-audit-followups` at `045769c2fe3193c67ae38898bd8b43a2c5bd6b4b`. Integrated the existing V2 domain/catalog/persistence/API/UI/test changes in `b2bd57b3`; subsequent commits contain verified test-helper, deployment-validator and evidence corrections. Integration is tracked in PR #50: https://github.com/denis-petruk/MuscleCuties/pull/50.

This pass changed `tests/maestro/workout-engine-v2/{log-current-exercise.yaml,log-set.yaml,profile.yaml,quiz.yaml}`, `tools/deployment/validate-worker-package.sh`, `tests/MuscleCuties.Deployment.Tests/test_worker_package.py`, `.github/workflows/ci.yml`, this file, and the earlier implementation-plan/login whitespace cleanup. The worker validator regression proved that an environment-configured worker has no appsettings dependency while each actual required runtime file remains mandatory. Later experimental scroll/animation changes failed their replay and were discarded.

`.codex/config.toml` is excluded. Concurrent, unrelated documentation edits/deletions (including ROADMAP, QA_REPORT, implementation-plan/catalog documents and docs/remaining) were preserved outside these commits.

## 2. Test commands and pasted output

```text
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug -v minimal
Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1602, Skipped:     0, Total:  1602, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)

dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   237, Skipped:     0, Total:   237, Duration: 201 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:    63, Skipped:     0, Total:    63, Duration: 533 ms - MuscleCuties.Registration.Tests.dll (net10.0)

source /tmp/mc-ux.integration; dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   122, Skipped:     0, Total:   122, Duration: 3 m 2 s - MuscleCuties.Registration.Integration.Tests.dll (net10.0)

source /tmp/mc-ux.integration; dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   125, Skipped:     0, Total:   125, Duration: 1 m 8 s - MuscleCuties.Server.Tests.dll (net10.0)

python3 -m unittest discover -s tests/MuscleCuties.Deployment.Tests -p 'test_*.py'
Ran 19 tests in 6.001s
OK
```

Total: 2,149 .NET tests plus 19 deployment tests passed. Initial fixture errors were corrected before these full runs: the first Server run lacked the latency connection; the first integration run had no marker and skipped database tests, then three tests rejected noncanonical disposable database names. The successful full integration run has zero skips.

All Maestro runs use simulator `678048F6-C4C7-406C-A970-7104AED3E00A` and `tests/maestro/workout-engine-v2/s26.yaml`:

| Locale / equipment | Invocation parameters | Actual result |
| --- | --- | --- |
| Russian / Full gym | `-e QA_LANGUAGE=ru -e QA_LOCALE=ru_RU -e QA_EQUIPMENT_ANSWER_ID=QuizAnswerEquipment1` | Exit 1: session logging could not find `WorkoutSessionNextExerciseButton`. All 17 saved journey screenshots reviewed. |
| English / Bodyweight only | `-e QA_LANGUAGE=en -e QA_LOCALE=en_US -e QA_EQUIPMENT_ANSWER_ID=QuizAnswerEquipment3` | Exit 0 after the observed bilateral/per-side repetition helper was added. Lifecycle restart, archive, sliders and band-anchor subflows completed. All 25 screenshots reviewed. |
| Requested historical selector 5 | `-e QA_LANGUAGE=en -e QA_LOCALE=en_US -e QA_EQUIPMENT_ANSWER_ID=QuizAnswerEquipment5` | Exit 1: `QuizAnswerEquipment5` does not exist in the current three-option catalog. |
| English / actual Dumbbells and bands | `-e QA_LANGUAGE=en -e QA_LOCALE=en_US -e QA_EQUIPMENT_ANSWER_ID=QuizAnswerEquipment2` | Stopped, exit 143, at DB Shoulder Press (exercise 4/7) with repeated weight validation. Subsequent isolated replay exited 1 because it did not advance to exercise 5. All 18 saved screenshots reviewed. This is not a passing full journey. |

Current selector mapping was verified in the rendered accessibility hierarchy and canonical seed: **1 = Full gym; 2 = Dumbbells and bands; 3 = Bodyweight only**. Earlier runbook/request labels 3/5 are stale; the passing selector-3 run must not be described as a Dumbbells and bands pass. Earlier retries also exposed registration input and wellness-checkbox scroll timing failures; profile scroll settling was corrected and subsequently reached the quiz.

## 3. What was not verified and why

- **Russian acceptance fails:** captured quiz questions/answers, Strength Focus/Strength & Sculpt, exercise names and RIR remain English. Cyrillic text in the captured screens wraps without observed essential-label truncation, but the interrupted journey did not capture later screens. No full Russian UX PASS is claimed.
- **Home session input remains blocked:** the actual Dumbbells and bands run remains on DB Shoulder Press with “Add a valid weight in kg.” Narrow scroll/animation replays did not advance. Whether the cause is input automation, focus/binding, or validation remains unresolved; do not claim that it is only a helper problem.
- **Numeric keyboard/layout:** a saved Home capture shows the numeric keyboard open, pinned Next reachable, and values `20` / `108` fully visible. The helper attempted `10` while the screen showed `108`, so exact preset replacement/observed-value persistence is unverified. Bodyweight's final keyboard capture did not prove an open keyboard; one preferences capture caught a loading frame.
- **History date observation:** the Bodyweight archive screenshot displays 17 November while the client date/cycle screen is 16 November. Account-local history-date correctness needs follow-up. Screenshots demonstrate the captured normal-size/light appearance only; no broader acceptance is claimed.

**Gate 95 reset and deployment — executed after owner authorization on 2026-11-16:**

The owner explicitly approved destructive resets, then confirmed Development, Staging and Beta share one Staging deployment. The reset was therefore performed once against that shared target; independent sequential resets are impossible with the current topology. Production was excluded throughout.

- Inventory: account `028417007003`; all 17 enabled regions checked for RDS instances/Lambda functions. Only `us-east-2` contains the shared RDS `musclecuties-staging-db` / database `musclecuties_staging`, API `musclecuties-backend-api` and worker `musclecuties-workout-reconciliation`. Active media is `musclecuties-media-staging-028417007003`; the empty, unused CloudFormation bucket `musclecuties-staging-media-028417007003` was preserved. Production bucket `musclecuties-media-prod-028417007003` was not mutated.
- Fresh encrypted RDS snapshot `mc-g95-staging-pre-reset-20261116` is retained and available. An isolated restore matched all 75 source tables by aggregated data fingerprint SHA256 `d86a65546b6d9bb0a4c94feced92e3152e46ab147608b866632ae55b4cb5d028`. The reset rehearsal then reached schema 11, 781 seeded rows, compatible schema and zero users.
- Private, encrypted, versioned S3 backup `mc-g95-staging-backup-028417007003`, prefix `backup-20261116/`, retains all 10 original object versions plus manifest metadata for two delete markers. All 8 current objects were restored into a separate drill prefix with matching sizes/ETags. Manifest SHA256: `c640baa264ada60bfd77a0296e073c3afc7d68ec401533c8c97c9fa8e5a4b8aa`.
- Shared reset output: `{"status":"reset","schema_version":11,"seeded_rows":781,"compatible":true,"users_remaining":0}`. Active Staging media reset deleted all 12 versions/markers; zero remain. Both functions resumed their prior concurrency configuration.
- A direct worker smoke exposed pre-reset object grants lost by schema recreation. A second isolated restore recovered 232 original grant statements; they were reapplied without changing credentials or role memberships. Discovery can execute discovery but cannot read account rows; the tenant can read account rows but cannot execute discovery. Final worker invocation: `{"Status":200,"FunctionError":null,"Version":"2"}`. Future reset preflight must preserve object grants in addition to data/schema.
- Source `b2bd57b31a939c5741dfacc880f246e67186bbf6` was packaged/validated and uploaded as immutable, versioned S3 artifacts. API `beta` alias serves version **15**, SHA256 `e84c7e12a1ebab8900edb83d5a7643f0ff8bb3eb4626225eb83269dad22524c1`; worker `beta` serves version **2**, SHA256 `a1aebbfb0048a0f0f90db2ea4946791b10522a7bef20ceb6e5f5be6b2c9c5133`. Serving digests matched their packages. API Gateway and the reconciliation schedule invoke the aliases. Deployment returned `release_status=verified code=none`; `/health/live` and `/health/ready` both returned HTTP 200. The older binary initially failed readiness after reset and was replaced.
- Both temporary restore databases and their security groups were deleted. RDS/S3 recovery backups remain retained. The disposable local API was stopped after QA; no TestFlight or App Store Connect actions occurred.

Screenshots, run logs and nonsecret cloud proofs are preserved under `artifacts/qa-s26-20261116/` (ignored by Git). Russian evidence is in `ru-gym-initial/`; the passing Bodyweight evidence is in `en-bodyweight-passed/`; actual Home evidence is in `en-dumbbells-bands-blocked/`; focused weight replays are in `weight-probe-*`. The stable open-keyboard contact sheet is `home-keyboard-open-proof.png`.

## 4. Next step or blocker

PR #50 owns Git integration; its current remote checks and merge state are authoritative. Retain both recovery backups. Before Beta UX acceptance, resolve the Russian runtime-copy gaps and the Home-profile weight-entry blocker, verify exact entered values and account-local archive dates, then repeat the failed journeys. The reset and cloud health/worker smoke are verified; the UX acceptance matrix is incomplete.
