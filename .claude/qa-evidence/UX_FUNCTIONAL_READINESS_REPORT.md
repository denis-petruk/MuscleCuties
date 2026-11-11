# UX / Functional Readiness — Initial Task Report (2026-11-10)

Scope: iPhone 17 Pro simulator, synthetic user qa_tester_0110b, local API 127.0.0.1:5080 (DB mc_local). No commits, pushes, AWS or TestFlight actions. Tests not run.

## Verified PASS
- Launch to sign-in (Russian locale).
- Registration and email verification (dev code from API output).
- Onboarding step 1: height, weight, birthday, wellness acknowledgement, activity selection.
- Onboarding quiz (11 questions) completed; plan generated: 2 training days, Mon "General strength training", 45 min, 6 exercises, moderate intensity.
- Workout exercises 1–5 logged at 20 kg, rows verified in app.exercise_set_logs; advancing does not duplicate rows.

## VERIFIED FAIL / CONFIRMED GAPS
1. Exercise 6 (Dead Bug, bodyweight) blocks session completion: error "Укажите корректный вес в кг…" with no visible weight/set inputs; "Повторить" and "Пропустить" have no visible effect. No save request reaches the API. Severity P0 for completing a workout. Root cause not confirmed (flags conflict; see checkpoint).
2. Localization: quiz questions (most), answer labels in some questions, "Finish" button, activity titles and exercise names are English under Russian UI.
3. Height/weight are wheel pickers; typed text is not applied (taps must target the field).

## Downgraded / not defects
- 0.0 kg row before input: display placeholder, not persisted.
- 3 training days selected, 2 scheduled: matches documented beta cap (2 strength sessions/week).
- Resend code 400 after API restart: per-process keys in dev delivery; re-registration required.

## NOT VERIFIED (initial task)
Workout completion and history, daily readiness, injury/safety, cycle personalization, returning-user relaunch, nutrition, negative scenarios, TestFlight gates, fix-and-retest loop, Opus gates.

## Evidence
- Screenshots: .claude/qa-evidence/screenshots/ (launch, onboarding, quiz, workout)
- Logs: .claude/qa-evidence/UX_JOURNEY_LOG_20261110.md, ORCHESTRATOR_CHECKPOINT.md
- Local API log: /tmp/mc-local-api.log

## Verdict
NOT READY for the workout-completion journey (P0 blocker, exercise 6). Functional readiness of onboarding and plan generation: CONDITIONALLY READY pending localization fixes. Release-distribution readiness: INSUFFICIENT EVIDENCE.
