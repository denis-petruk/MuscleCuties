# Unified day and activity experience

2026-11-04. Proposed scope for A.UI/A.API under the existing coordinator. Impeccable Operate and Design Taste app-screen guidance were applied to planning; no UI was edited or rendered. This proposal preserves [DESIGN.md](../../../DESIGN.md), Soft Minimal, existing Shell destinations, shared modal lifecycle, two button styles and two card styles. It changes no design authority or palette.

The [full implementation plan](IMPLEMENTATION_PLAN.md) owns UX01a/b (projection and Day/All), UX02a/b (activity actuals and logger) and UX03 (media/results/history), plus DS00/WP03 data dependencies. Keep these as compatible vertical slices under one coordinator.

## Intended journey

Train → choose Day or All → date group → ordered session → activity detail → Start/Resume → log actual work → Finish → relevant result → editable history.

Day shows the selected account-local date and **all** its sessions. All shows a chronological agenda grouped by date. Both use the same session rows and status semantics. Selecting Tuesday must actually scope the list, not merely change a featured tile above an unchanged week list. Preserve selected date/scope when returning from detail, logging or editing.

Illustrative Tuesday:

1. Strength — planned/in progress/complete as appropriate.
2. Running — its independent state and action.
3. Recovery — if scheduled.

This is anatomy, not fabricated user history. Cardio-only and strength-only dates are valid. When combined, strength precedes cardio and recovery follows. Do not retroactively invent sequence for historical records lacking DayOrder or prevent recording what a person actually did; preserve actual chronology and separately explain a future scheduling preference.

## Fix the data model seam

The current primary-plus-one-companion projection cannot represent the scheduler's three/four-session days. Agree a backward-compatible ordered sessions collection through server, wire contract, presentation mapper and view state. Every session needs stable ID, local date/time-zone context, explicit modality/capabilities, DayOrder, planned/actual values, status and supported actions. Keep canonical IDs and persisted acceptance semantics.

Order is not a cosmetic sort. Preview, accepted schedule, day view, resume, detail, completed summary and history must agree. Inspect active/future session revisions and stale responses. Existing clients may need additive fields and a compatibility period; the coordinator owns DTO and schema reservations.

## One anatomy, relevant fields

| Surface | Shared structure | Activity-specific content |
| --- | --- | --- |
| Day/session row | Identity/image, purpose, duration, stable status, one next action | Strength label/coverage or running target/other supported summary. |
| Intro/detail | Shared header/back/date, appropriate media, planned work, adjustment reason, Start/Resume | Muscle/exercise prescription; run duration/distance/intervals; recovery duration. |
| Logging | Target, previous comparable actual, current input, validation, save acknowledgement | Fields below. |
| Footer | Dominant Log/Finish, secondary action only when useful; retained draft and retry | Same placement, touch/focus behavior and lifecycle. |
| Result/history | Truthful completion, relevant totals and correction path | No weight-volume PR panel for running; no invented pace for duration-only sessions. |

| Modality | Appropriate observed fields |
| --- | --- |
| Strength | Per-set planned/actual reps and load, completed/skipped state, effort/RIR or explicit Unknown; aggregate summary derived from saved work. Execution report is self-report, not verified technique. |
| Running/walking | Active duration, optional distance, consistent derived pace, effort, supported intervals; optional heart rate only when supplied. Duration-only is valid. |
| Cycling | Duration, optional distance/speed, effort; power/cadence/heart rate only when supported and entered. |
| Swimming | Duration, optional meters and pace per 100 m; verify conversions to existing km/sec-per-km transport fields. |
| Yoga/mobility/recovery | Duration and supported completion/effort/notes; no forced weight/set metrics. |

Use explicit modality capabilities instead of English-name substring matching. Do not manufacture exercise rows merely to access a cardio form. The logging contract must represent canonical activity sessions with zero strength exercises. No new GPS/HealthKit/wearable scope is implied.

## Save and state contract

Keep planned, draft, saving/pending, saved and failed distinct. Optimistic UI must not claim persisted completion. Preserve draft through retry, offline interruption, background/reopen and navigation. Prevent double submits, stale responses from another date/account/session, and duplicate completion after an ambiguous timeout. Start/Resume/Finish and historical correction use the same semantics across modalities.

Display temporary readiness/cycle changes near the affected prescription, with plain reason and original/adjusted values where useful. Logging must preserve the actual session prescription and policy version so progression does not mistake a recovery load for baseline loss. A pain-trigger action must enter the reviewed safety/substitution flow.

## Styling and imagery

Reuse the incumbent page/header/body/footer, gutters, type roles, semantic light/dark colors, Fluent icons, ButtonPrimary/Secondary and CardPrimary/Secondary. Running does not get a separate theme, card geometry or logger. Use compact aligned records and progressive disclosure; no display-font redesign, decorative dashboards or invented performance values. Large text and EN/RU are design inputs.

Inventory existing media before promising new assets. Use accurate, licensed/approved exercise or activity media with a consistent crop, frame and loading behavior. Avoid technical images that depict the wrong equipment, variation or movement. Useful media must not displace inputs or serve as the only technique instruction.

Provide a stable missing/offline/error fallback: appropriate existing Fluent modality icon plus localized activity name, without a broken image or dumbbell for every activity. Decorative images are excluded from accessibility traversal; informative media has a useful localized description. Do not add generated technique illustrations without review or reuse private user photos.

## Verification

- Day/All round trip, no/single/three/four sessions, cardio-only, correct date/ordering, reschedule and cancellation.
- Start/resume/complete/correct each supported modality through its canonical accepted-plan journey.
- Running distance/pace and swimming units, decimal locales, missing optional metrics, partial work and duration-only logs.
- Failed/lost/repeated save, offline draft, background/reopen, account/date/session switch and stale callback suppression.
- Same candidate in EN/RU, both appearances, narrow supported iPhones and largest text; VoiceOver labels/focus, Reduce Motion, safe-area/keyboard, actual scroll/back/dismiss gestures.
- Relevant summary/history and complete persisted round trip; no synthetic metrics or strength-only totals on cardio.

Native acceptance is pending. Browser/CSS inspector output cannot certify a MAUI iPhone screen. The Impeccable context loader reported missing PRODUCT.md while finding the existing DESIGN.md; this scoped proposal uses the incumbent authority and current user brief. Creating new product/design governance files is outside this audit's scope.
