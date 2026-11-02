# MuscleCuties — Pre-First-Build Audit

Current owner-facing report for the unreleased iOS app. `STATUS.md` and `ROADMAP.md` remain the historical record; this file is the single current state, remaining work and first-build order.

- **Baseline commit:** `fbbfb9767ab24a01de97b26e99c12e557f921aed`.
- **Working tree at start:** an existing uncommitted worktree (planning rules, nutrition, server mappings, localization and tests, including three untracked planning/rule files and one untracked test file) was preserved. Nothing pre-existing was reverted, committed, pushed or deployed.
- **Scope of this pass:** Apply-as-acknowledgment, first-plan auto-apply, Apple Health UI removal, readiness-proposal removal, plan-preview and copy simplification, localization cleanup, documentation consolidation. No schema, migration, cloud or store change.

---

## 1. Resolved in this implementation

| Area | Change |
| --- | --- |
| Apply as acknowledgment | Pressing **Apply** now accepts the visible preview and its applicable exercise conditions. The separate acknowledge button, command, state set and round trip are gone from the plan page, ViewModel and catalogs. Apply re-evaluates a preview that only needs condition review, then accepts the resulting feasible proposal. |
| First-plan auto-apply | Reduced volume no longer blocks automatic first-plan application. `CanAutomaticallyApplyFirstPlan` keeps feasibility, persistence, replacement, required-answer, style-choice and omission gates. Auto-apply and manual Apply both build acceptance data through `WorkoutPlanningFlowRule.AcceptanceAcknowledgments`, which sets `ReducedVolumeAcknowledged` when required and carries the accepted exercise-condition IDs. |
| Readiness proposals | Removed the user-facing review/apply/keep workflow end to end: three server routes, contracts, use-case methods, store interface members, `PostgresReadinessAdjustmentStore` and its DI registration, client calls, dashboard commands/state and card UI. Internal daily readiness prescription logic (`CycleReadinessPrescriptionRule.DailyReadinessAdjustment`), reconciliation dose adjustment and their tests are untouched. No proposal data was deleted. |
| Apple Health UI | Removed the onboarding connect card, the profile health row, the health-sync page/ViewModel/use case/contracts/store interface, Shell route and DI registrations, `HealthConnection*`/`IsAppleHealthAvailable`/`HealthStatusText` UI state, the corresponding EN/RU keys and `NSHealthShareUsageDescription` from `Info.plist` and both `InfoPlist.strings`. Dormant services (`DisabledHealthSyncService`, `IHealthSyncService`, `IProfileHealthConnection`) remain registered and unactivated. |
| Plan preview layout | One notes surface consolidates scheduling notes, limitations, movement omissions and duration omissions; one **Plan details** expander holds activity decisions and strength sessions; the mixed-schedule order note and the duplicated notes/limitations/strength headings are removed, as are local typography overrides. Reduced-volume information stays as a read-only notice. |
| Copy and localization | 14 English strings over 150 characters were rewritten with aligned Russian translations (safety meaning retained). Missing `Workout_Generation_ChooseUnderlyingCardio` was added in both catalogs. Auth, OTP, recovery, quiz, daily check-in, profile setup/units/feedback/personal info/nutrition settings, nutrition meal/search/custom-food and workout-session user-facing literals now resolve through catalog keys in EN and RU. |
| Client acceptance compile gap | The plan page called `WorkoutPlanningFlow.AcceptanceAcknowledgments`, which the UseCases adapter did not expose, so the MAUI client could not compile. The adapter now delegates to `WorkoutPlanningFlowRule.AcceptanceAcknowledgments`. The UseCases project compiles with 0 errors, and the UI call site now compiles as well (see the compile-and-launch row above). |
| Localization gaps found in verification | The reduced-volume notice read `Workout_Generation_ReducedVolumeNotice`, which did not exist: English showed a hard-coded fallback and Russian showed English. Both catalogs now define the key and the fallback is gone; the unused `Workout_Generation_ReducedVolumeTitle` constant was removed. Resend countdown, OTP “sending code”, cycle quick-log/calendar/phase-edit statuses, injury site and status labels, dashboard and workout placeholder labels, nutrition zero-state copy and profile summary defaults now resolve through catalog keys with aligned Russian. |
| Cycle phase copy | The phase summary sheet and phase-explorer pills now resolve their names, taglines and five focus fields per phase through 32 catalog keys in EN and RU. The cycle guidance surface contains no remaining English literal; the phase-detail catalog was already key-based. |
| Client failure taxonomy | The API client keeps the exact HTTP status and the server `X-Request-Id` (or ProblemDetails `requestId`) reference on every failed response, and `ApiFailurePresenter` turns them into distinct instructions: retry for an outage, wait for a rate limit, sign in again when the session ended, contact support when a database deployment fault is reported. The workout session and strength-style offer paths use it instead of one connectivity message, and the session page shows the support reference when the server sent one. |
| Client compile and launch defects | The first iOS build of the client exposed four defects left by earlier passes, all fixed: `MauiProgram` still imported the deleted `Profile.HealthSync` namespace; `ProfileViewModel` and `ProfileSetupViewModel` lost the `MuscleCuties.UI.Services.Profile` import that `AppPageLoadGate`, `IProfileRefreshNotifier`, `ILocalPhotoCacheService` and `ProfileWorkoutActivityOptions` need; `StrengthPlanGenerationPage` placed two children inside the single-child `Expander.Content`; and the plan request assigned an `IReadOnlySet<int>` to a `HashSet<int>` parameter. The iOS simulator build now succeeds with 0 errors, and the app launches and stays resident. |
| Documentation | Removed the 21 superseded top-level documents and the stale `beta-orchestration`, `orchestration`, `v1` and `workout-migration-checkpoints` directories. Kept `docs/UI_PRESENTATION_DESIGN_SPEC.md` and the live operational runbooks under `docs/infra/`. README now points only to existing documents. |

Workout safety rules, the always-applicable strength requirement, injury substitution/omission behavior, internal readiness adjustments and all unrelated behavior are unchanged.

---

## 2. Deferred engineering work

1. **Offline nutrition and meal-failure fallbacks.** Purpose: keep meal logging usable without connectivity and distinguish retryable failures. Why deferred: requires product decisions on cached-day semantics and conflict handling. Rough scope: one client slice (cache read/write, conflict banner, tests), no schema change.
2. **Remaining screens on the failure taxonomy.** Purpose: the presenter exists and the workout session path uses it, but most other screens still show one connectivity message and expose no support reference. Why deferred: each screen has its own error contract, so they need a reviewed pass on a device. Rough scope: per-screen wiring plus the acceptance run in Section 4.
3. **Observed-set progression evidence for the frozen candidate.** Purpose: prove progression uses completed sets rather than planned volume. Why deferred: needs a frozen candidate and history fixtures. Rough scope: reconciliation evidence harness plus Core tests.
4. **Catalog provenance and exercise-floor review.** Purpose: confirm every prescribed movement has reviewed source data and a volume floor. Why deferred: needs reviewed source data from the owner/reviewer, not code invention. Rough scope: catalog review pass, data update, floor tests.
5. **Live injury-substitution reproduction.** Purpose: confirm on-device that an active injury still yields strength work with safe substitutions. Why deferred: needs device fixtures and acceptance runs. Rough scope: manual device scenario matrix plus any resulting fix.

---

## 3. Owner-side blockers and decisions

Each item: **Required action · Why it matters · Evidence needed to close it.**

### Data safety
- Isolate the beta database from staging, or complete and document the PITR restore drill · Staging and production currently resolve to one `musclecuties_staging` database · Restore log with timestamps, restored schema fingerprint and a post-restore `dbcheck`.
- Record RPO/RTO for that database · Defines acceptable loss and recovery time before testers create data · Signed values in the runbook.
- Verify `REC-01` status · Recovery gate is unconfirmed · Owner evidence that the control is implemented or explicitly waived.

### Secrets and authentication
- Confirm rotation of DB, JWT, HMAC, RSA and Resend secrets; confirm Sign in with Apple configuration and revocation prerequisites · Leaked or stale credentials invalidate the beta · Rotation record (names and dates, never values) plus a successful sign-in and revocation test.
- Confirm Apple token revocation and account-deletion behavior with a real credential · Required for account deletion and store review · Test log from a physical device.

### Deployment
- Decide whether beta uses staging or a separate production environment · Determines credentials, data isolation and rollback · Written decision and the matching Lambda/database target.
- Verify deployment approval mechanisms (manual CI job, HMAC release authorization, lock owner) · Prevents accidental production deploys · Dry-run blocked-deployment report and an approved release record.
- Reconcile `schema_fingerprint_sha256` and run `dbcheck` against the chosen environment · Drift blocks readiness and can corrupt planning state · `dbcheck` output showing `compatible` at the recorded fingerprint.

### Background processing
- Verify the reconciliation EventBridge rule is enabled in the chosen environment · Pending workout revisions must be processed · Rule state, recent successful invocations and an empty DLQ.
- Confirm DLQ visibility and that `NeedsReview` is observable without failing the schedule · Silent failures would hide unresolved workouts · Alarm/runbook evidence and a synthetic `NeedsReview` log entry.

### Media
- Validate presigned upload, commit, download, expiry and deletion against the live bucket · Photo vault and progress photos depend on it · End-to-end request log, object lifecycle check and an expired-URL negative test.

### Apple distribution
- Change `ApplicationVersion` from 2 to 3 only after acceptance checks pass · Version churn before acceptance complicates TestFlight builds · Owner confirmation after device acceptance.
- Provision certificates/profiles, sign, generate and export the IPA, complete export compliance, App Store Connect metadata, testing groups and beta review · No installable build exists without these · Accepted TestFlight build.
- Confirm capabilities include Sign in with Apple and Associated Domains, and exclude HealthKit unless explicitly approved · Capability mismatch breaks auth or misdeclares data use · Entitlements screenshot and App Store Connect capability list.

### Legal and public surface
- Deploy the static support/privacy site and confirm AASA correctness · Associated Domains and support links must resolve · Live URLs returning the expected content and a valid `apple-app-site-association`.
- Review public copy, privacy policy, terms, support details, App Privacy disclosures and email sender configuration · Store review and user trust depend on accurate declarations · Published pages plus the App Privacy answers.

### Device acceptance
- Run the physical-iPhone checklist: install, sign in, AutoFill, barcode camera, photos, nutrition, workout planning, injury flows, cycle, offline behavior, account deletion, EN/RU · The MAUI client was not built or run in this pass · Signed acceptance log per scenario.

### Owner decisions
- Whether HealthKit belongs in the beta · The UI is removed and the dormant seam is intact; re-adding needs privacy text, capability and App Privacy review · Written decision.
- Whether user-facing readiness proposals should ever return · The internal logic remains; the rejected public workflow is gone · Written decision.
- Whether the camera barcode scanner is in the advertised first-build scope · It is wired to the food log but not device-verified here · Written scope decision.

---

## 4. First-build sequence

1. Isolate or restore-verify the beta database; record RPO/RTO and `REC-01`.
2. Rotate and confirm all secrets; verify Sign in with Apple and revocation.
3. Choose the beta environment; reconcile the schema fingerprint and pass `dbcheck`.
4. Deploy the backend and the reconciliation worker; verify EventBridge, DLQ and `NeedsReview`.
5. Provision signing and capabilities; build and export the IPA.
6. Run the physical-device acceptance checklist in EN and RU.
7. Validate media upload/expiry/deletion against the live bucket.
8. Publish the support/privacy site, AASA, store metadata and App Privacy answers.
9. Only after acceptance, raise `ApplicationVersion` and submit the beta build.

---

## 5. Verification evidence and limitations

**Executed**

- `dotnet build` UseCases, Infrastructure.PostgreSql, Server, WorkoutReconciliation: 0 errors. SDK 10.0.203 was used from `/tmp` with absolute paths because the repository pins 10.0.403; `global.json` was not edited. Three pre-existing analyzer warnings (CA1859 ×2, CA2263) appear in Core; no new warnings were introduced by this pass.
- Release configuration, which Debug-only checks had not covered, was verified from a clean tree with all `bin`/`obj` deleted first: Server, DbMaintenance and WorkoutReconciliation each reported **Build succeeded, 0 errors** against a full restore and rebuild. The three Core analyzer warnings above are the only output. Nothing was packaged or deployed; the regenerated outputs were removed afterwards so the workspace ships as source only.
- `dotnet test` Core.Tests: **1029 passed, 0 failed, 0 skipped** (six of these are the new request-reference tests).
- `dotnet test` Server.Tests: **110 passed, 0 failed, 0 skipped**, including retired-route coverage for all three readiness-proposal shapes.
- `xmllint --noout` on every edited XAML file (`StrengthPlanGenerationPage`, `ProfileSetupPage`, `ProfileDetailsContent`, `ReadinessRecoveryCard`): all well-formed.
- Localization (reference-aware, not grep counts): 1652 keys in each catalog, no key present only in EN or only in RU, all 850 XAML `LocalizedText` references and all 195 literal `UiTextCatalog.Get("…")` references resolve, and the removed `Workout_Generation_AcknowledgeExerciseConditions` key has no reference. Seven duplicate EN and three duplicate RU registrations remain; they pre-date this pass and were not introduced here. Measured after the rewrite: no English value exceeds 150 characters and no Russian value exceeds 200.
- User-facing literals: a scan of `Status`/`Message`/`ErrorText`/`Text`/`Label` assignments across the targeted ViewModels finds 0 non-key English literals. The 32 cycle phase-summary and explorer-pill literals are now catalog keys in EN and RU, and `CycleViewModel.Guidance.cs` contains no string literal assignment outside catalog lookups.
- `dotnet build` MuscleCuties.UI for `net10.0-ios`, Debug, `iossimulator-arm64` (SDK 10.0.203 from `/tmp`): **build succeeded, 0 errors**. 32 analyzer warnings remain in four pre-existing categories (CA1863 16, CA1305 11, CA1001 3, CA1822 2); one of them is in the new `ApiFailurePresenter` (`string.Format` over a catalog-resolved format string).
- Installed to the booted iPhone 17 Pro Max simulator and launched with `simctl`: the process stayed resident for the full observation window with no managed exception, XAML parse error, binding failure or crash report. The first two launches aborted inside `mono_init` with `Failed to load AOT module 'System.Private.CoreLib' ... doesn't match assembly`, caused by AOT artifacts left in `obj`/`bin` by an earlier toolchain; deleting the iOS build output and rebuilding removed it, and the fresh bundle logs no such error. Why it recurs: the bundle copies `System.Private.CoreLib.dll` from the installed runtime pack (still carrying its pack date) while `System.Private.CoreLib.aotdata.arm64` is regenerated per build, so an incremental build after an SDK or workload change can pair a new AOT image with the pack's older corlib — and the abort happens in `mono_init` before any app code. Recover by deleting `obj`/`bin` output for `net10.0-ios` and rebuilding; `dotnet build` reports success in both cases, so a successful build alone does not prove the bundle starts.

**Not verified**

- The owner lifted the client build gate for one session, so the UI was compiled and launched once (see Executed). Nothing beyond startup was exercised: no screen flow, layout, VoiceOver behavior or runtime state was checked, because the notification-permission prompt covered the first screen and could not be dismissed without Accessibility control (`osascript` was blocked, and `simctl privacy grant notifications` is not a supported service), after which the simulator screenshot API failed on this device. Runtime binding correctness, the new failure-taxonomy copy and the workout-session reference label therefore remain verified only by compilation, XAML well-formedness and reference checks.
- No physical-device or simulator acceptance was performed.
- No cloud operation was executed: no deployment, EventBridge check, DLQ inspection, bucket lifecycle test, AASA fetch or App Store Connect change.
- Staging state, schema fingerprint and secret rotations were not independently checked in this pass.
- Localization validation is structural (keys and duplicates), not a native-language review.
