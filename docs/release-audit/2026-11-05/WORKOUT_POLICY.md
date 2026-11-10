# Approved Beta workout policy

**Policy version** `MC-BETA-WORKOUT-1`

**Product decision** Denis Petruk approved all six workout decisions on November 5, 2026, and removed medical review from Beta acceptance. This closes PD00 product adoption. Implementation and candidate acceptance remain open.

Beta acceptance requires the recorded product decision, independent engineering review, and candidate-bound behavior and safety evidence. Preserve the [approved workout workflow](../../PRODUCT_WORKFLOWS.md#workout-planning) and [safety rules](../../SAFETY.md#health-and-workout-safety). This policy supersedes the November 4 proposal for Beta. The [research and limitations](../2026-11-04/WORKOUT_POLICY_PROPOSAL.md#evidence-basis-and-limits) remain supporting context.

## Adopted decisions

1. **Muscle coverage.** Plan direct work for each main muscle group in at least two strength sessions per week where feasible, preferably three. Keep the existing catalog's ten region groups for this Beta. Count each working set as one for the primary muscle and one third for each catalogued, actively assisting muscle when tracking volume. Express frequency in the planned sessions and omit the separate credited-day calculation. If equipment, available time or safety prevents coverage, explain the gap and keep the eligible work. Strength precedes cardio on combined days and cardio-only days remain valid.
2. **Exercise selection.** Filter by pain, restrictions, available equipment and feasible technique. Use relevant longitudinal muscle-growth evidence for eligible exercise variations. When evidence is comparable, favor a familiar variation that covers the target and allows consistent progression. Keep performance history separate across noncomparable variations or equipment.
3. **Short sessions.** Remove optional sets and secondary exercises first. Keep warm-up, rest and primary work. Use roughly two to three reps in reserve for working sets within the exercise's reviewed range. The user's time choice governs session planning.
4. **Readiness.** Use one adjustment in the ordinary workout flow, informed by the current check-in and recovery, symptoms, warm-up performance, recent training and cycle context. With poor sleep, fatigue or a clearly worse warm-up, begin with one available load step down for that session. With marked fatigue, remove an optional set first where one exists. Preserve the baseline load. Avoid applying a second load decrement for the same session when signals overlap.
5. **Cycle context.** A calendar estimate alone leaves the plan as scheduled when the current check-in and warm-up are normal. When current state or a recurring individual response supports a cycle-related adjustment, start with one load step down and up to two additional reps within the reviewed range and effort target. Add one working set to the selected exercise only with a fresh, positive recovery signal. Recovery determines extra-set eligibility. Missing, stale or poor recovery keeps extra sets off, and marked fatigue can still trigger the optional-set reduction in decision 4. Apply the adjustment automatically in the regular flow.
6. **Pain.** Stop the provoking movement when pain is reported. Re-evaluate permitted alternatives against the trigger, equipment and existing restrictions. Use a reviewed substitute where eligible. Otherwise withhold the exercise and explain why. Withhold the strength prescription when no eligible strength block remains. Preserve unknown and stale safety answers and existing restrictions. Use a plain stop/withhold explanation in Beta and leave new clinical scoring or escalation thresholds out of its scope.

**Progression and Apply.** Automatically choose the smallest available load increase after two comparable, fully logged workouts with every required working set at the top of the prescribed rep range and at the intended effort. Skipped sessions and incomplete logs preserve prior valid progress and the current baseline. Use corrected actuals for future recommendations and preserve historical session records and accepted plans. Routine adjustments work automatically within established safety rules. Keep the first-plan automatic acceptance exception. For plan replacements, an upfront confirmation disclosure informs the user that regenerating will replace the active plan; upon user confirmation, eligible replacement plans are automatically activated (eliminating redundant secondary Apply taps), with native alerts provided for any injury adaptations (per MISSION.md:109 / PG01, approved 2026-11-05).

## Ratified amendments — 2026-11-09 (owner ratified)

Ratified at the evidence-backed recommendations in [WORKOUT_POLICY_RESEARCH.md](../2026-11-09/WORKOUT_POLICY_RESEARCH.md) by the owner on 2026-11-09:

- **A-4 (decision 4, S-03) — RATIFIED (Option A).** For Beta, readiness reduces volume only: with marked fatigue, remove one optional set where one exists, once per session. Trainees autoregulate absolute load naturally through their target RIR. The "one available load step down" is withdrawn/deferred; planned prescriptions carry no load datum to step down from. Matches production code.
- **A-5 (decision 5, S-03) — RATIFIED (Option A).** A cycle estimate never changes reps, sets, or load by itself. Cycle context informs symptoms and readiness only, which then act through decision 4. The extra-set and +2-rep adjustment is withdrawn (current evidence shows no consistent phase effect on acute strength performance).
- **A-6 (decision 6, S-04) — DEFERRED.** In-session pain reporting and substitution is deferred for initial TestFlight and planned for the subsequent update.
- **A-7 (S-05) — RATIFIED (Option A).** No fixed 7-day post-clearance window. Immediate restoration on clearance day (0-day window): a cleared exercise returns as prescribed from its clearance date when current eligibility permits. Unknown or future clearance keeps the restriction.


## Implementation acceptance

- Trace the actual preview → Apply → scheduled session → adjusted dose → saved actuals → next prescription route. Use one final dose authority and record the policy version, input provenance and reason. Preserve existing clients, baseline prescriptions, and idempotency on retry and resume.
- Replace the active `MC-PLANNING-REVIEW-TEST` approval reference with authentic Beta product provenance when wiring the adopted policy. Keep fixtures confined to tests. Update Beta app copy about pending medical review in EN/RU to describe workout estimates and limits. Preserve the pain and injury controls and their user-facing explanations.
- Verify these cases through the production entry points and disposable PostgreSQL where persistence is involved. Attach results to the exact candidate.

| Scenario | Required result |
| --- | --- |
| Two qualifying workouts, then an incomplete or corrected log | Eligible progression uses the smallest available step. Incomplete work creates no new qualifying result. Corrections affect only future recommendations. |
| Short session with a coverage conflict | Optional work is removed first. The resulting plan reports any remaining coverage gap. |
| Poor sleep with a normal warm-up | One automatic session load reduction with baseline preserved. |
| Estimated phase, normal check-in and warm-up | Planned session remains as scheduled. |
| Cycle adjustment with good recovery, then with unknown or poor recovery | Eligible current response adjusts reps and load once. The extra set is available only with a fresh positive recovery signal. |
| Pain and no eligible alternative | The provoking exercise stops and the reason is shown. The strength prescription is withheld if no permitted block remains. |
