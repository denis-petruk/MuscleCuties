# Owner simulator QA — 2026-11-09

**Result: simulator acceptance NOT EXECUTED — the iOS app could not be built in the QA environment. 0 PASS, 0 FAIL,
21 BLOCKED of the 21 checklist rows; the 10 supplementary checks are also BLOCKED or code-level only.**

This is a new owner record. It does not edit [SIMULATOR_ACCEPTANCE_2026-11-09.md](SIMULATOR_ACCEPTANCE_2026-11-09.md),
`RELEASE_READINESS.md`, `REVIEW_PACKET.md`, G12 or the release verdict (still NO-GO). Nothing below is simulator, device,
screenshot or VoiceOver evidence. The wizard (`scripts/beta-release-wizard.sh`) was not run; its stage 3 commands were
used as reference only.

## Run header

| Field | Value |
| --- | --- |
| Branch / tip | `integration/beta-polish-20261109-v2` / `f1918e138634ba39c2483bde3e1194b6d346f2a8` |
| Tree verification | `git fetch origin integration/beta-polish-20261109-v2` → `origin/…` = `f1918e1…`; detached worktree `.agent-worktrees/qa-20261109` at that SHA; `git status --short` empty. The parent checkout (`main` @ `4ee9b4f2`, 56 uncommitted owner files) was not switched, stashed or modified. |
| Host | macOS 26.6.2 (25G83), Apple silicon |
| Xcode | 26.3 (17C529). CI compiles on `xcode-27`; the local Xcode is older. |
| Simulator runtimes | iOS 26.3 (23D8133), iOS 18.2 (22C150) |
| Simulators available | iPhone 17 Pro `678048F6-C4C7-406C-A970-7104AED3E00A` (booted), iPhone SE (3rd gen) `93D64AF7-909F-4ED8-B8FF-E51A81C8400E` (shutdown). Not used: no app binary. |
| .NET | SDK 10.0.203 (`global.json` 10.0.200, `rollForward: latestFeature`; CI uses 10.0.403). Workload `maui` 10.0.20/10.0.100 installed. |
| Build command | `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64` |
| Build result | **FAIL (environment)** — see below. No `.app` produced; nothing installed or launched. |
| Appearance / language / Dynamic Type / VoiceOver / Reduce Motion | Not exercised (no app). |
| Local API | `http://127.0.0.1:5080`, Development, loopback only. Started from this tip; 200 on `/api/v1/accounts/registration/quiz`. |
| Local DB | Disposable PostgreSQL 16.15 on `127.0.0.1:55461` (pre-existing local cluster, data dir `/tmp/mc-pg16`), new database `mc_qa_20261109`. `migrate` → `schema_version=7`, 8 scripts (000–007); `seed` → 746 rows; `dbcheck` → `compatible`, fingerprint `25ff237d…8c982c4b`. |
| Synthetic accounts | `qa.sim<5-digit>@example.test` / `qasim<5-digit>` (5 accounts; OTP via development log delivery). No Beta data, no Beta endpoint. |
| Screenshot folder | None — no app was rendered. |
| Evidence folder | [evidence-owner-qa-2026-11-09/](evidence-owner-qa-2026-11-09/) (redacted logs) |
| Tester / date | Automated agent for the owner, 2026-11-09 |

## Why the build failed (blocker, not a product defect)

The agent session sandbox denies `AF_UNIX` sockets (`bind` → `EPERM`; probe in
`evidence-owner-qa-2026-11-09/sandbox-unix-socket-probe.txt`). MSBuild needs them:

1. Default build: worker-node handshake never completes; the build ends `Build FAILED`, 0 errors, after 5 minutes
   (`build-ios-2.log`).
2. In-proc build (`-m:1 -nodeReuse:false`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`): `error MSB4216: Could not run the
   "ComputeManagedAssemblies" task because MSBuild could not create or connect to a task host with runtime "NET"`
   (`build-ios.log`). `Microsoft.NET.ILLink.targets:320` declares that task with `TaskFactory="TaskHostFactory"`,
   so it always runs out of process; there is no in-process option.

`dotnet test` fails similarly: testhost receives `SocketException (13): Permission denied`
(`vstest-diag-excerpt.log`). `initdb` fails with `shmget … Operation not permitted`, so the pre-existing local cluster
was used. The sandbox settings belong to the owner and were not changed or worked around.

**To unblock:** run the build command above in a normal terminal on this Mac (or in Rider), or allow Unix sockets in
the agent sandbox settings, then re-run this record. Not attempted: building in the user's own terminal tab,
which would have bypassed the sandbox.

## Checklist rows (from SIMULATOR_ACCEPTANCE_2026-11-09.md)

| ID | Check | Result | Observation | Screenshot |
| --- | --- | --- | --- | --- |
| E-04 | Invalid onboarding form shows the reason above Continue | BLOCKED | No app binary. Code at tip: both messages sit in the fixed row-2 footer (`ProfileSetupPage.xaml:279-296`). | — |
| E-05 | Today phase header matches quiz/Train phase | BLOCKED | No app. API: overview for a fresh account returns `phaseName: "Follicular"`, matching the quiz answer (backend half only). | — |
| E-06 | Beginner general-strength day does not claim "Heavy" | BLOCKED | No app. Code: `WorkoutViewModel.cs:1044` reports localized Moderate. | — |
| E-07 | No empty-state flash on session open; failed load shows the error card | BLOCKED | No app. Code: `HasNoActivities` gated on `_hasLoadedSession` (`WorkoutSessionViewModel.cs:299-300`). | — |
| J01 | Onboarding end to end | BLOCKED | No app. API: challenge → verify → register 201 (`onboardingComplete: true`) → login 200 at this tip. | — |
| J02 | Incomplete onboarding | BLOCKED | No app. API: goal "muscle tone" + pace answer → 400 `InvalidQuiz` (expected rule). | — |
| J03 | Plan inspection | BLOCKED | No app; plan acceptance was not driven over the API. | — |
| J04 | Injury restrictions and substitutions | BLOCKED | No app. | — |
| J05 | Per-set logging | BLOCKED | No app. | — |
| J06 | Preview / Apply | BLOCKED | No app. | — |
| J07 | Schedule / reschedule | BLOCKED | No app. | — |
| J08 | Session logging and completion | BLOCKED | No app. | — |
| J09 | History | BLOCKED | No app. | — |
| J10 | Cycle | BLOCKED | No app. | — |
| J11 | Nutrition | BLOCKED | No app. | — |
| J12 | Measurements | BLOCKED | No app. | — |
| J13 | Offline / retry | BLOCKED | No app. | — |
| J14 | Russian at large text | BLOCKED | No app. Static: EN/RU 2036/2036 keys, 0 missing, 0 format mismatches; longest RU action labels listed in the styling audit. | — |
| J15 | First-run install | BLOCKED | No app. | — |
| CR01 | Plan → session → back ×20 | BLOCKED | No app. | — |
| QA01 | Physical iPhone acceptance | BLOCKED | No physical device was used, and no app binary exists. | — |

Counts: **PASS 0 · FAIL 0 · BLOCKED 21 · NOT RUN 0.**

## Supplementary checks (not in the 21-row record)

| ID | Result | Observation |
| --- | --- | --- |
| E-01 | BLOCKED (device); server half PASS | API age gate at this tip (today 2026-11-09): birth 2009-11-10 → 400 `InvalidProfile`; 2008-11-09 (18 today) → 201. Registration accepts up to 120; 18–64 is enforced at planning (`WorkoutPopulationScopeRule`). Code findings Q-02/Q-03 below. |
| E-02 | BLOCKED (device) | Key `Workout_Recommendation_StartingWeight` has EN and RU values (`UiTextCatalog.CycleWorkout.cs:503`). |
| E-03 | BLOCKED (device) | `RegisterPage.xaml:61` username entry `Keyboard="Plain"`. |
| U-09 | BLOCKED (device) | `Workout_Featured_*` keys used (`WorkoutViewModel.cs:654-720`). |
| U-10 | BLOCKED (device) | `Dashboard_Workout_Count*` keys used (`DashboardViewModel.cs:230-236`). |
| G05 | BLOCKED | No plan was accepted in this run; CI `verify_plan_generation` green at tip. |
| G08 | BLOCKED (UI); API PASS | Registration journey succeeds against the local API at this tip. |
| G10 | BLOCKED | No session was logged. |
| G13 | BLOCKED | Restart/offline need the app. Local DB + API were used for the API steps above. |
| T-21 | CONFIRMED OPEN | Core.Tests build: 0 errors, 6 warnings = 4 xUnit analyzer (xUnit1030 ×1 `BackendApiRefreshFailureTests.cs:183`, xUnit1031 ×2 `CycleOnboardingAnchorTests.cs:44,64`, xUnit1025 ×1 `TodayCheckInScoreEnergyTests.cs:26`) + 2 NU1900 (sandbox network). AUDIT.md's "xUnit1031 ×4" overstates the count. Not warning-free. |

## Defects / findings from this run

| ID | Sev | Finding | Reproduction | Status |
| --- | --- | --- | --- | --- |
| Q-01 | P0 (gate) | Simulator acceptance cannot run in the agent sandbox (MSBuild needs Unix sockets) | Run the build command inside the agent sandbox | Environment; owner unblocks |
| Q-02 | P2 | Stale age error: `OnBirthDateChanged` sets `ErrorMessage = "Ui_Onboarding_Error_Age"` but clears only when it equals the old English sentence, so the error never clears when the date is corrected | `ProfileSetupViewModel.cs:170-178`; reachable only if an under-18 date reaches the VM (the picker max is today−18y) | Code-level; needs device check |
| Q-03 | P2 | A user whose 18th birthday is today cannot confirm the pre-filled default date: selection registers only on a change, and Continue rejects an untouched picker | `ProfileSetupViewModel.cs:24,160-166,211`; leave the picker untouched and tap Continue | Hypothesis; needs device check |
| Q-04 | P1 | Caption text uses bare `Primary` #C85A87 (no dark variant): 3.99:1 on light card, 3.42:1 on dark card, below 4.5:1 | `FeaturedWorkoutCard.xaml:243`, `StrengthPlanGenerationPage.xaml:736` | Static; see styling audit S-01 |
| Q-05 | P3 | RU mistranslation `Cycle_QuickLog_LoadingSavedEntry` = "Загрузка сохраненного входа…" ("saved login") | Catalog | Static |

## Not run and why

- All UI rows: no app binary (Q-01).
- `dotnet test` suites: testhost socket denied; CI run 42687031242 at `f1918e1` is the only current-tip test evidence.
- QA01: no physical device and no binary.
- Live AWS: not touched. The concrete actions (A1–A6, A11) need owner-specific targets and approvals; a general
  instruction is not enough.
