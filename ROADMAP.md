# Roadmap — MuscleCuties

Sequence of remaining work only. Completed work is in Git history; verified state is in `STATUS.md`; user-visible policy is in `docs/PRODUCT_WORKFLOWS.md`.

## Shipped on `main` and `fix/qa-audit-followups`

Provisionable schema (`migrate`/`seed`/`dbcheck`), digest-verified Lambda deployer with rollback, per-set training capture and optional-RIR snapshot progression (`ea07bda6`), one-day Full Body planning (`630c3dd0`), lower-body pattern gap disclosure (`9d3f58e5`), soreness/DOMS exclusion across readiness/recovery/deload (`54dbd353`, `456f7b89`, `484bbb19`), reviewed 59-exercise catalog integrity guard (`50e49da2`), deterministic template resolution (`fa0e308d`), HealthKit multi-metric sync, USDA FoodData Central search + open-source meal generation (server-side), Workout Engine 2.0 (Packages A–K complete, Gate 60 closed, legacy V1 planners retired), and full onboarding enforcement for Sign in with Apple accounts.

## Remaining Work

1. **Controlled Environment Reset (Gate 95):** Inventory non-production resources, verify backups and isolated restore drills, obtain separate explicit owner authorization per environment, and execute clean resets (`Development → Staging → Beta`) with `Production` strictly excluded.
2. **iOS Simulator Verification Passes:**
   - Russian (`ru_RU`) full-gym E2E (`QuizAnswerEquipment1`) and Home Dumbbells + Bands (`QuizAnswerEquipment2`) E2E on the latest build.
   - Account-local Workout History calendar date verification on simulator.
   - Dark Mode and Dynamic Type (`accessibility-large`) full-tab visual regression pass.
   - Password recovery email flow E2E (owner-approved email delivery).

## Deferred, not half-shipped

- **HIIT (Gate 80):** deferred from Workout Engine 2.0 active selection and generation; historical records preserved.
- **Fasting:** domain, use case and storage exist; no client surface. Never presented as available.
- **Exercise media:** no image or video player; text guidance only (technique demonstration videos deferred until catalog media is generated and verified).
- **Android:** not targeted.
