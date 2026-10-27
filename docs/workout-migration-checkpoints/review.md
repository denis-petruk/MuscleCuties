# Workout migration independent review

Review 2026-10-26; static inspection only. Implementation remains incomplete. Core safety/style work is being independently reviewed by the persistence owner; this report does not self-approve it.

## Actionable findings

1. `PostgresStrengthPlanProposalStoreV020.SaveAnswersAsync` rejects every changed answer for an active plan, and requires no preview before assessing safety. This safely avoids some unsafe writes but prevents required safety overrides and normal existing-plan edits. Implement a single account-locked transaction to version/save answers, invalidate preview, remove newly unsafe future activity while preserving logs, and enqueue a canonical applied-preference regeneration for a restored-safe activity. Do not automatically create a first plan.
2. `PostgresWorkoutProgressStore.CreditPlannedCompletionAsync` is reached from the planned exercise-log path, which requires nonempty planned exercises. `PostgresWorkoutActiveSessionStore.FinishSessionAsync` and non-strength activity completion lack credit integration. Credit every provable completed app workout once per effective local date in its completion transaction, retain provenance on deletion, and keep unprovable historical/manual records uncredited.
3. `PostgresWorkoutProgressStore` sets `PromotionPending` but current source has no reconciler consumer. Durable promotion is recorded, but future prescriptions are not updated. Add reliable server-side work that defers while a valid preview exists and preserves in-progress and logged work.
4. Injury creation/clearance records state and invalidates previews, but reviewed future exercise replacement/restoration, original provenance, overlapping injuries and seven-calendar-day reconciliation are not implemented. Preserve the existing clearance deadline through delayed restoration and never mutate logged prescriptions.
5. `PostgresAcceptStrengthPlanProposalStoreV020` rechecks strength muscle-group sets but does not establish a version or fingerprint for the rest of the reviewed exercise/catalog safety and template data. A catalog change outside muscle contributions can leave a formerly safe preview acceptable. Snapshot and compare authoritative reviewed catalog and policy versions atomically; do not claim completeness from nonempty contribution rows.
6. UI offers explicit strength-style selection after a nonpersisted preflight and sends its choice, renews leases, and preserves the same acceptance request across network exceptions. No user-facing takeover path was found. Add an explicit takeover action with server fence token, stale-view invalidation and retry handling; test app suspension and expired leases.
7. Existing preference edits, readiness/phase endpoints, legacy direct generation and other reachable plan mutation routes still need a complete server-side lock and canonical lifecycle review. Disable or delegate any bypass while preserving historical reads. The broad migration cannot be claimed complete on the basis of the new proposal route alone.

## Recommended next phase

Coordinate one account-scoped safety and automatic-update transaction boundary across saved answers, injury updates and durable promotion jobs. Add reviewed per-exercise complete safety/catalog provenance and reviewed template floors before implementing safe reduced-dose style ranking. Finish all-activity completion provenance and server reconciliation, then wire takeover and first/existing-plan navigation through one lifecycle. Use synthetic concurrency/rollback tests and static architecture/XAML review; execute PostgreSQL checks, tests, and the explicit iOS build only under the existing required authorization. Reviewed safety catalog and template-specific minimum volume are external review material and must not be invented.

## Independent Core review received from other owners

- Single-style preview can lose the reduced-volume acknowledgment flag because the use case sets it only in the multi-style branch.
- When no selected strength style is safe, the current aggregate limits the whole proposal rather than allowing a safe non-strength remainder.
- Style evaluation falls back to actual sets when original planned-set metadata is absent, risking a reduced option being classified as unreduced; require authoritative reviewed baseline and floors.
- Treating every strength limitation as fatal can reject a safe remaining plan when a movement is omitted. Keep safety limitations distinct from explainable omissions.
- Reconcile climbing competence/instruction question conditions with the reviewed scheduling safety policy.
- The test proposal-store fake must implement the newly added `GetActivePreviewAsync` interface method before the test suite can compile.

## Planned-session provenance review

`PostgresWorkoutPlanAndSessionStore.LogAsync` currently credits a linked planned workout when all exercise logs reach completion even if its planned session remains active; the UI logs each exercise before Finish, so later-abandoned work can earn a completion day. Finalize credit atomically only on actual workout completion, once per effective local date. `PostgresWorkoutActiveSessionStore.FinishSessionAsync` credits only activities with no exercises, leaving the strength path dependent on the premature LogAsync credit. It also lacks a completed-session guard for a different finish idempotency key. Planned start persists client-provided `StartedAtUtc` and later uses it to validate elapsed duration, so stale/client-manipulated timestamps can fabricate elapsed time; persist server-accepted start provenance. Review app restart/reconnect duration against that authoritative start and preserve the same pending finish payload after an uncertain response. Owners were notified; these changes were in flight at review time and must be rechecked after integration.

The additive credit migration uses per-tenant RLS and a unique user/local-date index; credits have no deletion-sensitive log FK. Planned provenance columns are nullable, preserving historical records without inventing them. Final integration still requires PostgreSQL race/rollback verification when authorized.

## 2026-10-27 automatic-revision follow-up

The trusted `WorkoutPendingRevisionJobHandler` is registered and delegates to `ReconcileAsync`, but no queue/scheduler invokes it; configured delivery and retry remain deployment work. Reconciliation currently uses an account-scoped transaction and preserves safety pending if a suppressed selected activity remains absent. Automatic acceptance deactivates the previous plan while `LoadOverviewAsync` reads only the new active plan, so an unfinished session on the previous plan disappears from overview even though its session, prescription and logged rows remain. Existing direct-detail lookup can find the prior day unless safety suppression filters it. Merge the in-progress day as a resumable overview item and preserve safety and account boundaries. The promotion boundary skips today whenever a session is active, so it cannot preserve a separate later same-day scheduled workout as the next revision boundary. Owners notified.

## 2026-10-27 promotion boundary and restoration review

The same-day promotion query rejects removed days, active sessions from another plan, completed retired-plan sessions, missing/duplicate orders, and changed activity/order sequences; current/old plan and snapshot rows remain preserved. It gathers all planned active/completed references for the active day's IDs. `completedToday` nevertheless uses only experience-credit records, so manually logged completed activity is ignored for first/replacement start eligibility. Manual completions must affect scheduling under the accepted account timezone without earning experience credits; if unsequenced they must conservatively defer same-day promotion. Persistence owner notified.

Injury restoration is an account/catalog-locked transaction and returns without commit on unknown or changed date/slot/exercise/dose/episode evidence. The current preview treats recently cleared injury sites as restrictive until reviewed cleared-load policy exists, so the requested immediate optional restoration remains blocked rather than silently inventing clinical evidence. Overlap, clearance boundary, rollback and actual PostgreSQL races remain unverified; no tests executed.

## Manual activity and bypass re-review

Acceptance now separately checks unlinked 100%-complete non-rest WorkoutLogs on the account-local date; these affect start eligibility without granting experience, and unsequenced manual logs defer same-day promotion. Linked unfinished exercise logs and rest days are excluded. Direct unplanned WorkoutSessionLog records can also be completed through ActiveSession Finish without a WorkoutLog; current acceptance does not see them. Their historical client-supplied completion timestamps and absent activity classification require explicit trustworthy date/non-rest provenance rather than invented interpretation. Backend owner notified. Direct generation endpoint is retired; phase/readiness Apply paths acquire the account lock and check active previews. This is a targeted reachability review, not proof that every legacy mutation route has been migrated. No tests/builds executed.

## Synthetic acceptance fixture review

`PlanningAcceptanceTransactionTests` asserts one active plan/revision, draft persistence, same-command replay, stale answer-version rejection, and retention of the old in-progress session row using synthetic accounts and a localhost test-database guard. Its initial setup save occurs before the cleanup `try/finally`; a setup failure can leave synthetic rows. The fixture creates no set logs and therefore does not yet prove logged-set preservation. The stale check occurs before writes and is not a rollback-after-partial-write test; add injected post-mutation failure, exact replay/payload conflict and concurrent competing acceptance cases before relying on it as transaction evidence. Its manually constructed no-strength preview tests persistence, not canonical planner/catalog safety. No tests run.

## Unplanned completion provenance re-review

New unplanned activity input rejects invalid/rest/planned combinations. Finish records server time and accepted account-local date/timezone/version; acceptance counts that completed nondeleted provenance for start eligibility without creating an experience credit. Legacy null provenance is left unknown. The synthetic PostgreSQL fixture now wraps setup in cleanup and checks retained set linkage, provenance and no credit; it still needs an actual later proposal acceptance asserting start-date deferral, rest/invalid/canceled/replay and timezone-change cases. Its real system clock may cross UTC midnight between setup and assertions. No tests run.

## Acceptance rollback and legacy Start hash review

The unplanned-classification-null Start hash retains the exact anonymous property names and order from the committed predecessor. The synthetic test records that hash and repeats the same command. The `SavedChangesAsync` interceptor throws after EF SQL and before the acceptance transaction commits, so it is a valid post-write failure point if reached. Existing assertions check profile weekdays, active plan/revision and proposal status; they should also verify lease ID/fence/expiry, pending flags and retained prior session/set after injected failure, and explicitly assert the interceptor runs inside the acceptance transaction. Fixture remains unexecuted; backend owner notified.
