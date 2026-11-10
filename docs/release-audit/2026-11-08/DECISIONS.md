# Closed-beta owner decisions — 2026-11-08

Decision record for the [closed-beta audit](CLOSED_BETA_AUDIT_AND_PLAN.md). Every row is **PENDING** until the owner
fills in the decision, date and any condition. A contributor may propose but never records an owner decision; a PR, plan
or green CI run is not a decision.

| ID | Question | Options | Recommendation | Blocks | Decision | Date |
| --- | --- | --- | --- | --- | --- | --- |
| OD-1 | Is the live `musclecuties-staging-*` stack (API `y2ejsgrc6l`, RDS `musclecuties-staging-db`) the Beta environment? (A2) | (a) Designate it Beta and rename its configuration; (b) provision a separate Beta stack | (a) — one environment, honestly named, until real need for Staging | Phase 1 | PENDING | |
| OD-2 | "Bodyweight only" equipment produces no squat, lunge or pull. (D1) | (a) Add reviewed bodyweight/band movements through catalog review; (b) remove the option for Beta and disclose the dumbbell vertical-pull gap | (b) for the first testers, then (a) | Phase 3.1, onboarding copy | PENDING | |
| OD-3 | The approved policy promises native alerts for injury adaptations; branch `product/beta-p0a-planning-flow` removes the alert and the per-exercise gate. (D3) | (a) Amend the policy: inline preview disclosure replaces the alert; (b) restore an informational alert without gating | (a), with independent review of the PR | Merge of `product/beta-p0a-planning-flow` | PENDING | |
| OD-4 | HealthKit in the Beta binary? (B3) | (a) Remove entitlement and register the disabled provider; (b) keep and ship a scoped, disclosed feature | (b) | Phase 2.2, Beta App Review | (b) Adopted: multi-metric sync (steps, sleep, active kcal, HRV/RHR), contextual prompt, calorie adjustment, and check-in prefill | 2026-11-09 |
| OD-5 | Export compliance: does the app use only standard OS/HTTPS encryption? (B4) | Yes → `ITSAppUsesNonExemptEncryption=false`; No → documentation required | Owner confirms facts | Phase 2.3 | Yes (`ITSAppUsesNonExemptEncryption=false` confirmed) | 2026-11-09 |
| OD-6 | Load decrease after one or two under-target sessions? (D5) | (a) One (current code); (b) two consecutive | (b) | Phase 3.5 | (b) | 2026-11-08 |
| OD-7 | Beginner selecting High-volume or High-intensity strength. (C1) | (a) Disable with a localized reason; (b) auto-downgrade to General strength with disclosure | (a) | Phase 2.5 | (a) | 2026-11-08 |
| OD-8 | Approval reference for dose changes. (D2) | Record `MC-BETA-WORKOUT-1` (decision of 2026-11-05) as the active approval | Record it | Phase 3.2 | Recorded | 2026-11-05 |
| S-03 | Readiness dose autoregulation: load step-down vs set-only volume reduction | (a) Set-only volume reduction (Option A); (b) Add per-exercise load datum to schema | (a) | Workout dose | (a) Adopted: set-only volume reduction; trainees autoregulate load via RIR. No cycle dose override. | 2026-11-09 |
| S-05 | Injury post-clearance window: immediate restoration vs 7-day probation | (a) Immediate restoration on clearance day (0-day window); (b) 7-day optional window | (a) | Workout safety | (a) Adopted: immediate restoration upon clearance (0-day window) | 2026-11-09 |

## Phase 0 branches awaiting review

All six branches start at `main` `4ee9b4f2`, are local only and carry the exact content of the 2026-11-08 working tree
(byte-compared per file). They are independent: any merge order works, except that the docs record should merge last.

| Branch | Lane | Content | Review focus |
| --- | --- | --- | --- |
| `product/beta-p0a-planning-flow` | A | Removal of the per-exercise acknowledgment gate and native injury alert; default General strength; preview rationale only when real; Limited previews no longer applicable; workout preferences saved to the server (`PUT /api/v1/profile/workout-preferences` restored) | R2 safety review (OD-3); route un-retirement; latency fixture regenerated |
| `platform/beta-p0b-catalog-review-seed` | B | `seed` also applies the embedded reviewed catalog so fresh databases can plan | Seed idempotency on an existing database |
| `product/beta-p0c-auth-local-dev` | A | Development-only log delivery of email codes; OTP errors localized | Delivery registered only in Development |
| `product/beta-p0d-session-set-capture` | A | Per-set rows (load, reps, RIR, technique, add/remove) in the session page; EN/RU copy | Device acceptance of set capture |
| `platform/beta-p0e-beta-only-endpoint` | B | Staging compile switch, endpoint and workflow option removed; contract test | Mobile release workflow |
| `integration/beta-p0f-record` | I | STATUS/ROADMAP/CONTROL/verification record, closed-beta audit and this decision record | Owner reading |
