# TF00–TF02 — Release Candidate Verification & Promotion Evidence Packet

**Date:** 2026-11-05  
**Candidate Worktree:** `/Users/denispetruk/.gemini/antigravity/worktrees/MuscleCuties/remediate_musclecuties_ui_ux`  
**Candidate Branch:** `remediate_musclecuties_ui_ux`  
**Candidate Git SHA:** `3fe95021` (synced with Rider `18e00858`)  
**Target Git Repository:** `denis-petruk/MuscleCuties` (GitHub)  
**Authority:** Single Integration Authority  
**Current Promotion State:** **READY FOR RELEASE REVIEW** (Product & verification decisions closed; simulator walkthrough captured; pending final PR merge and iOS archive signing)

---

## 1. Executive Summary

This release candidate packet consolidates all corrective beta plan implementations and the full UI/UX & Backend Remediation scope. All automated domain, contract, registration, and server tests pass. Clean Architecture boundaries (Core independent of EF Core, ASP.NET, and MAUI) and backward-compatible wire contracts have been strictly preserved.

### Release Invariants Verified
1. **Zero Destructive DB Operations:** No `dbinit` or `EnsureCreated` on shared environments.
2. **Deterministic Versioned Migrations:** Ordered scripts 001 $\to$ 002 $\to$ 003 tracked in `ops.schema_migrations` with SHA-256 verification and advisory lock protection.
3. **Apple App Store & Privacy Compliance:** `PrivacyInfo.xcprivacy` verified with `CA92.1` reason for `UserDefaults`, `NSPrivacyTracking=false`, zero tracking domains.
4. **Single Dose & Safety Authority:** Composed readiness cycle dose rule with Fatigue Safeguard (withholding extra sets on readiness $< 75$) and structured pain trigger substitution.
5. **Schedule Multi-Session Display & Status Transitions:** 7-day strip renders ordered session chips without truncation; overview cleanly transitions upon session completion.
6. **Package P0 Port Collision Fix:** `LocalBaseUrl` relocated from port 5000 to port 5080, eliminating macOS AirPlay Receiver port collision on test runner.
7. **Remediation Scope (NM01–NM03, CR01, PG01):** Grouped meal editor with portions & drafts, micronutrient coverage rule & breakdown presenter, upfront plan replacement disclosure with auto-activation of eligible plans, and native alerts for injury adaptations.

---

## 2. Automated Test Verification

| Test Suite | Project Path | Tests Executed | Passed | Failed | Status |
|---|---|---|---|---|---|
| **Core Domain & Architecture** | `tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj` | 1,190 | 1,190 | 0 | **PASS** |
| **Mobile Contracts & UI Presenters** | `tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj` | 56 | 56 | 0 | **PASS** |
| **Registration & Cloud Contracts** | `tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj` | 43 | 43 | 0 | **PASS** |
| **Mobile Client Build (`net10.0-ios`)** | `src/MuscleCuties.UI/MuscleCuties.UI.csproj` | Simulator build | 0 warnings | 0 errors | **PASS** |

### Mobile Client Compilation
- **Target Framework:** `net10.0-ios`
- **Configuration:** `Debug` (with simulator architecture `iossimulator-arm64`)
- **Compilation Outcome:** Build succeeded with **0 warnings, 0 errors**.
- **XAML Invariant:** `MauiXamlInflator=SourceGen` verified clean.

---

## 3. Ordered PostgreSQL Migration Rehearsal (QA00)

The schema lifecycle has transitioned from legacy `dbinit` to versioned, idempotent migration scripts executed under serializable isolation and protected by advisory lock.

| Version | Script Name | Resource Name | SHA-256 Checksum | Execution Type |
|---|---|---|---|---|
| **001** | `001_base_schema.sql` | `MuscleCuties.Infrastructure.PostgreSql.Persistence.Schema.001_base_schema.sql` | `014264c72d6c9682e13a56a5adcb56a3e408d74bd73d3b70c32027062ca05ea8` | Idempotent table & index supplement |
| **002** | `002_schema_fingerprint.sql` | `MuscleCuties.Infrastructure.PostgreSql.Persistence.Schema.002_schema_fingerprint.sql` | `2601776a62a49a2095ec43175aa72f8320b3851dba2e42c0221fbcb09fb512a2` | RLS security policies, roles, grants |
| **003** | `003_schedule_seam.sql` | `MuscleCuties.Infrastructure.PostgreSql.Persistence.Schema.003_schedule_seam.sql` | `0cc67684059dd6b8f9c9361aa157f520c27c77e25332962e13e7137a47cd1681` | Multi-session schedule & log indexes |

- **Tracking Table:** `ops.schema_migrations (version, script_name, checksum_sha256, applied_at_utc)`
- **Metadata Singleton:** `ops.schema_metadata (singleton, schema_version, model_fingerprint, applied_at_utc)`
- **Rehearsal Verification:** Tested via `SchemaMigrationsContractTests.RehearseChainAsync_ProducesVerifiedChecksumsAndMonotonicOrder`. Idempotent re-runs skip applied scripts; checksum divergence fails closed.

---

## 4. Endpoint Health & Probe Status

Target deployment API Gateway in `us-east-2`: `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`

| Route | Expected Response | Probe Result | Functionality Verified |
|---|---|---|---|
| `GET /health/live` | HTTP 200 OK | **200 OK** | Container process liveness, minimal startup probe |
| `GET /health/ready` | HTTP 200 OK (`Healthy`) | **200 OK** | Relational DB connectivity & migration schema version match |
| `POST /api/v1/accounts/registration/quiz` | HTTP 200 OK | **200 OK** | Unauthenticated public registration flow |
| `GET /api/v1/session` | HTTP 401 Unauthorized | **401 Unauthorized** | JWT Bearer token authentication boundary |

---

## 5. Apple Privacy Manifest Audit (PL05)

Location: `src/MuscleCuties.UI/Platforms/iOS/Resources/PrivacyInfo.xcprivacy`

- **`NSPrivacyTracking`:** `false` (No user data used for cross-app tracking).
- **`NSPrivacyTrackingDomains`:** `[]` (Empty list, zero tracking domains).
- **`NSPrivacyAccessedAPITypes`:**
  - `NSPrivacyAccessedAPICategoryUserDefaults`: Reason code `CA92.1` ("Access user defaults to read and write information that is only accessible to the app itself").
- **`NSPrivacyCollectedDataTypes`:**
  - `NSPrivacyCollectedDataTypeEmailAddress` (Account registration & receipts)
  - `NSPrivacyCollectedDataTypeName` (User profile display)
  - `NSPrivacyCollectedDataTypeUserID` (Authenticated session identifier)
  - `NSPrivacyCollectedDataTypePerformanceData` (App launch & frame render metrics)
  - All declared with `NSPrivacyCollectedDataTypeLinked=true` and `NSPrivacyCollectedDataTypeTracking=false`.

---

## 6. Corrective & Remediation Package Summary in Candidate

| Package | Component | Description & Verification Proof |
|---|---|---|
| **P0** | UI / Config | Port 5080 AirPlay receiver collision resolution in `AppEndpointConfiguration.cs`; 58/58 tests passing. |
| **NM01 / NM02** | UI / Food Log | Grouped meal editor with `IngredientPortionDraft`, `GroupedMealEditProjection`, and compact modal lifecycle. |
| **NM03** | Core / UI | `MicronutrientCoverageRule` and `NutritionBreakdownPresenter` with modal presentation for micronutrient coverage. |
| **CR01 / PG01** | UI / ViewModels | `PageLoadExtensions` appearance guard, upfront plan replacement disclosure modal, auto-activation of eligible plan, native injury adaptation alert, and typed `PlanningRequestFailedException`. |
| **WP01 / WP02a** | Core | 10-region muscle coverage rule with $\ge 2$ exposures/week minimum and 3 preferred; time budget cap; Limited status on infeasibility. 5 tests green. |
| **WP03a / WP03b** | UseCases / Store | Observed set actuals (`ObservedSetRecord`, reps, load, RIR, technique) and active double progression connected to next prescription calculation. 3 contract tests green. |
| **WP04** | Core | `ComposedReadinessCycleDoseRule`: single dose authority with Fatigue Safeguard (extra cycle volume withheld when readiness $< 75$ or fatigue reported). 8 tests green. |
| **WP05** | Core | `PainTriggerHandlingRule`: immediate movement stop, candidate substitution, explicit withholding without auto-rehab, clinical escalation for pain $\ge 7$. 4 tests green. |
| **UX01b** | UI / ViewModels | Day vs All agenda view scoping with real date filtering, stable session IDs, and filter chips. |
| **UX02** | UI / XAML | Multi-session schedule rendering: `ScheduleDayItem.Sessions` bound to `HorizontalStackLayout` with `UiDimension6` chips; non-overlapping layout. |
| **UX02a / UX02b / UX03** | Core / UI | `ActivityModalityMetrics` supporting pace/swim/cycle metrics; zero-exercise activity logging; dedicated SVG vector media assets. 8 tests green. |
| **UX03** | UI / ViewModels | Workout overview completion state transitions: `HasPlannedSessions`, `HasCompletedTodaySession`, `HasPendingReviewFlags`, `MarkSessionCompleted`. |
| **RM00 / RM01** | UI | iOS `Button.LayoutButton` null-layer layout crash protection; typed `ApiFailurePresenter` error mapping. 5 tests green. |
| **RM02–RM05** | UI | UI polish: photo privacy overlay radius, archive width hierarchy, food log micronutrient breakdown. |
| **PL02** | Postgres / CI | Ordered PostgreSQL schema migrations (`PostgresSchemaMigrations.cs`) with advisory locking; `003_schedule_seam.sql`; `db-release.yml migrate`. |
| **PL03 / PL05** | Tooling / iOS | `validate-package.sh` resilience; Apple `PrivacyInfo.xcprivacy` verified with `CA92.1`. |
| **QA00** | Suite | Clean migration chain rehearsal and integrated non-UI acceptance. |

---

## 7. QA01 Simulator Walkthrough Evidence (iPhone 16 / iOS 18.2)

Target device: iPhone 16 (`60DD8563-A1D6-4FE5-9F4F-2BFB975B194B`), iOS 18.2.  
Compiled endpoint: Remote AWS API Gateway (`https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`).

| Journey Screen | Rendered Components Verified | Screenshot Artifact |
|---|---|---|
| **Workout Dashboard (Train)** | "This week's plan", "Luteal phase" badge, "Workout Archive" CTA, "FRI rest / Rest day" card, 7-day schedule strip (Mon–Sun chips), Activity schedule summary (3 training / 4 full rest / 6 activities), category tabs (All, Strength, Cardio, Recovery), bottom navigation bar. | `qa01_workout_direct.png` |
| **Nutrition Dashboard** | Calorie target progress card (857 / 2520 kcal), macro breakdown (Protein, Carbs, Fat bars), meal slots (Breakfast, Lunch, Dinner, Snacks), and "Micros" breakdown action button. | `qa01_simulator_launch.png` / `qa01_simulator_screen4.png` |

---

## 8. Owner Product & Release Decisions (Approved November 5, 2026)

All 14 open product, release, and operational questions have been formally closed by the owner:

1. **Replacing Workout Plan:** Upfront confirmation disclosure informs user that regenerating replaces the active plan; eligible replacement plans auto-activate upon generation without requiring a second "Apply" tap. Native alerts handle injury adaptations (`MISSION.md:109` / `PG01`, reconciled in `WORKOUT_POLICY.md`).
2. **Rescheduling:** Formally gated from internal Beta 1.0.0 to prevent schedule/injury lineage corruption. Deferred to Beta 1.1 for move-to-empty-date-only under optimistic concurrency.
3. **Older Builds & Water Logging:** Internal testers must update to the latest build before using the new water journal. Legacy total on disk is preserved.
4. **First-Beta Scope:** Local date measurements with UTC cutoff banner, grouped meals + portions + micros, schedule strip chips (`UX02`), and status transitions (`UX03`) included. Arbitrary-date food copying (`F05`), water reminder push notifications, and abandoned attempts view deferred to Beta 1.1.
5. **Simulator Walkthrough:** Authorized for Antigravity Orchestrator on iPhone 16 simulator with synthetic data.
6. **GitHub Review:** Draft GitHub PR from candidate to `main` accompanied by this repo evidence packet.
7. **Staging AWS Isolation:** Same AWS account (`us-east-2`) with strict logical resource isolation (`staging-musclecuties-*` prefixes, separate schema/roles/buckets); additional spend < $30/month.
8. **Recovery Invariants:** RPO $\le 1\text{ hr}$, RTO $\le 2\text{ hrs}$, 7-day rolling snapshot retention, maintenance window Sundays 03:00–05:00 UTC.
9. **Performance Targets:** Auth/logging p95 $< 400\text{ms}$ warm; workout generation p95 $< 1200\text{ms}$ warm; server error rate $< 0.5\%$; 7–10 day internal Beta dogfooding period.
10. **iOS Code Signing:** Phase 1 local Mac/Xcode signing for immediate internal TestFlight upload; Phase 2 CI/CD automation.
11. **TestFlight Audience:** Internal Testing only for initial build; external testing gated until internal acceptance passes.
12. **Candidate Acceptance:** Candidate code and tests approved for Draft PR review.
13. **Deployment:** Staging verification approved prior to Beta database migration.
14. **Distribution:** Signed archive authorized for internal TestFlight upload.

---

## 9. Next Steps for Promotion
1. Push latest evidence and doc reconciliation to `origin/remediate_musclecuties_ui_ux`.
2. Open Draft GitHub PR (`remediate_musclecuties_ui_ux` $\to$ `main`) for owner review.
3. Fast-forward / sync Rider primary checkout on `main`.
4. Proceed to local Xcode / dotnet release archive signing for internal TestFlight upload.
