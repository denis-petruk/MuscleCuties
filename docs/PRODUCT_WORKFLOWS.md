# Product workflows

User-visible behaviour that is not already in `AGENTS.md`. The safety rules and the single plan-acceptance path live there (§3) and are not repeated here.

## Workout planning

- Ask safety questions only when information is new, affected by a change, missing or due for reassessment.
- Keep the active accepted plan while a replacement is prepared.
- Apply approved conservative adjustments for readiness, sleep, energy or cycle-related conditions automatically, within the reviewed rules.
- Applying the preview is the only decision: there is no per-exercise acknowledgment step. Injury-excluded movements are already removed and the preview explains the week inline.
- When the week fits only with reduced volume, the preview says so and **Apply plan** carries that acceptance.

## Generation prerequisites

- Generation needs a saved strength activity and a valid saved weekday set (1–7 distinct days), plus every other field that influences the workout.
- Structurally invalid input is a distinct failure and is never presented as a gap the user can fix.

## Preview completeness

- One summary states the week's composition: strength sessions, prescribed working sets and timed aerobic minutes.
- Each strength session lists its movements with prescribed sets/reps, and each movement states why it was selected.
- Timed phases show the warm-up, main, cool-down and interval split actually prescribed. Absent values stay absent.
- Limitation and advisory reasons are disclosed in full. Informational diagnostics are not presented as limitations.

## Training capture and progression

- Strength logging captures per-set rows: reps, load, RIR and technique acceptability. Edited rows are sent as `ObservedSets` with working sets renumbered from one; warm-up, zero-rep and unedited placeholder rows are excluded.
- Row-less timed sessions (running, walking, cycling, swimming, recovery) send an activity actual with duration and effort, without invented sets.
- Progression consumes only persisted observed sets. A session with missing RIR or no working sets is omitted, never filled from aggregates.
- Progression uses the exercise's real major muscle group and the user's real effective experience tier, never hardcoded defaults.
- Recommendation, progression and skipped-summary copy is localized for EN and RU.

## Owner-ratified dosing decisions (2026-11-09/10)

- **Poor readiness or marked fatigue:** reduce one optional set where one exists. Load is not stepped down; the trainee autoregulates through target RIR.
- **Menstrual cycle:** a cycle estimate alone never changes reps, sets or load. Symptoms and readiness drive adjustments; optional sets stay off unless recovery evidence is positive.
- **Injury clearance:** cleared exercises return to normal prescribed work on the clearance day whenever current eligibility permits.

## Apple Health

- Scope: steps, sleep stages, active kcal, resting HR, HRV. The permission prompt appears in context, at check-in or profile sync.
- Daily calorie target adjusts from the rolling 7-day step trend through `HealthEnergyAdjustment.CalculateDailyCalories`, clamped to -180…+260 kcal.
- Daily Check-In pre-fills `SleepHours` from HealthKit and offers recovery suggestion chips for energy and sleep quality.

## Food search

- USDA FoodData Central is called only by the server (`/api/v1/nutrition/catalog/search`); the client holds no key and makes no direct call.

## Deferred surfaces

- **Fasting:** storage exists, no client surface; no screen or copy presents it as available.
- **Exercise media:** the session screen shows text guidance only and renders no image or video placeholder.
