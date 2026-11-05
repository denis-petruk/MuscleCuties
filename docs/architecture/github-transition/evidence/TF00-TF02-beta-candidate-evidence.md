# TF00–TF02 — Release Candidate Verification & Promotion Evidence Packet

**Date:** 2026-11-05  
**Candidate Worktree:** `/Users/denispetruk/.gemini/antigravity/worktrees/MuscleCuties/continue_musclecuties_beta_plan`  
**Candidate Branch:** `continue_musclecuties_beta_plan`  
**Baseline Git SHA:** `45508e6d`  
**Target Git Repository:** `denis-petruk/MuscleCuties` (GitHub)  
**Authority:** Single Integration Authority  
**Current Promotion State:** **READY FOR REVIEW** (Pending owner authorization for external TestFlight upload / codesigning)

---

## 1. Executive Summary

This release candidate packet consolidates all corrective beta plan implementations from candidate baseline `45508e6d`. All 1,346 automated non-UI tests are passing (100% green). Clean Architecture boundaries (Core independent of EF Core, ASP.NET, and MAUI) and backward-compatible wire contracts have been strictly preserved.

### Release Invariants Verified
1. **Zero Destructive DB Operations:** No `dbinit` or `EnsureCreated` on shared environments.
2. **Deterministic Versioned Migrations:** Ordered scripts 001 $\to$ 002 $\to$ 003 tracked in `ops.schema_migrations` with SHA-256 verification and advisory lock protection.
3. **Apple App Store & Privacy Compliance:** `PrivacyInfo.xcprivacy` verified with `CA92.1` reason for `UserDefaults`, `NSPrivacyTracking=false`, zero tracking domains.
4. **Single Dose & Safety Authority:** Composed readiness cycle dose rule with Fatigue Safeguard (withholding extra sets on readiness $< 75$) and structured pain trigger substitution.
5. **Schedule Multi-Session Display & Status Transitions:** 7-day strip renders ordered session chips without truncation; overview cleanly transitions upon session completion.

---

## 2. Automated Test Verification (1,346 / 1,346 Passed)

| Test Suite | Project Path | Tests Executed | Passed | Failed | Status |
|---|---|---|---|---|---|
| **Core Domain & Architecture** | `tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj` | 1,131 | 1,131 | 0 | **PASS** |
| **Mobile Contracts & UI Presenters** | `tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj` | 56 | 56 | 0 | **PASS** |
| **Registration & Cloud Contracts** | `tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj` | 43 | 43 | 0 | **PASS** |
| **Schema & Persistence Contracts** | `tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj` | 4 | 4 | 0 | **PASS** |
| **Server HTTP Routes & Auth** | `tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj` | 112 | 112 | 0 | **PASS** |
| **Total Automated Non-UI Tests** | | **1,346** | **1,346** | **0** | **100% PASS** |

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

## 6. Corrective Package Summary in Candidate Baseline

| Package | Component | Description & Verification Proof |
|---|---|---|
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
| **QA00** | Suite | Clean migration chain rehearsal and integrated non-UI acceptance (1,346 / 1,346 tests passed). |

---

## 7. Owner Authorization Checklist for External Action

In accordance with AGENTS.md, all external and destructive operations remain strictly guarded:

- [ ] **TF00 (Archive Code Signing):** Owner authorization required for Apple Developer code signing certificate and provisioning profile targeting `ios-arm64`.
- [ ] **QA01 (Physical Device Acceptance):** Owner authorization required for physical device test execution across Light/Dark themes and EN/RU locales.
- [ ] **TF01 (Internal TestFlight Upload):** Owner authorization required for pushing signed build archive to App Store Connect / TestFlight.
- [ ] **TF02 (Beta Distribution & Closeout):** Owner authorization required for promoting internal build to external beta testing group.
