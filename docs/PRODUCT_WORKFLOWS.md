# Product workflows

The rules below govern plan acceptance and safety. The [approved Beta workout policy](release-audit/2026-11-05/WORKOUT_POLICY.md) owns Beta coverage, exercise selection, progression and session adjustments. This file owns the user-visible workflow and disclosure rules, so the UI and the policy documents cannot disagree about acceptance.

## Workout planning

- Ask safety questions only when information is new, affected by a change, missing, or due for reassessment.
- Preserve the active accepted plan while preparing a replacement. Do not activate a replacement before acceptance.
- Show a new or changed plan for user review when the established workflow requires it.
- Automatically apply approved conservative adjustments for readiness, sleep, energy, or cycle-related conditions within the existing reviewed rules.
- Apply injury-related restrictions and safe substitutions; request additional information when the existing rules cannot determine a safe result.
- Treat **Apply** as the user's acceptance of the preview it belongs to.

## Plan acceptance (one policy)

- **First plan:** the source's tested exception stands. A generated first plan that is feasible, persisted, leased, requested in the foreground and free of missing required answers is applied automatically; no separate Apply tap is added.
- **Replacement plan:** an existing active plan is never replaced without an explicit user action. The initiating Generate/Regenerate control states the replacement consequence up front, and the resulting plan is activated only through **Apply plan**.
- **No per-exercise acknowledgment gate:** applying the preview is the only decision. Injury-excluded movements are already removed by the reviewed rules and the preview explains the week inline, so nothing waits on a per-exercise acknowledgment step.
- **Reduced volume:** when the week fits only with reduced volume, the preview says so and Apply plan carries that acceptance.
- No other acceptance path exists. A local generator, a cached preview or a failed API call must not substitute for this policy.

## Generation prerequisites and recovery

- Generation needs a saved strength activity and a valid saved weekday set (1–7 distinct days).
- When either is missing, the page must show what is missing **and** an action that opens the editable workout setup. A status message alone is a dead end and is not acceptable.
- Every missing or changed prerequisite is reported together, not one per retry. Structurally invalid input remains a distinct failure and is never presented as a user-recoverable gap.

## Preview completeness

- The preview states the week's composition in one summary: strength sessions, prescribed working sets and timed aerobic minutes.
- Each strength session lists its movements with prescribed sets/reps, and each movement states why it was selected.
- Timed phases show the warm-up, main, cool-down and interval split that is actually prescribed. Absent values stay absent; the preview does not invent a phase.
- Limitation and advisory reasons are disclosed in full. Informational diagnostics are not presented as limitations.

## Training capture and progression

- Strength logging captures per-set rows: reps, load, RIR and technique acceptability. Edited rows are sent as `ObservedSets` with working sets renumbered from one; warm-up and zero-rep rows are excluded.
- Row-less timed sessions (running, walking, cycling, swimming, recovery and similar) send an activity actual with duration and effort so the activity completes without invented sets.
- Progression consumes only persisted observed sets. A session with missing RIR or no working sets is omitted, never filled from aggregate values, so an incomplete week cannot become a qualifying week.
- Progression uses the exercise's real major muscle group and the user's real effective experience tier. Hardcoded defaults are not acceptable inputs to a load decision.
- Recommendation, progression and skipped-summary copy is localized for EN and RU.

## Deliberately deferred surfaces

- **Fasting:** domain, use-case and PostgreSQL storage exist, but there is no client surface. It is deferred, not shipped, and no screen or copy presents it as available.
- **Exercise media:** there is no exercise image or video player. The session screen shows textual technique guidance and only states that a saved video exists; it renders no image or video placeholder.

## Safety invariants

- Missing or stale injury answers remain distinct from "no injury"; absence never relaxes a restriction or increases intensity.
- A session with no permitted strength block is withheld with a review reason rather than replaced by unsafe work.
- Health synchronization stays dormant unless the owner authorizes its scope.
