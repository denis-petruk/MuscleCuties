# MuscleCuties handoff and input register

Start with the [implementation plan](IMPLEMENTATION_PLAN.md), then use the [orchestration prompt](ORCHESTRATOR_PROMPT.md). The [simulator checkpoint](../2026-11-05-simulator-checkpoint.md) is the incident record.

The [RM07 checkpoint](RM07_HANDOFF.md) records the owner's P0 acceptance and the scoped local candidate work. The [approved Beta workout policy](WORKOUT_POLICY.md) records PD00 and governs the workout behavior.

## Resume from here

| Field | Current evidence |
| --- | --- |
| Owner decision | Simulator debug data path resolved (P0 completed). Next phase: In-app simulator verification against AWS, fast-forwarding Rider `main`, and progressing QA01 / TF00–TF02. UX02-RUN implementation is READY FOR REVIEW; result acceptance is pending (see the November 6 stage record). |
| Observed Rider checkout | `main` at `d840d62745337a604f56f4ba32568fb66ae53158` in `/Users/denispetruk/RiderProjects/MuscleCuties`. Port 5080 fix applied to `AppEndpointConfiguration.cs`. Untracked audit files present. |
| Selected candidate | `continue_musclecuties_beta_plan` at `6e38535756494ab3e40a75cc74109ad247727213` in `/Users/denispetruk/.gemini/antigravity/worktrees/MuscleCuties/continue_musclecuties_beta_plan`. 1,346 automated tests green. Port 5080 fix applied. |
| P0 Simulator Status | **COMPLETED & VERIFIED.** Port 5000 AirPlay collision diagnosed. Port 5080 configured in `AppEndpointConfiguration.LocalBaseUrl`. Simulator `backend_api_base_url` pointed to AWS API Gateway (`https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`). App booted cleanly to login screen. Latency measured at 124–165ms warm, RDS PostgreSQL healthy. |
| Current authorized files | Full corrective beta plan in MuscleCuties. Local changes, simulator testing, and test runs authorized. Remote git pushes, cloud mutations, and external releases require owner approval. |
| Next pending packages | 1. **QA01 (In-App Journey Verification):** Execute authentication / workout generation on booted iPhone 16 simulator with AWS backend.<br>2. **Candidate Integration:** Fast-forward / merge candidate `6e385357` into Rider `main`.<br>3. **PL03/PL04:** Isolated Staging & backend release verification.<br>4. **TF00–TF02:** Code signing, device archive, and TestFlight preparation. |

## Inputs and credentials

Collect the following in order. Verify presence and effective target by identifier, role, test or fingerprint. Use protected storage for values and record only sanitized references and verification outcomes in this handoff.

| When | Input or exact name | Source, destination, and status |
| --- | --- | --- |
| Simulator now | Build/device and API URL | Owner supplied Rider Debug and iPhone simulator. Verify effective address. `backend_api_base_url` is the existing MAUI preference. Confirm a free loopback port and matching API listener; 5080 was available during the check. The server can bind with `ASPNETCORE_URLS`; the client resolves its own URL. |
| Simulator now | `ConnectionStrings__PostgreSql`, `Authentication__SigningKey` | Generate a disposable loopback database connection and fresh local key of at least 32 UTF-8 bytes. Keep them in the development secret mechanism and use a synthetic account. No shared AWS, Apple or Beta credential is needed for this gate. |
| Simulator now | `Authentication__Issuer`, `Authentication__Audience` | Existing local defaults apply unless the candidate uses a deliberate override. Treat them as non-secret configuration and verify token issuance and validation agree. |
| Affected disposable DB tests | `MC_PREPARE_DISPOSABLE_WORKOUT_DATABASE`, `MC_REGISTRATION_WRITE_TEST_POSTGRES`, `MC_REGISTRATION_TEST_POSTGRES`, `MC_RETRY_TEST_POSTGRES`, `MC_SCHEMA_COMPATIBILITY_POSTGRES`, `MC_PG09_LOAD_POSTGRES` | Documented in `db-verify.yml`. Select the variables required by the affected suites and use only guarded loopback fixtures. |
| After simulator, when GitHub/AWS work is approved | Environment identity, `AWS_REGION`, `AWS_RELEASE_BUCKET`, OIDC artifact upload/Staging deploy/Beta deploy roles | Repository configuration supplies expected names and target. Platform verifies live role trust, GitHub environment/reviewer and audience/subject, region/account, bucket versioning and least privilege. Use short-lived role credentials. This audit verified read-only access to the expected Beta API, not release-role authority. |
| After simulator, when shared PostgreSQL work is approved | GitHub environment secret `DATABASE_CONNECTION_STRING` → `ConnectionStrings__PostgreSql` | The protected DB workflow expects a dedicated migrator, verified target/version/lock and restore rehearsal. Platform validates the resolved target and grants before use. The runtime API gets its own least-privilege connection. Never infer environment identity from the resource name. |
| After simulator, when backend/worker is approved | API `Authentication__SigningKey`, `Authentication__Issuer`, `Authentication__Audience`; worker `MUSCLECUTIES_WORKOUT_DISCOVERY_CONNECTION` and `MUSCLECUTIES_WORKOUT_TENANT_CONNECTION` | Use environment-scoped secret references and dedicated principals. Verify distinct worker grants to the same intended database, TLS/VerifyFull and fail-closed runtime checks. Keep API auth secrets and database passwords in the approved secrets store. |
| Approved release verification | `MC_RELEASE_POLICY_FILE`, `MC_DB_LOCK_RUNNER`, `MC_SYNTHETIC_BEARER_TOKEN` where the reviewed harness consumes them | Confirm current GitHub implementation versus historical GitLab adapters. Select candidate-bound policy and shared lock. Use an approved short-lived synthetic identity for target smoke and keep its bearer value out of docs and logs. |
| Approved Apple signing and TestFlight | Apple Developer Team ID, distribution identity and provisioning profile, App Store Connect app access, bundle ID `com.musclecuties.app` | Owner chooses owner-operated signing or a protected automation path when the gate opens. Verify signing and upload authorization separately. Keep signing private material in the owner Keychain or approved protected store. For API upload, verify the key ID, issuer and scoped access there. The candidate's device workflow establishes no signed archive proof or validated distribution-secret schema. |

Provider settings apply only to authorized feature acceptance. Confirm which provider path is in use before requesting any input.

| Feature | Candidate configuration to verify |
| --- | --- |
| Apple sign-in/revocation | `AppleSignIn__Audience`, `AppleSignIn__TeamId`, `AppleSignIn__KeyId`, `AppleSignIn__PrivateKeyPem`, and `AppleSignIn__PublicKeys__<kid>` where applicable. These settings serve backend identity and revocation. |
| Email challenges | Provider key `Resend__ApiKey`, `RESEND_API_KEY`, or `EmailChallenges__ResendApiKey`, with `Resend__FromEmail` or `EmailChallenges__FromEmail`; relay path `EmailChallenges__RelayEndpoint` and `EmailChallenges__RelayBearerToken`. Verify `EmailChallenges__ActiveHmacKeyId` and `EmailChallenges__HmacKeys__<kid>` for the selected environment. |
| Email receipts | `EmailReceipts__ActiveKeyId`, `EmailReceipts__PrivateKeyPem`, `EmailReceipts__PublicKeys__<kid>`, `EmailReceipts__Issuer`, and `EmailReceipts__Audience` where the flow is enabled. |
| Media and cleanup | `Storage__S3__BucketName` and `Storage__S3__Region` with verified execution-role permissions. Temporary AWS credentials come from the runtime. Verify `ConnectionStrings__PhotoCleanupDispatch` and `ConnectionStrings__PhotoCleanupProcessor` for the dedicated cleanup paths when applicable. |

The owner's current inputs are recorded. Next ask only for a missing execution decision once the simulator package and exact scope are reviewable. Treat source-derived names as configuration inventory. Presence and privilege in protected environments require their own verification.

## Save before a limit

Write the next checkpoint while context is available. Include the package, base/head SHA, actual changed files and reservations, approvals and exclusions, source/artifact/configuration identities, relevant commands and results, remaining checks, recovery requirements, and the exact next action. Record where any stopped local service or disposable fixture can be safely recreated. Resume through a supported continuation with scope and drift checked again.

## Stage record — November 6, 2026 (P0 re-verification and UX02-RUN)

**State:** READY FOR REVIEW. Result acceptance pending. No remote write, push, deploy, or release was performed.

| Item | Evidence |
| --- | --- |
| Source | Rider `main` at `0af0bef5ba434339cfb5d09f9e177d5c322c2038` (tree `6850656e8e83418c7c227b63e1a53675f0bbfc3c`), worktree `/Users/denispetruk/RiderProjects/MuscleCuties`. |
| Preserved unrelated work | Pre-existing changes kept untouched: `WorkoutPlanAndSessionEndpointMappings.cs`, `WorkoutPresentationModels.cs`, `WorkoutViewModel.cs`, both audit docs, and untracked `BetaWorkoutScope.cs` / `BetaWorkoutScopeTests.cs` (rescheduling gate). Not reviewed or adopted here. |
| UX02-RUN changed paths | `src/MuscleCuties.UI/ViewModels/Workout/WorkoutSessionViewModel.cs`, `src/MuscleCuties.UI/Pages/Workout/WorkoutSessionPage.xaml`, new `src/MuscleCuties.UI/Services/Workout/TimedActivityProgress.cs`, `UiTextCatalog.CycleWorkout.cs`, `RussianUiText.CycleWorkout.cs`, new `tests/MuscleCuties.Mobile.Contracts.Tests/TimedActivityProgressTests.cs` plus its `.csproj` link. |
| Implementation (4H) | A timed activity now enters a visible `SessionScreen.TimedActivity` surface on successful start: prescribed warm-up/main/cool-down phase derived from confirmed active time plus the activity's own prescription, elapsed clock, remaining time and progress when a total is known, and Pause/Resume/Finish. No second timing authority: values come from `_confirmedActiveSeconds` plus the existing `_plannedSessionTimer`. A duration-only activity shows elapsed time only and an honest no-phases note. Finish persists actuals through the existing planned-session finish route, then opens a summary where strength volume/PR cards are hidden and time is shown. |
| Implementation (4I) | Abandon is now confirmed, single-flight, and reconciles an uncertain outcome: a retry reconciles the server session instead of sending a second abandon. Cancel preserves the active state and confirmed time. Success cancels the timing loop and display timer, clears busy/overlay/pending state, and performs one bounded return to the opener; the Workout page reloads on appearance, so the earlier "Loading your data" loop is not re-entered. `FinishActivityAsync` no longer re-finishes an already committed session. |
| Checks run | `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64` → **Build succeeded, 0 errors, 0 warnings** with `MauiStrictXamlCompilation` and compiled bindings. `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests` → **66 passed, 0 failed** (10 new phase/countdown checks). |
| Installed binary | `MuscleCuties.UI.app` built Nov 6 09:03:36 2026, installed and launched on booted iPhone 16 `60DD8563-A1D6-4FE5-9F4F-2BFB975B194B`. |
| Effective endpoint | Simulator preference `backend_api_base_url` = `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com` (the owner-accepted P0 target); no loopback override present. |
| Endpoint evidence | AWS: `/health/live` 200 (96 ms), `/health/ready` 200 (301 ms), anonymous `/api/v1/session` 401 (108 ms). Loopback API on `127.0.0.1:5080`: `/health/live` 200, `/health/ready` 200, anonymous session 401. |
| Authenticated data load | On this exact binary the synthetic account dashboard rendered with today's Running workout, readiness and recovery scores: `docs/release-audit/2026-11-05/evidence/ux02-run-boot.png` (synthetic data, no personal records). |
| Not verified | Simulator tap-through of start → running surface → pause/resume → finish summary, and abandon confirm/cancel/uncertain-retry; background/foreground; strength-session baseline; EN/RU and light/dark screenshots; large text, keyboard and VoiceOver. No non-UI suite beyond Mobile Contracts was re-run. UX02-ACT activity rollout is untouched. |

**Owner decision entries (kept separate)**

1. **Plan approval** — the November 6 implementation plan and orchestrator prompt are the governing revision; this stage implements its UX02-RUN package and the P0 re-verification it prescribes.
2. **Execution authorization** — the current request authorizes local source edits, the iOS simulator build/install/run/screenshot, and local test runs. It does not authorize pushes, cloud or database mutation, signing, or release actions; none were attempted.
3. **Result acceptance** — PENDING. The stage is ready for owner review; acceptance of the observed simulator behavior requires the tap-through listed as not verified.

**Next action:** owner reviews the diff and, on the installed binary, walks start → running surface → pause/resume → finish summary and abandon confirm/cancel; then UX02-ACT can proceed on the reviewed lifecycle.

## Stage record — November 6, 2026 (UX02-ACT, all non-strength timed activities)

**State:** READY FOR REVIEW. Result acceptance pending. No remote write, push, deploy, or release was performed.

| Item | Evidence |
| --- | --- |
| Source | Rider `main` at `0af0bef5ba434339cfb5d09f9e177d5c322c2038` (tree `6850656e8e83418c7c227b63e1a53675f0bbfc3c`). Pre-existing workout-scope files and both audit docs remain untouched. |
| UX02-ACT changed paths | `src/MuscleCuties.UI/Services/Workout/TimedActivityProgress.cs` (activity classification beside the phase rule), `src/MuscleCuties.UI/Models/UI/Workout/WorkoutActivitySectionItem.cs` (`IsTimedActivity`), `src/MuscleCuties.UI/ViewModels/Workout/WorkoutSessionViewModel.cs` (tag-based routing, rest-day start guard, localized summary), `UiTextCatalog.CycleWorkout.cs`, `RussianUiText.CycleWorkout.cs`, `tests/MuscleCuties.Mobile.Contracts.Tests/TimedActivityProgressTests.cs`. |
| Shared lifecycle | One rule decides the surface: `TimedActivityProgress.IsTimedActivityTag` treats every non-strength, non-rest tag (cardio, recovery, yoga, climbing, case-insensitive) as timed, and an unknown or empty tag stays on the strength flow so a section that cannot be classified never loses its logging inputs. `StartActivityAsync` routes by `SelectedActivity.IsTimedActivity` rather than by exercise count, so all timed activities use the same `SessionScreen.TimedActivity` surface, the same confirmed-time-plus-stopwatch authority, the same Pause/Resume/Finish, the same finish route and the same summary. No second timer or parallel path was added. |
| Per-activity behaviour | Phase, elapsed, remaining and progress come from each activity's own prescription: continuous cardio (walking, cycling, running, swimming, rowing, elliptical) uses 5/20/5, HIIT uses its 10-minute warm-up, climbing its longer main block, and yoga, mobility and gentle recovery show a main phase with no invented warm-up or cool-down. Prescription-free and zero-minute activities show elapsed time plus the “no timed phases” note. Finish persists actuals through the existing planned-session finish route and opens the summary with the strength-only volume/PR cards hidden and time shown. Abandon keeps the reviewed confirm, single-flight and uncertain-outcome reconciliation behaviour unchanged. |
| Localization | EN and RU added for the timed summary duration, the under-a-minute value and the not-applicable placeholder; no new user-facing literal is hardcoded in the timed path. |
| Checks run | `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64` → **Build succeeded, 0 errors, 0 warnings** under strict XAML compilation with compiled bindings. `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests` → **84 passed, 0 failed**, including per-activity phase and countdown coverage for 11 activity prescription shapes, tag-classification checks for cardio/recovery/yoga/climbing versus strength/rest/empty/null, and the zero-minute and negative-elapsed edges. |
| Installed binary | Rebuilt `MuscleCuties.UI.app` installed and launched on booted iPhone 16 `60DD8563-A1D6-4FE5-9F4F-2BFB975B194B` (launch PID 79971). |
| Endpoint and data load | Effective `backend_api_base_url` remains `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`; the app rendered the signed-in synthetic dashboard with today's Running workout: `docs/release-audit/2026-11-05/evidence/ux02-act-boot.png`. |
| Surface rule | One rule decides the surface (`TimedActivityProgress.DecideSurface`): rest days never start; a section without planned movement rows is always a timed activity; a section that carries movement rows keeps the exercise flow because those rows record distance, pace, heart-rate and power actuals that the planned-session finish route does not persist. The planner assigns movement rows only to true strength activities, so every non-strength activity day — including rock climbing, which is persisted as a strength-type day without rows — reaches the timed surface. Unknown tags follow the same structural rule, so no section is stranded on an empty exercise flow. |
| Not verified | On-device tap-through per activity (start → running surface → pause/resume → finish summary, and abandon confirm/cancel) remains unverified: no tap-automation input is available in this environment, and the requirement stands as a manual walk on the installed binary. HIIT history gating and abandoned-activity history readback were not re-exercised at runtime this stage. |

**Owner decision entries (kept separate)**

1. **Plan approval** — the November 6 implementation plan and orchestrator prompt remain the governing revision; this stage implements UX02-ACT on the reviewed UX02-RUN lifecycle.
2. **Execution authorization** — the current request authorizes local source edits, the iOS simulator build/install/run/screenshot and local test runs. No push, cloud, database or release action was attempted.
3. **Result acceptance** — PENDING. Acceptance requires the manual per-activity walkthrough on the installed binary listed as not verified.

**Next action:** owner walks one cardio activity, one interval activity (HIIT) and one recovery activity on the installed binary, then QA00 can assemble integrated evidence.

## Stage record — November 6, 2026 (UX02-ACT routing closure)

**State:** READY FOR REVIEW. Result acceptance pending. No remote write, push, deploy, release or walkthrough harness was produced.

**Code inspection that motivated the closure.** The planner creates movement rows only for true strength activities (`PostgresAcceptStrengthPlanProposalStoreV020`, `if (!IsStrengthActivity(activity) …) continue;`). Rock climbing is persisted as `WorkoutType.Strength`, so its section is tagged `STRENGTH` while carrying zero rows. The earlier tag-plus-rows rule therefore routed a climbing day into the exercise flow with no exercises to log, and the previous report's claim that every non-strength tag reaches the timed surface was inaccurate for that path.

| Item | Evidence |
| --- | --- |
| Source | Rider `main` at `0af0bef5ba434339cfb5d09f9e177d5c322c2038` (tree `6850656e8e83418c7c227b63e1a53675f0bbfc3c`). Pre-existing workout-scope files and both audit docs remain untouched. |
| Changed paths | `src/MuscleCuties.UI/Services/Workout/TimedActivityProgress.cs` (`WorkoutActivitySurface` and `DecideSurface` replace the tag-only helper), `src/MuscleCuties.UI/Models/UI/Workout/WorkoutActivitySectionItem.cs` (`IsTimedActivity` now reads `DecideSurface(Tag, HasExercises)`), `tests/MuscleCuties.Mobile.Contracts.Tests/TimedActivityProgressTests.cs` (routing table per activity type). The view model is unchanged in this stage: it already routes on `IsTimedActivity`, guards rest days out of Start, reuses the single confirmed-time-plus-stopwatch authority, and keeps the reviewed finish summary and abandon reconciliation. |
| Surface rule (final) | `DecideSurface(tag, hasPlannedMovements)`: rest tag → no start; movements present → exercise flow; otherwise → timed activity. It reads the tag only for rest, because the tag alone cannot distinguish climbing from strength while the row count can. A section with movement rows is the only place distance, pace, heart-rate and power actuals can be recorded, so it keeps that flow; a row-less section has nothing to log per exercise and always gets the timed lifecycle. |
| Checks run | `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64` → **Build succeeded, 0 errors, 0 warnings** under strict XAML compilation with compiled bindings. `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests` → **95 passed, 0 failed**, including the routing table for walking, cycling, running, swimming, rowing, elliptical, HIIT, climbing, yoga, mobility, gentle recovery, the three strength types, rest with and without rows, unknown and empty tags with and without rows, plus the existing per-activity phase/countdown checks and the zero-minute and negative-elapsed edges. |
| Installed binary | Rebuilt `MuscleCuties.UI.app` installed and launched on booted iPhone 16 `60DD8563-A1D6-4FE5-9F4F-2BFB975B194B` (launch PID 80729) against `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`; screenshot `docs/release-audit/2026-11-05/evidence/ux02-act-routing.png`. |
| Not verified | The on-device tap walkthrough (start → running surface → pause/resume → finish summary, abandon confirm/cancel) remains **unverified**: no tap-input automation is available here, and no result was invented for it. HIIT history gating and abandoned-activity history readback were likewise not re-exercised at runtime. |

**Owner decision:** result acceptance pending; no acceptance was requested or assumed in this stage.
