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
