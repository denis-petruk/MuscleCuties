# Roadmap — MuscleCuties

Sequence of remaining work only. Completed work is in Git history; verified state is in `STATUS.md`; user-visible policy is in `docs/PRODUCT_WORKFLOWS.md`; full Workout Engine 2.0 specification and gate matrix live in `docs/workout-engine-v2/IMPLEMENTATION_PLAN.md` and `docs/workout-engine-v2/ORCHESTRATOR_PROMPT.md`.

## Shipped on `main` and `fix/qa-audit-followups`

Provisionable schema (`migrate`/`seed`/`dbcheck`), digest-verified Lambda deployer with rollback, per-set training capture and optional-RIR snapshot progression (`ea07bda6`), one-day Full Body planning (`630c3dd0`), lower-body pattern gap disclosure (`9d3f58e5`), soreness/DOMS exclusion across readiness/recovery/deload (`54dbd353`, `456f7b89`, `484bbb19`), reviewed 59-exercise catalog integrity guard (`50e49da2`), deterministic template resolution (`fa0e308d`), HealthKit multi-metric sync, USDA FoodData Central search (server-side).

## Next — Workout Engine 2.0 Packages (`docs/workout-engine-v2/IMPLEMENTATION_PLAN.md`)

1. **Packages A–K (`Core`, `UseCases`, `Infrastructure.PostgreSql`, `Server`, `UI` & 26-Scenario Verification — COMPLETE):**
   - **Package B / Gate 60 (Owner Review CLOSED):** 8 owner-approved home/bodyweight/band exercises (`232–239`) activated with complete reviewed V2 metadata, explicit `Sliders` and `BandAnchor` equipment capabilities, and honest bodyweight-only upper-body pulling limitation disclosure (`WeeklyCoverageMissing_UpperPull_Equipment` / `WeeklyCoverageMissing_UpperPull_BandAnchorRequired`).
   - **Packages C–J (Gates 49–59, 61, 63–93):** Canonical `WorkoutGenerationEngineV2` deterministic planner, 100% direct / 50% indirect / 0% incidental muscle accounting, glute specialization & plateau handling (`EvaluateStrengthProgression`), 7-step duration optimization ladder, cardio progression (`EvaluateCardioProgression` + `PriorCardioActuals`), 72h pain reassessment & conservative return (`V2PainReturnSource`), cycle/readiness adaptation, V2 deload & reconciliation (`PostgresCanonicalRecoveryEvidence`, `PostgresWorkoutPlanReconciler`), and cleaned/styled MAUI iOS UI across `WorkoutSessionPage`, `ProfileUnitsDisplayPage`, `ProfilePersonalInfoPage`, `StrengthPlanGenerationPage`, `FeaturedWorkoutCard`, and `WorkoutHistoryPage`.
   - **Package K (Gates 62, 94):** Retired legacy V1 generators (`WeeklyWorkoutPlanGeneratorRule`, `ExercisePickerRule`, `WeekPlanRule`, `WeekScheduleRule`, `DashboardPlanner`, `WorkoutActivityClassifier`, `WorkoutExercisePresentationMapper`) guarded by `WorkoutEngineV2ArchitectureTests`, with all 5 host test suites and iOS Simulator Maestro `s26.yaml` passing.
   - **Status detail:** the dated gate matrix in `IMPLEMENTATION_PLAN.md` is a historical checkpoint; its current-status section reconciles REQ-019, REQ-026, REQ-041 and Gate 60 with this roadmap.
2. **Package L (Controlled Dev → Staging → Beta Reset — Gate 95):** Inventory non-production resources, verify backups and isolated restore drills, obtain separate explicit owner authorization per environment, and execute clean resets (`Development → Staging → Beta`) with `Production` strictly excluded.

## Deferred, not half-shipped

- **HIIT (Gate 80):** deferred from Workout Engine 2.0 active selection and generation; historical records preserved.
- **Fasting:** domain, use case and storage exist; no client surface. Never presented as available.
- **Exercise media:** no image or video player; text guidance only.
- **Android:** not targeted.
