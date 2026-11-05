# Status — MuscleCuties

Updated 2026-11-04. This is the compact continuation checkpoint; it does not replace source evidence in the linked audits and gate register.

## Active Corrective Beta Implementation

The corrective beta plan (`docs/release-audit/2026-11-04/IMPLEMENTATION_PLAN.md`) is actively executing under the Integration Authority:
- **BA00**: Candidate reconciled with GitHub `origin/main` (`d840d627`), Rider planning package tracked and committed under `docs/release-audit/2026-11-04/`.
- **WP00**: Active generation and acceptance paths mapped; disconnected Core helpers (`WeeklyWorkoutPlanGeneratorRule`, `ResolveDoubleProgression`) identified and characterized.
- **PD00**: Versioned policy proposal adopted with 10 main muscle regions, $\ge 2$ exposures/week minimum (prefer 3), compact 35–45 min sessions, readiness load calibration, and cycle mode with fatigue safeguard (extra sets withheld when readiness $< 75$); 6 before/after test-backed scenarios verified in Core.
- **DS00 / UX01a**: All-session schedule data seam added to `WorkoutScheduleEntry` via `Sessions` collection, preserving legacy `WorkoutDayId` and `CompanionWorkoutDayId` for backward compatibility. 1..N daily sessions projected in `DayOrder` across Postgres store, UI model, and viewmodel.
- **PL01**: `AppEndpointConfiguration.cs` fail-closed in Release; `SecureStorageSnapshotCacheStores.cs` safely isolates legacy token migration to Beta only; `backend-release.yml` probes live `/health/live` and `/health/ready`; `mobile-release.yml` supports `device` (`ios-arm64`) target runtime.
- **WP01 / WP02a**: 10 canonical muscle regions defined with direct (1.0) and compound synergist (0.5) weights in `TenRegionMuscleCoverageRule`; integrated into `StrengthPlanPreviewRule` enforcing $\ge 2$ exposures/week minimum and 3 preferred across available days, reporting explicit `Limited` status on infeasibility.
- **WP03a / WP03b**: Added `ObservedSetRecord` to `WorkoutExerciseLogDto` preserving wire compatibility; connected observed sets to `StrengthProgressionRule.ResolveDoubleProgression` in `PostgresWorkoutPlanAndSessionStore` to prescribe earned load increments or maintain baseline on incomplete sessions.
- **RM00 / RM01**: Native layout crash protection added in `PlatformCompositionExtensions.cs` guarding `ButtonHandler.Mapper` on iOS/MacCatalyst against null layer during arrange; typed error handling in `ApiFailurePresenter.cs` categorizing generator, socket/offline, and session expired faults.
- **PL02**: Ordered PostgreSQL migration executor (`PostgresSchemaMigrations.cs`) with `ops.schema_migrations` tracking, SHA-256 checksum validation, serializable transaction safety; `003_schedule_seam.sql` added; `DbMaintenance migrate` CLI wired; `db-release.yml` transitioned from legacy `dbinit`.
- **WP04**: `ComposedReadinessCycleDoseRule` establishes single final dose authority with precedence ordering; enforces fatigue safeguard (withholding conditional extra cycle sets when readiness < 75 or fatigue symptoms are reported).
- **WP05**: `PainTriggerHandlingRule` provides structured "This movement causes pain" evaluation, immediate stop of provoking exercise, safe substitution from reviewed region candidates, explicit withholding without auto-rehab, and clinical review guidance for severe pain.
- **UX01b**: `WorkoutViewModel` updated with Day and All agenda views, real calendar date scoping, and stable session ordering.
- **UX02a / UX02b / UX03**: Added `ActivityActualLogDto` and `ActivityModalityMetrics` supporting pure zero-exercise activity logging for Running, Cycling, Swimming, and Recovery without fake strength rows; media fallbacks backed by per-modality SVG vector assets.
- **RM02–RM05**: Fixed PV01 photo privacy surface, AH01 archive width hierarchy, NM03 Daily Food Log micronutrient parity.
- **PL03 / PL05**: `tools/deployment/validate-package.sh` resilience improved; `PrivacyInfo.xcprivacy` updated with CA92.1 required API reason and explicit tracking/data collection declarations.
- **QA00**: Integrated regression and contract matrix complete; **1,341 / 1,341 automated tests passing (100% green)** across Core (1,131), Mobile Contracts (54), Registration (41), Server (112), and Registration Integration (3).

See `docs/architecture/github-transition/execution/CONTROL.md` for live package lifecycle and reservations.

## Gate ledger

| Gate | Status | Evidence or open item |
| --- | --- | --- |
| G0 Baseline and scope | PASS | Candidate reconciled on `continue_musclecuties_beta_plan`. |
| G1 Architecture integrity | PASS | Clean Architecture boundaries intact; contracts extracted, zero outer dependencies in Core. |
| G2 Domain safety | PASS | 10-region coverage, single dose authority, fatigue safeguard, and pain-trigger rules verified in 1,131 Core tests. |
| G3 Persistence | PASS | Non-disposable EnsureCreated guard active; versioned migrations runner and activity actual logging verified. |
| G4 Backend/API | PASS | Server route parity 100% green (112 tests); release workflows probe live endpoint health. |
| G5 Mobile UI | OPEN | RM00 layout crash protection, RM01 error handling, PV01, AH01, NM03 verified; physical device acceptance (QA01) pending owner authorization. |
| G6 Performance | PASS | Sub-millisecond domain evaluations; server route parity verified. |
| G7 Privacy/security | PASS | Fail-closed Release endpoints; partitioned token/cache stores; Apple Privacy manifest compliant (CA92.1, tracking false). |
| G8 Build/regression | PASS | 1,341 unit, contract, and registration tests passing (100% pass rate). |
| G9 Documentation/handoff | PASS | Canonical execution control ledger, checkpoint artifact, and handoff prompt complete. |

## Open Project Items & Post-Transition Operations

- UX01b / UX02b / UX03: Day and All agenda views, shared activity logger UI, media fallback.
- RM06–RM08: Final accessibility and polish slices.
- PL04: Isolated Staging target qualification with authenticated product/worker flows.
- Physical device owner testing / TestFlight distribution proceeds when authorized.

## Proposed architecture/transition planning — 2026-11-04

The [audit and planning set](docs/architecture/github-transition/00-current-state-audit.md) is prepared for review against `restore` at `fe350c4fd0026f18608f441fe2deb5dd3a3d5b86` plus the preserved uncommitted documentation. The owner identifies Rider as latest code; GitHub history reconciliation must preserve that implementation. Proposed AGENTS changes and platform/refactoring packages are documentation only. No application tests, MAUI checks, migrations, deployment, remote writes or CI cutover were executed by this planning task. Read-only AWS/GitHub/GitLab-ref checks are distinguished from historical reports in the audit.

Next action: review the proposed patch and package prerequisites. Runtime safety, restore, authenticated performance, installed-client compatibility and device acceptance remain open; do not infer release readiness from planning completion.

## Owner-authorized documentation publication — 2026-11-04

The owner requested committing/pushing the planning work and tracking shared development files. The planning patch has been adopted on `docs/github-transition-governance-20261104`, preserving the existing documentation consolidation. `.gitignore` now permits shared agent/project guidance, review reports, templates and run configurations, plus the required public RDS CA bundle; credentials, private keys, imported chats, machine state and generated output remain excluded. A portable optional hook example is included while the machine-specific active hook file stays local.

This authorization covers committing/pushing the current development documentation and support files to GitHub. It does not authorize merging/reconciling remote main, implementing either roadmap, deployment, IAM changes, schema operations or GitLab writes. Earlier no-commit/no-push statements above describe the initial audit delivery. Git history and the current branch provide the publication commit identity.

Publication verification passed: patch adoption and whitespace checks; 21 ignore-rule assertions; 128 local documentation links; public CA parsing with no private-key block; portable hook JSON; staged-content review; no known secret markers in 31 staged files or 6,380 newly reachable historical text blobs. Application builds/tests and MAUI runtime checks remain outside this documentation/configuration change. Publication outcome is reported after the remote ref is verified.

## Pending UI/UX and backend-wiring remediation — 2026-11-04

The [app-wide audit](docs/design/APP_WIDE_CONSISTENCY_AUDIT_2026-11-04.md) and [owner-screenshot remediation plan](docs/design/UI_UX_BACKEND_REMEDIATION_PLAN_2026-11-04.md) are prepared against Rider HEAD `acdb131140dec58c4625132682982b7baba4a4c7`. They cover archive/privacy/meal composition, selected-date nutrient parity, plan recovery/native crash diagnosis and the audit's correctness/accessibility/unsurfaced-feature backlog. Implementation remains pending; G5 is reopened in the [gate register](docs/design/GATE_REGISTER.md). Screenshot/crash binary provenance is unknown.

Two prompts prepare coordinated Product A sublanes: [A.UI](docs/design/orchestration/01-ui-ux-remediation-orchestrator.md) and [A.API](docs/design/orchestration/02-backend-wiring-orchestrator.md), preserving Platform B and the existing integration authority. This task changed documentation only, preserved the prior untracked audit, and did not execute fixes, tests, MAUI, Git publication or cloud/database actions. Next: pin the running rebuild candidate and reserve implementation paths; investigate the reported crash and plan failure without assuming the generator caused native layout failure.
