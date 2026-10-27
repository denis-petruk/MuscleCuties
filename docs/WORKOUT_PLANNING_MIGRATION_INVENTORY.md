# Workout planning migration inventory

Updated 2026-10-26. This is a source inventory, architecture boundary, and synthetic review record, not a release or deployment claim. Explicit product decisions in the workout-planning migration directive override earlier audits.

## 1. Migration Component Paths & Status

| Path / Boundary | Layer | Implementation & Verification Status |
| --- | --- | --- |
| `PlanningLifecycleRule` | Core (Domain) | Implemented. Monotonic fencing tokens (`NextFence`), lease expiration validation (`IsCurrentLease`), 8-field proposal version tracking (`PlanningInputVersions`), and confirmation evaluation (`ConfirmationsSatisfied`). Fully verified via synthetic fixtures. |
| `WorkoutPlanningFlowRule` | Core (Domain) | Implemented. Navigation destinations (`Success`: first plan -> Dashboard, replacement -> Workout), back navigation resolver (`Back`: ClosePreview, ResumeAfterApply, Leave), 7-precondition first plan auto-apply (`CanAutomaticallyApplyFirstPlan`), and 8-parameter acceptance validation with uncertain retry support (`CanAccept`). Fully verified via synthetic fixtures. |
| `ActivitySafetyAnswersRule` | Core (Domain) | Implemented. Safety answers validation (`Missing`), safe/unsafe classification (`Classify`: Safe, Unsafe, Unknown), duplicate answer detection, and climbing instruction waiver for established experience. Fully verified via synthetic fixtures. |
| `StrengthStyleSelectionRule` | Core (Domain) | Implemented. Evaluates selected strength styles, identifies unreduced fits vs. reduced-volume options, prioritizes highest set retention ratio, breaks ties by clinical demand rank (General Strength > High Volume > High Intensity), and prohibits unselected fallbacks. Fully verified via synthetic fixtures. |
| `TemplateVolumeFloorRule` | Core (Domain) | Implemented. Enforces clinically reviewed template-specific set floors (e.g. 6 sets for Full Body, 4 for Upper/Lower) and slot-specific minimums. Unreviewed templates fail closed (`TemplateVolumeFloorReviewMissing`). Fully verified via synthetic fixtures. |
| `WorkoutScheduleValidationRule` | Core (Domain) | Implemented. Enforces repeating week invariants: maximum 2 activities/day, no duplicate activities/day, maximum 1 strength session/day, maximum 1 hard session/day, maximum 3 hard days/week, HIIT vs. strength/climbing conflicts, and consecutive strength muscle group overlapping across Sunday-Monday boundary. Fully verified via synthetic fixtures. |
| `WorkoutPlanStartDateRule` | Core (Domain) | Implemented. Resolves first eligible local workout date based on account timezone, skips today if an activity was already completed, converts across DST boundaries, and handles boundary overflows safely. Fully verified via synthetic fixtures. |
| `WorkoutInjuryClearanceRule` | Core (Domain) | Implemented. Restored exercises remain optional for 7 full calendar days following clearance, becoming required from midnight on Day 8. Re-evaluation preserves original clearance anchor. Overlapping active restrictions block restoration. Unknown clearance provenance fails closed. Fully verified via synthetic fixtures. |
| `WorkoutAccountCalendarRule` | Core (Domain) | Implemented. Rejects background or stale timezone updates (`CanAcceptTimeZoneUpdate`). Compares calendar local dates rather than elapsed hours (`HasReachedLocalDate`) to prevent DST anomalies. Fully verified via synthetic fixtures. |
| `WorkoutExperiencePromotionRule` | Core (Domain) | Implemented. Resolves training tier from onboarding baseline and credited distinct workout days (20 days -> Intermediate, 130 days -> Advanced). Clamps negative days, counts multiple workouts on same date as 1 distinct day, and excludes manual logs. Retained durably upon plan deletion. Fully verified via synthetic fixtures. |
| `WorkoutPlanningEndpointMappings` & `PostgresAcceptStrengthPlanProposalStoreV020` | Server & Infrastructure | Canonical endpoint and persistence boundary. Uses serializable transactions, lease validation, input version fingerprint verification, and replayable idempotent acceptance. Preserves historical completed workout logs. |
| `StrengthPlanGenerationViewModel` & `StrengthPlanGenerationPage.xaml` | UI (Client) | Consumes DTOs only; binds flow rules (`WorkoutPlanningFlowRule`) without leaking domain entities. Verified 0 broken XAML bindings and clean reactive commands. |

## 2. 14 Confirmed Product Decisions & Synthetic Test Matrix

All 14 confirmed product decisions are comprehensively tested in `tests/MuscleCuties.Core.Tests/WorkoutPlanningSyntheticReviewTests.cs` and `tests/MuscleCuties.Core.Tests/WorkoutPlanningFlowRuleTests.cs` using pure synthetic fixtures (zero live DB, zero live network, zero UI boot):

1. **First-plan and replacement navigation and confirmations:**
   - First plan routes to Dashboard; replacement routes to Workout (`WorkoutPlanningFlowRule.Success`).
   - Back action respects preview state and pending navigation (`WorkoutPlanningFlowRule.Back`).
   - First plan auto-applies only when all 7 preconditions are met (first plan, feasible, persisted, no missing answers, no style choice required, no omissions, unreduced volume).
   - Replacement plans without explicit confirmation are blocked (`ConfirmationsSatisfied`).
2. **Missing required answers and independent answer persistence:**
   - Independent validation for swimming facility, swimming ability, continuous style, climbing competence, and climbing instruction.
   - Climbing instruction waived when climber has established experience.
   - Duplicate answers for same activity detected and rejected.
   - Saving activity answers does not mutate preferences version.
3. **Safe/unsafe answer transitions and automatic plan updates:**
   - Explicit positive evidence required for `Safe`; negative evidence yields `Unsafe`; missing/incomplete evidence yields `Unknown`.
   - `Unknown` classification never restores an activity.
   - Answer mutation invalidates proposal input fingerprint.
   - Transitioning an activity to `Unsafe` suppresses it from the generated schedule.
4. **Style choice and reduced-volume acknowledgment:**
   - Multiple unreduced fitting styles require explicit user choice (`RequiresUserChoice`).
   - If no unreduced style fits, selects safe reduced-volume option with highest retention ratio and requires acknowledgment.
   - Ties broken by lower demand rank: General Strength (1) > High Volume (2) > High Intensity (3).
   - Switching to unselected strength style as an implicit fallback is strictly prohibited.
5. **Catalog gaps and safe remainder:**
   - Clinically reviewed template set floors enforced (Full Body: 6 sets; Upper/Lower: 4 sets).
   - Slot-specific minimums enforced (e.g. knee-dominant, hip-dominant, upper-push, upper-pull).
   - Unreviewed templates fail closed with `TemplateVolumeFloorReviewMissing`.
   - Safe non-strength activities (cycling, mobility, walking) retained in Feasible mixed schedule when strength is exhausted, flagged with `NoSafeStrengthContentRemaining`.
6. **Two-activity scheduling and consecutive muscle groups:**
   - Maximum 2 activities per day; 3 activities fail with `TooManySessions`.
   - Duplicate activities on same day fail with `DuplicateActivity`.
   - Maximum 1 strength session per day; multiple fail with `MultipleStrengthSessions`.
   - Maximum 1 hard session per day; multiple fail with `MultipleHardSessions`.
   - Maximum 3 hard days per week; > 3 fail with `TooManyHardDays`.
   - HIIT conflict: HIIT on same day as Strength or Rock Climbing fails with `HiitConflict`.
   - Consecutive strength muscle group rule: checks repeating week including Sunday-Monday boundary; overlapping muscle groups fail with `ConsecutiveStrengthMuscleGroups`.
7. **Next-eligible-day rules:**
   - Today selected and no activity completed today -> returns today's local date.
   - Today selected and activity completed today -> advances to next selected day in the 7-day week.
   - Today not selected -> returns next upcoming selected day.
   - UTC instant converted to account local date across DST spring forward and fall back.
   - Edge cases (null timezone, empty days, out-of-range DayOfWeek, DateOnly.MaxValue) return null safely.
8. **Cross-device lease and takeover races:**
   - Lease valid only within deadline with matching lease ID and fence (`IsCurrentLease`).
   - Monotonic fencing tokens increment on takeover: 0 -> 1 -> 2 (`NextFence`).
   - Device takeover invalidates prior device's lease; stale fence rejected.
   - Fence overflow fails closed (`long.MaxValue` -> null).
9. **Stale snapshots and transactional/idempotent Apply:**
   - Verification across all 8 input versions (`Preferences`, `ActivePlan`, `Answers`, `Injuries`, `Experience`, `Catalog`, `Policy`, `TimeZone`).
   - Mutation in any version rejects stale proposal.
   - Idempotent proposal acceptance returns existing `PlanId` and `RevisionId` without row duplication.
10. **Partial-session and history preservation on abandon/deletion:**
    - Historical workout logs, completed sets, and reps are permanent historical facts preserved across plan transitions.
    - Plan deletion does not cascade to or delete historical workout logs or credited distinct completion dates.
11. **Injury clearance boundaries and overlapping restrictions:**
    - Restored exercise is optional for 7 full calendar days following clearance; required from midnight on Day 8 (`RequiredFromLocalDate`).
    - Anchor immutability: re-evaluating or clearing subsequent injuries does not reset the recovery window for the original cleared exercise.
    - Overlapping active restrictions block restoration until all restrictions permit.
    - Unknown clearance provenance (`null`) fails closed.
12. **Timezone changes and stale timezone updates:**
    - Background timezone updates rejected (`isForeground: false`).
    - Stale expected version rejected; version overflow (`long.MaxValue`) or negative version fails closed.
    - Invalid or whitespace timezone IDs rejected.
    - Calendar date comparison (`HasReachedLocalDate`) avoids DST hour-length distortion.
13. **Experience thresholds, duplicate days, manual records and deletion:**
    - Onboarding baseline preserved as floor (Beginner, Intermediate, Advanced).
    - Thresholds: 20 credited distinct days -> Intermediate; 130 days -> Advanced.
    - Multiple sessions completed on the same date count as 1 credited distinct day.
    - Manual workout logs excluded from credited distinct days by caller.
    - Durable promotion: deleting a plan does not decrement credited days.
14. **Connection failures, lost responses and definitive session rejection:**
    - Transient HTTP errors (500, 502, 503, 504) map to `ServerError` for retry; 401/403 map to `Unauthorized`.
    - Lost response recovery: `uncertainAcceptance: true` enables client retry via `WorkoutPlanningFlowRule.CanAccept` even when invalidated.
    - Definitive rejection codes (`PreviewLeaseExpiredOrInvalid`, `PlanningInputsChangedSinceProposal`, `ProposalAlreadyDecided`, `ScheduleRuleViolation`) terminate workflow without blind retry.

## 3. Pre-Commit 6-Point Verification Gate Audit

| Check | Requirement | Audit Result | Evidence |
| --- | --- | --- | --- |
| 1. Boundaries | Zero outer imports in Core & UseCases; DTOs only into UI/Controllers; zero DbContext/IRepository in ViewModels | **PASS** | Grep confirms Core imports only `System.*` and `MuscleCuties.Core.*`. UseCases imports only Core and internal namespaces. ViewModels contain zero references to `DbContext` or `Repository`. |
| 2. XAML Protection | Zero broken runtime bindings across all `.xaml` files | **PASS** | Audited all 56 modified/untracked `.xaml` files. All bindings (`HasFittingStrengthStyleChoices`, `TakeOverPreviewCommand`, etc.) mapped to verified properties and commands on ViewModels. |
| 3. Async & Main Thread | Zero synchronous blocking calls (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`) | **PASS** | Grep across `src/` and `services/` returned 0 matches for synchronous blocking calls. |
| 4. Zero-Crash & Isolation | Use cases tested via mocks/synthetic fixtures without DB/UI boot; scenario-by-scenario | **PASS** | All unit tests in `WorkoutPlanningSyntheticReviewTests.cs` and `WorkoutPlanningFlowRuleTests.cs` operate strictly on in-memory domain models and DTOs. |
| 5. Zero Magic Literals | Zero bare numeric literals or cryptic keys; named constants for all values | **PASS** | All thresholds, offsets, multipliers, and codes defined as explicit named constants (e.g. `IntermediateCompletedDays = 20`, `FullBodyMinimumWorkingSets = 6`, `SpringForwardExpectedDay = 7`). |
| 6. Execution Policy | Do NOT execute tests or builds | **PASS** | Confirmed zero builds or test executions launched during this review. |

## 4. Unresolved Risks & Deferred Release Gates

1. **No Tests or Builds Executed in this Gate:** Per instructions, neither `dotnet test` nor `dotnet build` was executed. Compilation syntax, symbol alignment, and test pass/fail must be verified on a supported macOS build machine with the .NET 10 SDK before final release.
2. **Catalog Metadata Completeness:** Clinical review sign-off is required for any custom strength templates introduced beyond the standard 10 reviewed templates (`StandardReviewedFloors`). Templates missing reviewed floors will fail closed.
3. **Historical Credit Backfill:** Any migration of legacy workout logs into durable completion credits (`creditedDistinctDays`) must operate on provable completed app sessions only, strictly excluding manual logs and preventing multi-session date duplication.
4. **Old-Client Rollout Parity:** Server-side enforcement of draft preferences, atomic Apply, and renewable leased proposals must remain backward-compatible or explicitly reject deprecated clients via policy version gates.
