# Findings for the future total implementation plan (running list)
Source: simulator audit 2026-11-10, iPhone 17 Pro, local API, synthetic user. Evidence: ORCHESTRATOR_CHECKPOINT.md + screenshots/.

## P1 (fix before TestFlight)
| ID | Finding | Evidence | Fix direction |
|----|---------|----------|---------------|
| F01 | Bodyweight exercise (Dead Bug) shows placeholder "0" kg; "Далее" fails with "use 0 kg", error banner hides grid until "Повторить" | activity-complete.png | Pre-fill 0 / treat empty weight as 0 for bodyweight in ValidateInputs or WorkoutExercisePresentationMapper |
| F02 | Injury (Колено/Острая) leaves 2026-11-30 session unrestricted; it contains Hack Squat which the app lists as blocked (DB evidence; UI exposure unconfirmed) | DB app.workout_days | Root-cause PostgresWorkoutPlanReconciler horizon/week-3 handling; add regression test |
| F03 | Injury cannot be logged while a workout is in progress | injury-blocked-active-workout.png | Allow injury report mid-session (stop + restrict) or clear in-app path via "Больно" |
| F04 | Workouts tab spinner >30 s after "Завершить сеанс" (no follow-up call) | checkpoint | Fix post-finish reload/navigation |
| F05 | Today nutrition card 0 kcal while Nutrition tab shows 130 | checkpoint | Unify source (daily_nutrition_logs empty vs logged_meals) |
| F06 | Today shows "Начать тренировку" for in-progress workout (Workouts tab correctly says "Продолжить") | readiness-result.png | Share state |

## P2 (polish / localization)
- English leaks in RU UI: Protein/Carbs/Fats, g, P/C/F, "1 serving ... kcal", "Portion", "Knee (Acute) -- 10 exercises blocked", "No active injuries are recorded.", "General strength training", "Strength & Sculpt", "2 sets x 10 reps", "Rest day", "TUE/MON", "1,7 t", "In Progress", "TRAINING", "Next period in 15d", raw enums Hiit/RockClimbing/Running, schedule line "Активности: 2 training days - 5 full rest days - 4" (garbled, labels swapped).
- Copy: "Запишите прием пищи" imperative title and truncated primary button; приём/прием inconsistent; "Начните 60-секундный отдых"; "Обменять" for swap; "Сессия: серия дней"; "Фотоальбом путешествия" in a fitness app; triplicated "Питание" header card.
- Layout: "Мышечная болезненность" tile wraps mid-word; custom-food Carbs/Fat fields under sticky Save; set-row list clipped by action buttons; duplicate "Назад" buttons on exercise screen; 12h time "11:40 AM" in RU.
- UX: check-in save disabled with no hint until sleep hours set (slider needs drag); misleading transient error "plan did not update" after injury save though reconcile succeeded; stale injury error after Cancel.
- Cycle card shows Овуляторная day 14/28 though user answered "Не знаю" (spec check pending).

## Cycle journey (2026-11-10 11:51)
- Data: app.cycle_logs start_date 2026-10-28, cycle_length 0 (unset) -> UI "День 14 из 28", Овуляторная, "Овуляция 11 нояб" (assumes 28-day cycle). Day count derives from the entered date, not fabricated, but ovulation date is stated as fact while period is "Предполагаемая". SAFETY.md: "represent uncertainty explicitly" -> P2: label ovulation/phase as estimate, surface that length was assumed.
- P2 layout (Цикл tab): primary button text truncated "Записать сегодняшние симптомы и интен…"; "Овуляторная" wraps mid-word ("Овуляторна/я"); "День 14 из 28" card cramped next to "Предполагаемая менструация через 15 дн."; "запись от нояб. 10" odd copy.
- P2 personalization copy is generic: "Ориентируйтесь на отметку самочувствия и ограничения." / "Следуйте личным целям по питанию." (no phase-specific guidance in ovulatory phase).
- Quick-log sheet ("Быстрая запись"): PUT /cycle/history/symptoms 200 but NO visible confirmation/toast and no symptom shown afterwards (P2 feedback). Chips truncated ("Менструация началас…", "…закончи…"), "Обильные" wraps mid-word, only one flow-intensity chip visible.
- DB: app.cycle_phase_logs phase=Ovulatory note "Set from onboarding quiz" although user answered "Не знаю" for phase -> phase inferred from last-period date; confirm intended (P2/P1 vs SAFETY.md "represent uncertainty").

## Returning-user relaunch (2026-11-10 11:51) - P1 candidate
- Two consecutive cold relaunches (simctl terminate + launch, ~48 min after sign-in, API still running, user fully onboarded with plan/injury/meal data) land on English "Tell us about yourself / Build your first plan" profile-setup form with default values (no name, 24 Oct 2001, 165 cm) and make ZERO API calls (no /auth/refresh, no /profile). Earlier in the session a relaunch restored Today. API log shows earlier 401s (token expiry) on /workouts/day-sessions/log and /workouts/progress.
- Likely: expired access token / failed refresh routes to onboarding instead of login or refresh -> data-loss-looking experience and risk of overwriting profile. Needs root-cause in startup/auth routing (UI App/Shell startup, token store). Screens: relaunch-after-injury.png, relaunch2.png. Also: onboarding screen is English (not RU) and shows both Imperial/Metric labels with toggle on Metric.
- Not verified: whether completing this form would overwrite the existing profile (not attempted).

## Still unverified / not done
Negative cases (bad login, offline), TestFlight readiness gates, Opus plan, fixes + Sonnet review + retest, 30 Nov Hack Squat UI confirmation.

## TestFlight readiness gates (docs/config only, no signing/upload performed) 2026-11-10
- Existing verdict in docs/beta-readiness/RELEASE_READINESS.md: NO-GO external, NO-GO internal TestFlight until device acceptance + owner actions. This audit's functional evidence supports NO-GO (F01-F07 open).
- Open owner items already tracked: S-03, S-04 (in-session pain report == our F03), S-05, QA01 physical device, live AWS posture.
- Config observations: ApplicationId com.musclecuties.app (csproj) vs sim install com.denispetruk.musclecuties (run script override - confirm release bundle id/provisioning matches Entitlements keychain group com.musclecuties.app). CodesignKey "-" only for Debug simulator (OK). PrivacyInfo.xcprivacy present. ITSAppUsesNonExemptEncryption=false. Portrait iPhone only.
- Review-risk: HealthKit entitlement + NSHealthShare/UpdateUsageDescription say Health data pre-populates check-in/nutrition, while CLAUDE.md says health sync must stay dormant unless owner authorizes -> confirm entitlement/strings match shipped behaviour (App Review rejects unused HealthKit). NSHealthUpdateUsageDescription describes only reading. Whoop URL scheme + Sign in with Apple entitlement present: verify both flows are shipped/enabled.
- Profile row "Политика конфиденциальности: Данные о здоровье синхронизируются с аккаунтом" - verify wording vs actual policy/consent.
- Not executed: archive, signing, upload, device acceptance (require owner authorization).

## Post-relaunch onboarding screen (2026-11-10 11:53)
- The profile-setup page shown after relaunch is entirely English (title, labels, activity cards "High volume strength", "Rock climbing", "HIIT", "Running", "Required/Optional") while the rest of the app is RU -> P2 localization of ProfileSetupPage.
- It pre-selects (pink) High volume strength, Rock climbing and HIIT; for a user with an active acute knee injury (rock climbing/HIIT are on the blocked list) re-onboarding would re-propose blocked activities -> confirm injury state is re-applied on re-onboarding (ties to F07).
- Negative cases (bad login, offline) NOT run: the app has no visible login entry from this state and a re-login needs the user's password (not available to the audit); no authentication bypass attempted.

## Proposed order for the master implementation plan
1. Safety: F02 reconciler horizon, F03/S-04 in-session pain+injury path, regression tests (Core + PostgreSQL disposable DB).
2. Auth/startup: F07 relaunch routing (token refresh vs onboarding).
3. Workout execution: F01 bodyweight weight default, F04 post-finish reload, F06 Today state.
4. Data consistency: F05 Today nutrition source.
5. Localization sweep (EN leaks, ProfileSetupPage, enums, units) + copy fixes; layout truncations (Cycle, check-in, quick-log, custom food).
6. Cycle uncertainty labelling; symptom-save feedback.
7. Release gates: HealthKit/Whoop/Apple Sign-In entitlement vs shipped behaviour, bundle id, device QA01 (owner).

## F07 re-assessment (2026-11-10 11:57) - DOWNGRADED
- After rebuilding/launching via scripts/run-ios-simulator.sh (which injects the loopback API endpoint) the app restores Today correctly with the same session. The earlier two cold relaunches used plain `simctl launch` WITHOUT the script's endpoint config -> app likely could not reach any backend and fell back to the onboarding form with zero API calls. So F07 is probably a harness artifact, but the resilience gap remains (P2): an unreachable/misconfigured backend should show an error/offline state, not a blank onboarding form.
- Today after completion still shows "Начать тренировку" + English "Completed Today", "TRAINING", "Keep it moderate", "Mostly recovered" (F06 + L10n).
## F01 fix status
- Edit applied (ValidateInputs pre-fills "0" for empty weight). Built OK via run script. Sonnet independent review pending. Simulator retest needs an uncompleted bodyweight exercise.
- F07 root cause (harness): the iPhone 17 Pro has TWO installs: com.musclecuties.app (the real build, launched by run-ios-simulator.sh) and com.denispetruk.musclecuties (stale). My two "relaunches" used the stale bundle id, which has no endpoint config -> onboarding with zero API calls. F07 is NOT a product bug; remove from P1 list. (Resilience gap for unreachable backend stays P2.)
- Simulators: iPhone 17 (13EDBFDC) runs an older com.musclecuties.app instance since 10:27 (login screen); the F01 rebuild targets iPhone 17 Pro (678048F6) only.

## F02 RETRACTED (2026-11-10 12:15)
- Root cause analysis: PostgresCheckInAndInjuryStore.SuppressFutureStrengthForInjuryAsync marks ALL future Strength days removed_by_safety except days already completed/credited. The un-suppressed 2026-11-30 day is the planned day my completed session (workout_session_logs.completed_at 2026-11-10 11:43) was bound to (started early from a future planned day on a rest day). So the "Hack Squat in plan" was the already-completed session, not a restriction gap. No defect. Remove F02 from the P1 list.
- Side observation (P2, product decision): starting a workout on a Rest day consumes the next planned strength day (30 Nov) instead of today; confirm intended.
- F01 fix: option-2 edit in WorkoutSessionPresentationMapper.MapExercise (mirror prescribed all-0 kg sets into weight box; ValidateInputs untouched after Sonnet review). Built; retest SKIPPED (needs unlogged bodyweight exercise; owner said skip). Residual risk: server sends 0 kg for unknown loads on weighted lifts -> prefer adding IsBodyweight to WorkoutExerciseDetailDto (shared DTO = integration authority).

## Code pointers gathered for the plan (2026-11-10 12:25)
- F06: DashboardWorkoutCard.xaml (button text fixed to Dashboard_Workout_StartWorkout, only IsRestDay trigger; bound to DashboardViewModel). State-aware label exists only in WorkoutViewModel.cs:698-728 (Workout_Overview_ResumeWorkout). Plan: expose in-progress/completed on DashboardViewModel and add triggers; "Completed Today"/"In Progress" produced in PostgresDashboardOverviewStore.cs:595 as raw English.
- F04: finish path WorkoutSessionViewModel.CompleteSessionAsync/CompletePlannedSessionAsync (~1165-1366) -> _navigateAsync(".."); stuck spinner on WorkoutPage after return (WorkoutPage.xaml.cs OnAppearing:44). Needs runtime trace; no follow-up API call observed after POST /finish.
- L10n root pattern: server/Core return plain English strings (not codes): PostgresWorkoutInjuryStore.cs:82 "exercises blocked", PostgresDashboardOverviewStore.cs:595, DashboardSupportRule.cs:205/215, DashboardPlanner.cs:197/207; client falls back to UiTextCatalog.RuntimeText.cs English->RU dictionary only where the binding uses the translating converter. Plan: switch to message codes + catalog keys (EN/RU parity), not per-string patches.
- Applied now: RuntimeText.cs entry for "No active injuries are recorded." (InjuryLogViewModel.cs:166).
- Edits so far (worktree, uncommitted): WorkoutSessionPresentationMapper.cs (F01), UiTextCatalog.RuntimeText.cs (1 string).

## Negative cases (iPhone 17, logged-out instance, local API) 2026-11-10 12:26
- Empty email+password -> "Invalid email or password"; NO API call (client-side). PASS-with-note (P2: no field-level/required hint).
- Unknown email + wrong password -> POST /auth/password 401 (163 ms); generic "Invalid email or password"; no account enumeration. PASS.
- Note: login screen is English/light on this instance (locale/appearance differ from the RU/dark Pro instance).
- Registration negatives (iPhone 17): empty submit -> "Accept the privacy notice before creating an account." (consent gate first, no API call); invalid email -> "Enter a valid email address." (client-side, one error at a time). PASS. Password rules exist in catalog ("at least 8 characters with uppercase and lowercase...", "Passwords do not match") but NO password-rule hint is visible on the form while typing (P2). Short username "ab" not yet rejected before email error (one-error-at-a-time flow; rule untested).
- Harness: simulator text injection onto already-filled fields appends and screenshots lag one frame; tapping a filled field did not clear it. Not app defects.
- NOT RUN (by design): offline/unreachable-backend test - would require restarting the local API, which invalidates the Pro session tokens (per-process keys). Covered statically: no error/offline state seen when endpoint was wrong (P2, see F07 note).
- Payment/health/third-party flows not exercised.
