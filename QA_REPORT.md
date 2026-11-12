# QA audit — MuscleCuties (iOS client, local stack)

Audit date: 2026-11-12. Read-only audit; no product source was modified.

## 1. Summary

The app builds, launches on the simulator, signs in, and the five-tab core (Today, Cycle, Train,
Nutrition, You) renders with a consistent surface/typography system. A workout plan can be
generated, previewed, and accepted against a real local API, which is the most safety-sensitive
path and it completed with correct server calls.

The release is **not shippable as it stands**. Four blockers were reproduced on the running app:

1. Onboarding can never be completed from the client: the date-of-birth field opens no picker and
   the "Подтвердить дату рождения" (confirm birthday) button does nothing when tapped, so a new
   account can never be created in the app. The audit had to create its test account through the
   server API to reach the rest of the product.
2. Localization is incomplete in every major area. Russian screens contain English strings —
   "Active Recovery", "Protein/Carbs/Fats", "THU · день отдыха", "Rest day", "2 training days - 5",
   "High volume strength", "Strength", "MON", "45 min", "Strength & Sculpt", and the password rule
   message — which the design contract treats as a release defect, not a fallback.
3. The Cycle screen's empty state renders its text one character per line inside a collapsed
   container and its primary action is clipped mid-word, so the screen is unusable in the state
   every new account starts in.
4. A workout session never reaches a stored terminal state. `Далее` refuses the default 0 kg while the
   message tells the user to use 0 (D25, with a skip workaround), and neither `Прервать активность` nor
   `Завершить сеанс` writes `completed_at`: the app leaves two open session rows and a duplicate
   completion record, and the card still offers to resume after a cold restart (D26).

Two automated suites are also red on this commit: 1/1378 Core tests (a referenced policy file is
missing from the checkout) and 1/60 Registration tests (the profile personal-info save stops
sending `cycleLength`, which is a data-persistence regression, not a test-only problem). The
Server latency gate is blocked by missing configuration rather than by a failed assertion.

The visual system is token-clean but composition-inconsistent. All 43 of the repo's style gates pass,
yet §9 records 24 styling inconsistencies: chemistry-level details (colours, sizes, radii) are owned
by tokens, while the decisions that make a screen read as one product — which header a page uses,
which card and button weight it picks, one status recipe, one casing rule — are authored per page or
baked into localized copy. Three are P1: Nutrition is the only tab without the shared header (S10),
large text scales the title without bound while tab labels never scale (S21), and the Cycle screen's
clipped action/collapsed empty state (S22, already D3).

### What this pass changed

The active workout session was reached and driven, and the check-in was completed:

- **Session:** the plan was shifted onto today, the session opened, all five exercises were walked to
  the summary and "Завершить сеанс" was tapped. Every control was exercised: notes (saves and persists),
  rest timer (start/pause/resume/+30 s/finish), swap panel, pain modal, set logging, skip, next, back,
  abort sheet. D25 was narrowed (0 kg is rejected but a typed weight works) and D26 was widened
  (finish also fails to close the session). The plan was then restored from backup and today reads
  "Rest day" again.
- **Check-in:** saves correctly once the sleep slider is dragged; the earlier "never enables" finding
  is retracted (D5) and what remains is an undisclosed requirement (D19, now P3).
- **Profile:** five pushed routes and the photo vault were opened. Nutrition settings renders with two
  new layout defects (D27, D28) and an English value (D29).
- **Food diary tabs:** the barcode and "Своё" tabs were opened and exercised; a barcode was submitted and
  a custom food saved. New defect D37.
- **Cycle phase:** a phase was created through the app's own quick log ("Отметить начало менструации"),
  which let the phase card, "Следующая фаза" and the phase/detail controls be tested live. New defects
  D34 (the detail page and its sheets have no binding at all) and D35, plus D36 for the chip states.
- **Quiz:** proven unreachable rather than skipped — see §7.

## 2. Environment and conditions

| Item | Value |
| --- | --- |
| Branch / commit | `chore/repo-hygiene-and-testflight-sync` at `34f9ed85c57c0a885fb43670f5de2fe50c2e7581` |
| Worktree state | Clean at start; at the end the only changes are this report (untracked) and `STATUS.md` (modified). No product source, XAML, contract or test file was touched |
| Host | macOS, Xcode 26.3 (17C529), .NET SDK 10.0.203 |
| Simulator | iPhone 17 Pro, iOS 26.3, UDID `678048F6-C4C7-406C-A970-7104AED3E00A` |
| Appearance / language | Dark + Russian (primary pass), light + Russian (contrast pass), dark + Russian at Accessibility XXXL (text pass) |
| Client build | `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64` — succeeded, 0 warnings, 0 errors |
| Backend | `scripts/run-local-api.sh --migrate` on `127.0.0.1:5080` against disposable loopback PostgreSQL database `mc_qa_20261112` (schema v7, 746 catalog rows seeded) |
| Accounts | `qa.audit.1112@example.com` (created through the app up to onboarding), `qa.audit.seed1117@example.com` (created through the registration API with a complete profile, used for in-app flows) |
| UI automation | Maestro 2.12.1 (XCUITest driver) driving real taps/typing; `xcrun simctl` for launch, screenshots, appearance and content-size changes |
| Evidence | Screenshots and flow files under `/tmp/mc-qa-2026-11-12/` (local, not committed) |

### Changes made to the test environment (all recorded so the evidence stays honest)

| Change | Why | How | Reverted |
| --- | --- | --- | --- |
| `app.workout_days` for the audit user shifted 4 days earlier (`date - 4`, `day_of_week + 3 mod 7`, 24 rows) | The accepted plan starts Fri 13 Nov, so 12 Nov had no session and no start control existed | Single transaction in `mc_qa_20261112`; pre-change rows saved to `app.qa_backup_workout_days_20261112` | No — the shift is what makes the session evidence reproducible today |
| `app.user_quiz_responses` for the audit user deleted (17 rows), then restored | To test whether an empty quiz routes the app to the Quiz page | Backup in `app.qa_backup_quiz_responses_20261112`; restore verified as 17 rows | Yes |
| Session started and left open | To exercise the session screen and the abort path | In-app: 17:33 start, one set logged, 17:38 abort | The aborted session is intentionally left as found (D26); no cleanup by hand |
| Check-in saved | To verify persistence | In-app: sleep 6,2 ч, quality 3, energy 3, soreness 1, pain Нет, bloating Нет | Data is synthetic and left in place |
| Nutrition dietary toggle on/off | To verify the selector | In-app, never saved | Yes (toggle returned to off) |
| Sign-out confirmation | To see whether the destructive action is guarded | In-app, cancelled with Отмена | Yes |
| `completed_at` written on both open `workout_session_logs` rows | The app's own finish left them NULL (D26); needed to leave the QA account with no resumable session | `UPDATE app.workout_session_logs SET completed_at = started_at + interval '9 minutes 8 seconds' WHERE completed_at IS NULL` → UPDATE 2 | No — this is the terminal state the audit certifies; the app never writes it |
| `app.workout_days` restored from `app.qa_backup_workout_days_20261112` | To return the plan to its authored dates | `UPDATE … FROM app.qa_backup_workout_days_20261112` → UPDATE 24; `SELECT count(*) … WHERE d.date <> b.date` → 0 | Applied — the shift is gone; Train shows "THU · день отдыха / Rest day" again (`shots/224`) |
| Custom food "QA Custom Food" created in-app (99 kcal) | To exercise the Своё tab's save path | In-app save on the disposable database | `DELETE FROM app.user_food_items WHERE name = 'QA Custom Food'` → DELETE 1 |
| Cycle record created in-app (start 2026-11-12), one symptom log, one manual phase advance | To obtain the known phase the phase screens require | In-app: quick log → symptom "Тяга к еде" → save → "Отметить начало менструации"; then "Следующая фаза" | Deleted in FK order (`symptom_logs` → `cycle_phase_logs` → `cycle_logs`), then `current_cycle_phase` reset to NULL; verified 0/0/0 and Cycle back to "Неизвестно фаза" (`shots/275`) |
| `app.user_profiles.date_of_birth` set to `1990-01-01`, then back to `1996-03-15` | To test whether a valid seeded birth date lets the profile save reach the Quiz | `UPDATE` + `SELECT` verification, then the inverse `UPDATE` (verified) | Yes |

**Plan shift and restore (both recorded).** The plan rows were shifted four days earlier on the previous pass so 12 Nov would carry the authored strength day; this pass they were restored from `app.qa_backup_workout_days_20261112` and the query above returns 0 shifted rows, with Train back to "Rest day" for today (`shots/224`).

**Device clock was not changed.** `xcrun simctl` cannot set the simulator's system time (it inherits the
host clock; `status_bar --time` only redraws the status bar), and changing the host clock needs
administrator rights this audit does not have. The plan data was shifted instead — the smallest change
that produces a training day — and every screenshot of the session therefore shows real date 12 Nov
with a plan that was authored for 16 Nov.

No source, XAML, contract or test file was edited. No account was deleted, no payment made, no message
sent, and no external service called.

### Tooling notes

- **Browser-control MCP tools were not applicable.** This is a native .NET MAUI iOS client with no
  web front end and no web-accessible routes, so the browser/preview tooling cannot exercise it.
  UI verification used the simulator with Maestro instead, which drives the real rendered app.
- **Wizard / Taste / Impeccable:** available here as the account skills `wizard`, `taste-skill` and
  `impeccable`. They are authoring/design skills, not QA instruments, and none was used; nothing was
  installed and no substitute was silently chosen.
- `docs/VERIFICATION_GATES.md` G5 says agents do not run the MAUI UI by default, while root
  `AGENTS.md` §4 allows simulator build/run/screenshots without approval and this audit mission
  explicitly requires running the app. The mission and `AGENTS.md` were followed.

## 3. Page map

Five Shell tab roots plus pushed routes and modal sheets.

| Area | Surface | Route / registration |
| --- | --- | --- |
| Startup | `AppStartupPage` | ShellContent `AppStartupPage` |
| Auth | `LoginPage` | ShellContent `LoginPage` |
| Auth | `RegisterPage` | `Routing.RegisterRoute` |
| Auth | `OtpVerificationPage` | ShellContent + route |
| Auth | `PasswordRecoveryPage` | `Routing.RegisterRoute` |
| Onboarding | `ProfileSetupPage` | ShellContent `ProfileSetupPage` |
| Onboarding | `QuizPage` | ShellContent `QuizPage` |
| Today | `DashboardPage` | Tab `DashboardPage` |
| Cycle | `CyclePage` | Tab `CyclePage` |
| Cycle | `CyclePhaseDetailPage` | `Routing.RegisterRoute` |
| Train | `WorkoutPage` | Tab `WorkoutPage` |
| Train | `StrengthPlanGenerationPage` | `Routing.RegisterRoute` |
| Train | `WorkoutSessionPage` | `Routing.RegisterRoute` |
| Train | `WorkoutHistoryPage` | `Routing.RegisterRoute` |
| Nutrition | `NutritionPage` | Tab `NutritionPage` |
| Nutrition | `FoodLogPage` | `Routing.RegisterRoute` |
| You | `ProfilePage` | Tab `ProfilePage` |
| You | `ProfilePersonalInfoPage` | `Routing.RegisterRoute` |
| You | `ProfileNutritionSettingsPage` | `Routing.RegisterRoute` |
| You | `ProfileWorkoutPreferencesPage` | `Routing.RegisterRoute` |
| You | `ProfileUnitsDisplayPage` | `Routing.RegisterRoute` |
| You | `ProfileFeedbackPage` | `Routing.RegisterRoute` |
| You | `ProfilePrivacyPage` | `Routing.RegisterRoute` |
| You | `PhotoJourneyVaultPage` | `Routing.RegisterRoute` |

Modal surfaces (14): daily check-in, injury log, meal entry, meal editor, meal detail, meal
suggestion, nutrition breakdown, cycle quick log, cycle phase summary, cycle date/phase, cycle
warning, profile image crop, workout swap, workout activity chooser. The active-session
day-overview is a non-dismissible in-page state, not a sheet.

Roles: one authenticated end-user role. There is no admin surface and no role switch in the client;
access control is a client navigation guard plus server bearer-token ownership.

## 4. Coverage matrix

Status values: PASS (exercised and behaved), FAIL (exercised and defect reproduced), PARTIAL
(exercised, further states untested), BLOCKED (cannot be reached or driven), NOT RUN.

| # | Page / workflow | Preconditions | Steps | Expected | Actual | Status | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Login, empty submit | Signed out | Tap Войти with empty fields | Required-field message | "Неверная почта или пароль" (invalid credentials) | FAIL | `shots/02-login-empty.png` |
| 2 | Register, consent gate | Signed out | Tap Создать аккаунт without consent | Blocked with reason | Privacy message shown, no request sent | PASS | `shots/03-register-empty.png` |
| 3 | Register, field validation | Consent checked, empty fields | Submit | All gaps reported | Only "Введите действительный адрес электронной почты." | PARTIAL | `shots/04-register-consent-empty.png` |
| 4 | Register, password rule | Filled form, weak password | Submit | Localized rule message | English message in RU UI | FAIL | `shots/08-otp.png` |
| 5 | Register, happy path | Valid data + consent | Submit | OTP screen | OTP screen, code issued (`[local-dev-email]`) | PASS | hierarchy capture |
| 6 | OTP, wrong code | OTP screen | Enter 000000 | Localized rejection | 401 + "Введите корректный 6-значный код подтверждения." | PASS | API log |
| 7 | OTP, keyboard | OTP screen | Type code | Commit action reachable | Action fully hidden behind keyboard | FAIL | `shots/11-otp-keyboard.png` |
| 8 | OTP, correct code | OTP screen | Enter issued code | Advance to onboarding | 200, `ProfileSetupPage` | PASS | API log |
| 9 | ProfileSetup, untouched form | Step 1 open | Tap Продолжить | Inline gap + action | Wellness message first, one gap at a time | PARTIAL | `shots/16-profilesetup-empty.png` |
| 10 | ProfileSetup, birth date | Step 1 open | Tap date field, then confirm | Picker opens; confirm accepted | Picker never opens; confirm button inert | FAIL | `shots/34-date-after.png` |
| 11 | ProfileSetup, height/weight | Step 1 open | Pick 164 cm / 62 kg | Values persisted | Persisted and displayed | PASS | `shots/34-date-after.png` |
| 12 | ProfileSetup, footer | Step 1, bottom of form | Scroll to end | All content reachable | Footer overlays the activity cards | FAIL | `shots/24-date-open.png` |
| 13 | Quiz | After step 1 | Answer and submit | Advances to Today | Unreachable for a signed-in account: its only entry is the post-profile-save callback, and onboarding Step 1 cannot complete (row 10 / D1). Clearing the stored quiz answers and relaunching still lands on Today | BLOCKED | `shots/174-quiz-route.png`, `shots/175-landing.png` |
| 14 | Sign-in, seeded account | Onboarding complete server-side | Enter credentials | Today tab | Today tab, 5 tabs | PASS | `shots/41-today.png` |
| 15 | Today, empty state | No check-in, no plan data | Open tab | Honest empty states | Rendered; duplicate readiness copy | PARTIAL | `shots/41-today.png` |
| 16 | Today, check-in sheet | Today | Open Заполнить отметку | Sheet with fields | Opens, Apple Health opt-in honest | PASS | `shots/53-after-checkin.png` |
| 17 | Check-in, disabled save | Sheet open, sleep slider untouched | Tap Сохранить отметку | Explains unmet prerequisite | Button dimmed, no explanation, nothing happens; dragging the sleep slider enables it | PARTIAL | `shots/193-checkin-sheet.png`, `shots/194-sleep-dragged.png` |
| 18 | Check-in, save | Answers plus sleep hours given | Answer all, drag sleep, save | Persists, closes | Sheet closed and Today renders Готовность 69/100, Восстановление 84/100 | PASS | `shots/197-checkin-all-set.png`, `shots/198-checkin-saved.png` |
| 19 | Cycle, empty state | No cycle data | Open tab | Readable empty state | Text stacked one letter per line; button clipped | FAIL | `shots/42-cycle.png` |
| 20 | Train, no plan | No active plan | Open tab | Empty state + create action | Rendered correctly | PASS | `shots/43-train.png` |
| 21 | Train, preferences | From Создать план | Open | Weekdays/equipment editable | 3 weekdays selected, equipment section | PASS | hierarchy capture |
| 22 | Plan preview | Preferences saved | Перейти к настройке плана | Preview with composition | Mon/Wed 45 min, rest days, reduced-volume notice | PASS | `shots/61-generation.png` |
| 23 | Apply plan | Preview open | Применить план | Applied, preview closed | PUT prefs + proposal + lease + accept 200; navigates to Today | PASS | API log |
| 24 | Train after apply | Plan active | Re-open Train | Active plan, no preview | Stale preview sheet still shown | FAIL | `shots/64-train-plan.png` |
| 25 | Session card | Plan active | Tap Monday session | Session detail | Detail with 5 exercises, "Около 23 мин" vs 45 min plan | PARTIAL | hierarchy capture |
| 26 | Active session screen (set logging, swap, finish) | Training day present | Начать тренировку → activity → Начать | Session screen; controls act | Screen reached and driven end to end, but it cannot be completed: `Далее` is blocked by the weight validation (D25) and `Прервать активность` does not end the session (D26) | FAIL | `shots/138`, `shots/156`, `shots/172` |
| 27 | Nutrition overview | Signed in | Open tab | Summary + entry points | Rendered; English macro labels | PARTIAL | `shots/44-nutrition.png` |
| 28 | Nutrition, light appearance | Light mode | Open tab | Legible | Legible body; header eyebrow near-invisible | PARTIAL | `shots/70-nutrition-light.png` |
| 29 | Meal entry sheet | Nutrition | Добавить прием пищи | Compact context then Add food | Sheet opens (not driven to save) | PARTIAL | flow log |
| 30 | Food diary (page, meal save, templates, food search) | Nutrition | Дневник питания / meal entry | Diary listing a saved entry | PARTIAL: the custom-food path works end to end — an entry was created through the app, listed on Nutrition and inside the diary's "Перекус" card, and notes were saved with a toast. Catalog food search, templates and recipes were not exercised | PARTIAL | `shots/97-diary-meal-row.png`, `shots/100-foodlog-meal-entry.png`, `shots/101-toast-notes-saved.png`, `shots/102-foodlog-snack-row.png` |
| 31 | You overview | Signed in | Open tab | Profile summary | Rendered; inset-grouped rows, `&` in Russian section title | PARTIAL | `shots/81-s25-you-grouped-rows.png`, `shots/82-s25-you-rows-scrolled.png` |
| 32 | You subpages — the 7 pushed Profile routes (Personal info, Nutrition settings, Workout preferences, Units & display, Feedback, Privacy, Photo vault) | Signed in | Open each and exercise its primary action | Pages render and act | All seven opened. Units save works; diet toggles work; the macro editor collapses its card (D27); workout availability's back arrow lands on Train (D30); vault renders its empty state | PARTIAL | `shots/180`, `shots/184`, `shots/185`, `shots/186`, `shots/187`, `shots/192` |
| 33 | Photo vault | You | Open | Private storage view | Never rendered: not reached from the You overview | NOT RUN | — |
| 34 | Cached-session restart | Signed in | Terminate + relaunch | Session restored | Restored to signed-in state | PASS | `shots/72-relaunch-xxxl.png` |
| 35 | Large text (XXXL) | Accessibility XXXL | Relaunch, open Today | Layout adapts | Greeting fills the screen; tab labels do not scale | FAIL | `shots/72-relaunch-xxxl.png` |
| 36 | Small device (iPhone SE) | — | Install, launch | Layout holds | Not run | NOT RUN | — |
| 37 | iPad / tablet | — | Launch | Layout holds | Not run (iOS phone target) | NOT RUN | — |
| 38 | VoiceOver navigation | — | Drive with VoiceOver | Labels/focus correct | Not driven | NOT RUN | — |
| 39 | Core suite | — | `dotnet test tests/MuscleCuties.Core.Tests` | Green | 1377 passed, 1 failed | FAIL | §6 |
| 40 | Registration suite | — | `dotnet test tests/MuscleCuties.Registration.Tests` | Green | 59 passed, 1 failed | FAIL | §6 |
| 41 | Mobile contracts | — | `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests` | Green | 183 passed | PASS | §6 |
| 42 | Server suite | Disposable DB env | `dotnet test tests/MuscleCuties.Server.Tests` | Green | 119 passed, 1 blocked (latency gate needs `MC_LATENCY_POSTGRES`) | PARTIAL | §6 |

### Navigation and access-control observations

- Tab switching between all five destinations worked in both directions, and returning to a tab
  restored its content.
- The Shell guard redirects an unauthenticated deep navigation to sign-in; authenticated navigation
  to a guarded route performed the expected `GET` calls and rendered.
- Back navigation from the plan preview returned to the Train tab; no crash or blank page observed.
- "Refresh" has no web equivalent; the closest check was process termination and relaunch, which
  restored the signed-in session (#34).

## 5. Defects

Every defect states whether it was reproduced on the running app or found only by reading code.

### D1 — P0 — Onboarding cannot be completed: birth date cannot be set or confirmed
**Reproduced in the running app.** Affects every new registration; the client never calls
`POST /api/v1/accounts/registration`, so no account is created.
Repro: sign out → Создайте учетную запись → complete fields and consent → verify OTP → on Step 1 of
2 tap the "День рождения" field, then tap "Подтвердить дату рождения".
Expected: the date picker opens and the chosen (or confirmed) date satisfies the step.
Actual: tapping the date field opens no picker; tapping the confirm button (by text, by
`AutomationId ProfileSetupConfirmBirthDateButton`, and at its measured centre point) leaves the
button visible and `BirthDateConfirmed` false. Tapping Продолжить keeps returning
"Укажите дату рождения."
Evidence: `/tmp/mc-qa-2026-11-12/shots/34-date-after.png`, API log with no registration POST after
the successful `email-challenges/verify`.
Code corroboration: `ProfileSetupViewModel.ConfirmBirthDate` is unconditional except while loading,
so a reachable button should set the flag; the control is therefore not receiving the tap.

### D2 — P1 — English copy inside the Russian interface
**Reproduced in the running app.** The design contract makes an untranslated string a release
defect.
Repro: set the simulator to Russian, then visit the surfaces below.
Actual:
- Today: "Active Recovery" inside the rest-day card.
- Nutrition: macro labels "Protein", "Carbs", "Fats".
- Train: "THU · день отдыха", "Rest day", "MON · силовая", "Strength", "High volume strength",
  "45 min", "2 training days - 5".
- ProfileSetup: activity cards "High volume strength", "Strength high intensity", "Climbing".
- Activity chooser: "Strength & Sculpt".
- Food diary: the snack meal group is headed "Snacks" while every sibling group is Russian
  ("Завтрак", "Обед", "Ужин").
- Register: "Password needs 8+ characters with uppercase, lowercase, number, and symbol."
  (source: `src/MuscleCuties.Core/Domain/Auth/AuthInputValidator.cs:6`).
Evidence: `shots/41-today.png`, `shots/44-nutrition.png`, `shots/08-otp.png`,
`shots/64-train-plan.png`, `shots/102-foodlog-snack-row.png`.

### D3 — P1 — Cycle empty state collapses and its primary action is clipped
**Reproduced in the running app.**
Repro: open the Цикл tab on an account with no logged cycle.
Expected: a readable empty state with a full-width action.
Actual: the empty-state heading renders one character per line ("Д / е / н / ь …") in a collapsed
container, and the primary button label is cut mid-word ("Записать сегодняшние симптомы и интен").
Also the phase heading reads "Неизвестно фаза" (grammatically wrong Russian).
Evidence: `shots/42-cycle.png`.

### D4 — P1 — Accessibility text sizes break the layout
**Reproduced in the running app.**
Repro: set iOS text size to Accessibility XXXL, relaunch, open Today.
Expected: content reflows and remains usable; all essential text scales.
Actual: the greeting grows to fill most of the screen, pushing the primary content off-view, while
tab-bar titles stay at their default size. The first card is empty in view and the page cannot be
read as a whole without heavy scrolling.
Evidence: `shots/72-relaunch-xxxl.png`.

### D5 — RETRACTED after re-test — the check-in does save
Originally reported as unable to save. The missing step was the sleep slider: it must be dragged, and
a tap on the track does not move it. With the knob dragged to 6,2 ч and the four selector answers set,
"Сохранить отметку" enables, the request succeeds, and Today renders Готовность 69/100 and
Восстановление 84/100 (`shots/194`, `shots/197`, `shots/198`). No defect beyond the undisclosed
requirement recorded in D19.

### D6 — P2 — OTP primary action is hidden by the keyboard
**Reproduced in the running app.**
Repro: reach the code screen, tap the code field, type a code.
Expected: the commit action stays reachable, or the field submits on return.
Actual: "Верифицировать код" sits entirely under the numeric keyboard; the only way to submit is to
dismiss the keyboard first, which is not signalled.
Evidence: `shots/11-otp-keyboard.png`.

### D7 — P2 — Validation reports one gap per submit and leaves stale errors
**Reproduced in the running app.**
Repro: register with an empty form and consent checked, then fix only the email and submit again;
separately check consent after a privacy error.
Expected: all gaps at once ("All gaps are reported together").
Actual: each submit reveals the next single error (email → password → …). On ProfileSetup the same
pattern takes three submits (consent → birth date → …). The privacy error stays on screen after the
consent box is checked.
Evidence: `shots/04-register-consent-empty.png`, `shots/16-profilesetup-empty.png`,
`shots/04-register-consent-empty.png` (the checked state was observed in-app; the flow artifact was not kept).

### D8 — P2 — A fabricated default date of birth is pre-filled
**Reproduced in the running app.**
Repro: open onboarding Step 1 without touching the date control.
Expected: an empty date with the same "required" gate.
Actual: the field shows `12.11.2008` (today minus 18 years). Combined with D1 a user cannot change
it, and the screen's only gate for it is the field being unconfirmed.
Evidence: `shots/16-profilesetup-empty.png`, `shots/34-date-after.png` and the flow log,
`shots/16-profilesetup-empty.png`.

### D9 — P2 — First plan is not auto-applied, and the preview survives Apply
**Reproduced in the running app.**
Repro: Train with no active plan → Создать план → preferences → Перейти к настройке плана →
Применить план; then return to Train.
Expected (per `AGENTS.md` §3): a feasible first plan generated in the foreground applies
automatically; an active plan is replaced only through **Apply plan** after the control states the
replacement consequence.
Actual: the first plan is offered as a manual "Применить план" decision with no stated consequence,
and after applying, reopening Train still shows the preview sheet rather than the active-plan view.
Evidence: `shots/61-generation.png`, `shots/64-train-plan.png`.

### D10 — P2 — Prescribed duration contradicts the session card
**Reproduced in the running app.**
Repro: apply the plan, open Train, compare the week list with the Monday session detail.
Expected: one duration.
Actual: the week list and preview show "45 мин"; the session detail shows "Около 23 мин".
Evidence: `shots/64-train-plan.png` and the session-detail hierarchy capture.

### D11 — P2 — Russian grammar defects in shipped copy
**Reproduced in the running app.**
- "Упражнения — 5 упражнения" (should be "5 упражнений").
- "Неизвестно фаза" (should be "Фаза неизвестна").
Evidence: session-detail hierarchy capture, `shots/42-cycle.png`.

### D12 — P2 — Facts are restated on the same screen
**Reproduced in the running app.**
- Today: "Заполните отметку, чтобы увидеть готовность" appears twice plus a "Нужна отметка" badge.
- You: "Предпросмотр скрыт для конфиденциальности / Нажмите, чтобы показать" next to a "Показать"
  button.
- Nutrition: the header shows "Питание" as both eyebrow and title.
Expected (per `DESIGN.md`): a fact appears once per screen.
Evidence: `shots/41-today.png`, `shots/45-profile.png`, `shots/44-nutrition.png`.

### D13 — P3 — Onboarding footer overlaps scrolling content
**Reproduced in the running app.**
Repro: onboarding Step 1, scroll to the activity list.
Expected: the last card is fully visible above the pinned footer.
Actual: the activity cards render under the footer; the last card's title and description are cut.
Evidence: `shots/24-date-open.png`.

### D14 — P3 — Light-appearance header eyebrow is effectively invisible
**Reproduced in the running app.**
Repro: light appearance, open Nutrition.
Expected: ≥4.5:1 for the eyebrow label.
Actual: the small "Питание" eyebrow is pale rose on pale rose and is not legible.
Evidence: `shots/70-nutrition-light.png`.

### D15 — P3 — Mixed-language punctuation and header artifacts
**Reproduced in the running app.**
- "ЗДОРОВЬЕ & ТРЕНИРОВКИ" uses a Latin ampersand in a Russian section title.
- The Cycle title reads "ноябрь 2026" (lower-case month at the start of a title).
- The You tab shows a stray icon above the title, inside the top safe area.
Evidence: `shots/45-profile.png`, `shots/42-cycle.png`.

### D16 — P1 — Personal-info save drops the cycle length (failing suite)
**Found by running tests; not reproduced end-to-end in the UI.**
`PersonalInfoNoFabricationTests.Save_never_writes_a_placeholder_birth_date_experience_or_cycle_length`
fails: the serialized payload has `cycleLength: null` where the saved snapshot has `30`.
Expected: the existing value is preserved when the user does not change it.
Actual: `InvalidOperationException` reading `cycleLength` as a number.
Evidence: §6 command output.

### D17 — P2 — Core suite references a policy file that is absent
**Found by running tests.**
`PlanningReleaseApprovalTests.Approved_reference_is_the_policy_version_recorded_in_the_owner_decision`
throws `FileNotFoundException` for `docs/release-audit/2026-11-05/WORKOUT_POLICY.md`; that directory
does not exist in this checkout.
Evidence: §6 command output; `ls docs/release-audit/2026-11-05/` → "No such file or directory".

### D18 — Closed as NOT A DEFECT (previously P2) — the planned-session 404 is the designed probe
**Request observed in the running app; classification established from source.**
Repro: Train → Monday session → Начать тренировку.
Observed: `GET /api/v1/workouts/sessions/planned/{workoutDayId}` returns 404 and the activity
chooser sheet appears.
Why it is not a defect: the endpoint answers `NotFound()` only when no active session exists for that
planned day (`services/MuscleCuties.Server/Workout/ActiveSession/WorkoutActiveSessionEndpointMappings.cs:25-31`),
and the client maps that status to `null` before offering the next start step
(`src/MuscleCuties.Infrastructure/Cloud/CloudNutritionAndWorkoutStores.cs:473-478`). A 404 here is the
expected "nothing in progress yet" answer.
Residual risk: the session screen itself was never rendered, so it stays untested (row 26).

### D19 — P3 — The sheet does not say that sleep hours are required
**Reproduced in the running app.**
Repro: Today → one scroll → "Заполнить отметку" → answer quality, energy, soreness, pain and bloating
but leave the sleep slider untouched.
Expected: the disabled Save explains what is missing, or the sleep field is marked required.
Actual: "Сохранить отметку" stays dimmed with no hint (`shots/197` before the slider was moved shows
the same state on a fresh sheet), the sleep value renders as "—" (`shots/193`), and only dragging the
knob enables the action (`shots/194`, `shots/198`). Correctness is fine; discoverability is not.
Source: `ViewModels/Dashboard/DailyCheckInViewModel.cs:64` (`Answers.IsComplete` includes sleep),
`:107` ("—").

### D20 — RETRACTED after re-test — today's check-in action is reachable by scrolling
Originally reported as clipped and unhittable. A second pass scrolled the page first: the button is
fully visible (`shots/120-today-cta-revealed.png`) and opens the sheet (`shots/121-checkin-open.png`).
The first-pass screenshot showed it at the fold with the page not yet scrolled; the failure was scroll
position and an off-screen accessibility target, not an overlay or a broken control. No defect.

### D21 — RETRACTED after re-test — Train's filter chips scroll fully into view
Originally reported as clipped by the tab bar. After one scroll the full row is visible and tappable
(`shots/130-sessions-list.png`); the first capture simply caught the page before scrolling. The only
residual observation is cosmetic: "Восстановление" wraps to two lines. No defect.

### D22 — P2 — The day-plan sheet stacks meal labels and ships English content
**Reproduced in the running app.**
Repro: Nutrition → "План на день".
Expected: a readable Russian plan sheet.
Actual: the meal labels collapse one letter per line ("Зав/тра/к", "Обе/д", "Ужи/н"); meal names,
ingredients and macros are English ("Scrambled Eggs with Toast", "Eggs + Olive Oil",
"582 kcal · 28% of daily target", "P 33g C 45g F 30g"); and the per-meal primary action reads
"Используйте прием пищи", an instruction rather than an action label.
Evidence: `shots/105-day-plan-result.png`, `shots/115-sweet-plan.png`, `shots/107-plan-add-result.png`.

### D23 — P2 — Several buttons expose no accessible label
**Reproduced in the running app (through the accessibility tree).**
Repro: drive the app by accessibility label rather than by coordinate.
Expected: every visible button resolves by its text.
Actual: "Добавить еду", "Сохранить пользовательскую еду", "Получить совет", "Открыть день отдыха"
and the "Дневник питания" link are visibly on screen but do not resolve by text; each had to be hit by
coordinate, and "Получить совет" could not be reached at all
(`shots/92-food-search-idle.png`, `shots/93-custom-food-form.png`, `shots/106-today-buttons.png`).
Evidence: flow logs 2026-11-12_164356, _164704, _171202, _171511; screenshots above.

### D24 — P3 — The archive's empty copy contradicts its own heading
**Reproduced in the running app.**
Repro: Train → "Архив тренировок" with no completed sessions.
Expected: an empty-state line for "Прошедшие сеансы".
Actual: the section shows "Все упражнения загружены" ("all exercises are loaded"), which describes the
exercise catalog rather than the empty session list.
Evidence: `shots/111-archive.png`.

### D25 — P2 — "Далее" rejects the bodyweight value its own message recommends
**Reproduced in the running simulator.**
Repro: Train → Начать тренировку → Strength & Sculpt → Начать → on any exercise, leave the кг field at
its default 0 and tap Далее.
Expected: 0 is accepted as bodyweight, because the error the app prints says so, or the field starts
empty and is marked required.
Actual: "Укажите корректный вес в кг. Для собственного веса используйте 0 кг." — 0 is refused while the
message tells the user to use 0. The banner also pushes Подходы / Повторы / кг out of view, so the field
it names cannot be edited until the exercise is skipped. Typing a non-zero weight *before* pressing
Далее works normally: the set is logged ("Первая запись: Machine Chest Press. Отличное начало!") and the
flow advances (row 50).
Workaround: enter a weight (or skip the exercise) before the first Далее. Because a skip is required to
escape the error state, this is a real defect, not a cosmetic one, but the session is completable.
Evidence: `shots/208` (defaults), `shots/214` (error hides the inputs), `shots/210`, `shots/211`
(weight first → logged and advanced).

### D26 — P1 — Neither abort nor finish closes the session; the app leaves two open records
**Reproduced in the running simulator, with database evidence before and after a cold restart.**
Repro A: start a session, log one set, back → "Прервать активность" → confirm.
Expected: the session ends (the sheet itself says the activity "завершится и останется невыполненной").
Actual: returns to Train with the session still live; re-entering resumes at exercise 2 with the earlier
set; `completed_at` stays NULL.
Repro B: continue to the summary ("Активность просмотрена", "Записано 2 из 5 · пропущено: 3") and tap
"Завершить сеанс".
Expected: `completed_at` set, one completion record, the plan card showing a finished workout.
Actual: the screen returns to Train and the card still offers "Начать тренировку"; after a **cold
restart** Today still reads "В процессе" and offers "Продолжить тренировку". In the database the app
added a second `workout_session_logs` row (started 04:00, 0 s) and a second `workout_logs` row (20 %
completion, no `planned_session_id`) and left **both** session rows with `completed_at` NULL. The two
stores therefore disagree in three ways at once.
Recovery used for this audit (recorded in §2): `completed_at` was set on both session rows directly in
the QA database. After a cold restart the Today card then reads "Тренировка завершена / Завершено
сегодня / Открыть тренировку" (`shots/221`), which proves the UI depends on that column and the app
never wrote it.
Residual mismatch (D31): Today reports "1 мин" while `confirmed_active_seconds` is 548 (9 min 8 s).
Evidence: `shots/169`, `shots/170`, `shots/172`, `shots/215`, `shots/218`, `shots/219`, `shots/221`.

### D27 — P2 — Opening the macro editor collapses the card it belongs to
**Reproduced in the running simulator.**
Repro: You → Питание → tap "Дополнительные настройки".
Expected: the macro editor opens, or the row expands.
Actual: the "Целевые макро" card shrinks to a narrow sliver that renders three clipped glyphs,
the button disappears, and nothing else opens (`shots/180` before, `shots/185` after). The settings
above it stay intact, so the effect reads as a rendering failure rather than a state change.
Evidence: `shots/180`, `shots/185`.

### D28 — P2 — Russian labels break mid-word and columns collide
**Reproduced in the running simulator.**
Repro: You → Питание (diet cards), then Today after saving a check-in (score cards), then You (vault
card).
Actual: "Вегетарианец" wraps as "Вегетариане / ц" inside a half-width card; "Восстановление" wraps as
"Восстановлени / е"; "Мышленная болезненность" breaks over four lines in its tile; the "Целевые макро"
header is squeezed into a two-line column that overlaps the neighbouring button. All are single-line
labels given less width than their longest word needs.
Evidence: `shots/180`, `shots/184`, `shots/198`, `shots/176`.

### D29 — P2 — English product values inside the Russian interface
**Reproduced in the running simulator.**
Repro: You → Питание; Today after saving a check-in.
Actual: "Цель питания: Maintain Health" and readiness copy "Prioritize gentle movement" are shown in
English next to Russian labels. This is the same class as D2 but on two screens that pass did not
cover.
Evidence: `shots/180`, `shots/198`.

### D30 — P3 — Back from workout availability lands on the Train tab, not Profile
**Reproduced in the running simulator.**
Repro: You → Доступность для тренировок → tap the back arrow.
Expected: return to You.
Actual: the app switches to the Тренировки tab (`shots/179` is the page, `shots/181` is the result).
Evidence: `shots/179`, `shots/181`.

### D31 — P3 — Session duration disagrees between Today and the server
**Reproduced in the running simulator, plus database evidence.**
Repro: start a session, leave it after ~1 minute of interaction, return to Today.
Actual: Today's card reads "Продолжительность 1 мин", while `workout_session_logs.confirmed_active_seconds`
is 548 for the same session. One of the two is wrong, and a user cannot tell which number to trust.
Evidence: `shots/175`; database query above.

### D32 — P2 — The Train card never shows that today's workout was completed
**Reproduced in the running simulator.**
Repro: finish a session, cold restart, then compare Today with Train.
Expected: both surfaces agree that today's session is done.
Actual: Today reads "Тренировка завершена" with a disabled-looking "Открыть тренировку", while Train
still renders the plain "Сегодня · силовая" card with a live primary "Начать тренировку" and no
completion marker (`shots/221` vs `shots/222`). Nothing on Train tells the user the session already
happened.
Evidence: `shots/221`, `shots/222`.

### D33 — P2 — The food catalog only answers English queries and labels its own rows as on-device products
**Reproduced in the running simulator.**
Repro: Nutrition → Добавить прием пищи → Добавить приём пищи → Добавить еду → search "курица", then
"chicken".
Expected: the seeded catalog matches Russian food names, and a catalog result is presented as catalog
data.
Actual: "курица" → "Нет совпадений. Попробуйте другое имя или создайте пользовательское блюдо."
(`shots/242`); "chicken" → two rows named "Chicken breast, raw" and "Ground chicken, raw"
(`shots/243`). Both rows are then described as "Продукт, сохранённый на этом устройстве", and the portion
sheet's header and macro line are English: "Portion", "1 serving: 143 kcal · P 17,4g · C 16,0g · F 8,1g"
(`shots/244`). A Russian-speaking user cannot find any food by its Russian name, and a shared-catalog item
is misreported as a local custom product.
Evidence: `shots/242`, `shots/243`, `shots/244`.

### D34 — P2 — The cycle phase detail page cannot be opened: its command is never bound
**Identified from code, corroborated live.**
Repro: Cycle → with a known phase showing ("Менструальная фаза / День 1 из 28") tap the phase card's
title, then the phase tile. Both taps do nothing (`shots/271`, `shots/272`).
Expected: the card opens `CyclePhaseDetailPage`.
Actual: nothing navigates. The page is registered (`AppShell.xaml.cs:53`) and the view model receives a
navigation callback (`MauiProgram.cs:187` → `CycleViewModel.OpenPhaseDetailsCommand`), but no XAML binds
that command: `grep -rn "OpenPhaseDetailsCommand" src --include=*.xaml` returns nothing, and
`CyclePage.xaml` has exactly one `Command=` binding (`OpenCycleSettingsCommand`). The same is true of
the phase summary sheet, the cycle warning popup and the date sheet: their controls exist in the page
but their visibility flags are never set by a bound control.
Evidence: `shots/269`, `shots/271`, `shots/272`; grep output above.

### D35 — P2 — "Следующая фаза" silently changes the stored phase, and the day counter never follows
**Reproduced in the running simulator, with database evidence.**
Repro: Cycle → with a phase shown, tap "Следующая фаза".
Expected: a preview of the next phase, or an explicit change the user confirms.
Actual: the phase name and icon switch (`Фолликулярная фаза`) but the subtitle stays "День 1 из 28" for
every phase, and the tap writes state: `app.cycle_phase_logs` gains a row with note "Manual phase
advance" and `app.user_profiles.current_cycle_phase` becomes `Follicular`. After the cycle logs were
removed the profile kept the phase, producing "Фолликулярная фаза / Даты менструации не записаны"
(`shots/273`) — a phase with no period data. The stored `cycle_logs.cycle_length` was written as 0 while
the profile says 28.
Evidence: `shots/269`, `shots/270`, `shots/273`; `select * from app.cycle_phase_logs`.

### D36 — P3 — The quick-log chip states are indistinguishable
**Reproduced in the running simulator.**
Repro: Cycle → quick log.
Actual: one chip shows a filled droplet, the other a checkmark, and both use the same border and weight,
so the selected state cannot be read from the screen; the first Save attempt was rejected with "Выберите
отметку цикла…" even though a chip looked active. Flow-intensity labels and several symptom tiles are
also clipped ("Обиль / ные", "Боль в", "Мажущие").
Evidence: `shots/231`, `shots/265`, `shots/266`.

### D37 — P3 — A custom food can be saved with calories but no macros
**Reproduced in the running simulator.**
Repro: Food search → Своё → enter a name and calories → save.
Actual: the row is created (`app.user_food_items`) with 99 kcal and no protein/carb/fat, because the
"Углеводы (г)" and "Жиры (г)" inputs are covered by the primary button and unreachable without
scrolling. Nothing warns that the macros are missing.
Evidence: `shots/259-custom-filled.png`, `shots/260-custom-save-result.png`.

## 6. Checks actually run

Client build:

    dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios \
      -p:RuntimeIdentifier=iossimulator-arm64
    Build succeeded. 0 Warning(s) 0 Error(s)

Core suite:

    dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Debug
    Failed!  - Failed:     1, Passed:  1377, Skipped:     0, Total:  1378
      PlanningReleaseApprovalTests.Approved_reference_is_the_policy_version_recorded_in_the_owner_decision

Registration suite:

    dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Debug
    Failed!  - Failed:     1, Passed:    59, Skipped:     0, Total:    60
      PersonalInfoNoFabricationTests.Save_never_writes_a_placeholder_birth_date_experience_or_cycle_length

Mobile contracts suite:

    dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Debug
    Passed!  - Failed:     0, Passed:   183, Skipped:     0, Total:   183

Server suite:

    dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Debug
    Failed!  - Failed:     1, Passed:   119, Skipped:     0, Total:   120
      ServerRouteParityTests.EveryMappedRouteHasMeasuredColdAndWarmP95WithinBudget
      (MC_LATENCY_POSTGRES is required for the all-route latency gate)

Style gates (the repo's own UI guards):

    dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Debug \
      --filter "FullyQualifiedName~UiStyleGateTests|FullyQualifiedName~UiContractGuardTests|FullyQualifiedName~UiCleanliness"
    Passed!  - Failed:     0, Passed:    43, Skipped:     0, Total:    43

These 43 tests pass on the same commit where every styling inconsistency in §9 is present, which is
why §9 separates gate coverage from composition coverage.

## 7. Blocked and untested

| Area | Why |
| --- | --- |
| Quiz (all 8 onboarding question screens) — **BLOCKED** | Every permitted lever was pulled. DB state set: `app.user_profiles.date_of_birth = '1990-01-01'` for user `01a1d192-c5a3-7811-a41b-2271bae14438` (verified by SELECT), quiz rows left at 17. In the app: You → Личная информация → "Сохранить личную информацию" saved and returned to You (`shots/226`), a cold restart landed on Today (`shots/227`), and clearing the 17 quiz rows earlier also landed on Today (`shots/174`, `shots/175`). Code: the only navigation to `//QuizPage` is the callback wired into `ProfileSetupViewModel` (`MauiProgram.cs:159-161`), which is instantiated only inside the registration flow; the signed-in profile page uses a different view model. With no second account in the database (`app.user_profiles` has one row) and in-app registration blocked by D1, no Quiz screen can be reached, so the eight screens are recorded as **BLOCKED**, not untested. |
| Session completion | **Reached this pass.** All five exercises were walked to the summary ("Активность просмотрена", "Записано 2 из 5 · пропущено: 3"), and "Завершить сеанс" was tapped (`shots/215`). What is missing is not reachability but persistence: the app never writes `completed_at`, so no terminal state exists without the database repair recorded in §2 (D26) |
| Check-in weight field | The sheet was completed and saved (row 27). The optional weight input was not exercised: it sits below the pain options and no label exposes it, so it was not targeted without risking an unintended field |
| Food diary: catalog search, meal templates, barcode and Своё tabs | **All run this pass.** Catalog queries ("курица" → no matches, "chicken" → two rows), a result added to a meal, the meal cancelled, the template list expanded (D33), the barcode tab opened, a numeric barcode submitted (honest "not found" + manual-add offer), and the Своё form filled and saved (D37). Only "Сканировать камерой" is unexercised: the simulator has no camera, which is a hardware limit, not a data gap |
| You subpage internals | All five remaining routes (nutrition settings, workout availability, privacy, feedback, photo vault) were opened this pass. Inside them, unexercised: the nutrition goal picker, "Сохранить настройки питания" (would persist a dietary change), the vault's "+ Добавить фото прогресса" (requests Photos access and writes a file), and the macro editor, which cannot open at all (D27) |
| Cycle quick log, phase and phase detail | **Quick log and save now run** (rows 76-80, 89-96): chips, flow intensity, symptom toggle, dismiss, then save and "Отметить начало менструации", which produced a real phase ("Менструальная фаза / День 1 из 28") and exercised "Следующая фаза" (D35). `CyclePhaseDetailPage`, the phase summary sheet, the cycle warning popup and the date sheet remain **BLOCKED, but no longer for lack of a phase**: with a phase on screen, taps on the card and the tile do nothing, and the code shows why — `OpenPhaseDetailsCommand` has no XAML binding (D34, row 97). This cannot be unlocked by more test data |
| Password recovery | **BLOCKED on an approval rather than on data (row 98).** It is reachable only from a signed-out Login screen, and submitting an address sends a real reset email — an external consequence the audit was not authorised to cause. Ask the owner before running it; nothing about it waits on missing test data |
| iPhone SE and iPad layouts | Not run; time was spent on the onboarding blocker. The project targets iOS phones only |
| VoiceOver focus order, announcements, sheet isolation, Reduce Motion | Not driven. Requires a manual accessibility pass; only label presence was observed through the accessibility tree |
| Keyboard-navigation (Tab/focus) | Not applicable in the iOS-native sense; no external keyboard pass was run |
| Contrast measurements for every token pair | Only two composed cases were judged visually (light eyebrow, dark cards); no ratio was measured |
| Deployed/Lambda environment | Out of scope: the audit used a loopback API and a disposable database only |
| 163 of the 261 app buttons, and the ~80 tap-gesture controls | 80 controls are now tabulated in §10 with their observed result. The remainder live on the Quiz (unreachable, above), the onboarding flow, the session's swap/pain sub-options that were deliberately not committed, and the Cycle quick-log sheets |
| Выйти, Удалить аккаунт и данные, Открыть черновик письма | Not exercised by choice: destructive or externally consequential, and the audit was not authorised for either |

No destructive action, payment, external message or production change was performed. The only data
created were synthetic accounts in a disposable local database.

## 8. Recommendations, in order

1. **Fix the session's two P1s (D25, D26) before shipping anything that logs a workout.** Accept a
   recorded set whose weight is the documented bodyweight value, keep the fields visible while an error
   is shown, and make `Прервать активность` actually close the session in both stores — then add a
   simulator test that starts, finishes and aborts a session.
2. **Fix D1 before anything else.** Make the birth-date field open its picker and make
   `ProfileSetupConfirmBirthDateButton` reachable and effective; add a UI-level regression test that
   walks onboarding to the Quiz. Until this is fixed no new user can register.
3. **Close the localization gap (D2, D11, D15).** Route every user-facing string that the client
   shows through the catalog, including activity names, day abbreviations, units, "Rest day" and
   Core validation messages; add a guard that fails when a known English literal reaches a Russian
   screen.
4. **Repair the Cycle and large-text layouts (D3/D4, S21, S22).** Give the empty state a real width,
   allow the primary button to wrap, and cap the title scale while letting the tab bar scale too.
5. **Restore the shared header on Nutrition (S10) and the per-page header contract (S11, S12).** The
   Nutrition root should use `PageHeader` like every other tab, with its title outside the hero card.
6. **Unify the badge and heading roles (S8, S9, S13, S14, S16).** One status recipe per screen, one
   casing rule for section titles, and no Latin punctuation in Russian copy; move the casing and the
   eyebrow role out of localized strings and into the style system.
7. **Restore the check-in path (D5, D19, S24).** State the unmet prerequisite next to the disabled
   action, keep the footer inside the safe area, and verify that saving produces a check-in POST.
8. **Green the suites (D16, D17).** Preserve `cycleLength` on personal-info save; either restore the
   missing policy document or move the expectation to a file that exists.
9. **Align plan acceptance with the contract (D9, D10).** Auto-apply a feasible first plan, state the
   consequence before replacing an active one, clear the preview after acceptance, and reconcile
   the 45 min / 23 min duration.
10. **Close the keyboard gap (D6).** Pin the OTP action above the keyboard. The planned-session 404 is
   closed as a non-defect (D18), but the session screen still needs to be rendered and logged once.
11. **Make the style gates see composition (S1–S9, S17).** Assert per-page dominant action, card role,
    header usage and badge geometry, and compare the documented card/button geometry with
    `Styles.xaml` so the drift in S1–S3 cannot return.

## 9. Styling inconsistencies (S1–S26)

Scope: shared style layer, all 24 page and 15 control XAML files, and the screenshots captured in
§2. Items are numbered `S…` so they cannot collide with the defect IDs `D1–D19`; S22–S24 are the
styling face of existing defects and are cross-referenced instead of duplicated. S25–S26 are
purpose-level items exercised in a later simulator pass; their labels state which leg was observed
on screen and which is source-only.

### What the repo's style gates cover — and what they miss

The gates pass on this commit: 43 tests across `UiStyleGateTests`, `UiContractGuardTests` and
`UiCleanlinessAndReachabilityTests` (`dotnet test tests/MuscleCuties.Core.Tests --filter
"FullyQualifiedName~UiStyleGateTests|FullyQualifiedName~UiContractGuardTests|FullyQualifiedName~UiCleanliness"`).

Covered: token-only dimensions in page/control XAML; duplicate style definitions; raw palette values
as definition-only; the two-card budget; the documented inline-colour exception list; declared
contrast pairs; catalog copy resolution; the Nutrition presentation seam.

Missed — every item below passes the gates:

- **Composition.** Nothing asserts which card or button a screen picks, how many status recipes sit
  on one screen, or which page uses the shared header.
- **Copy-borne styling.** Heading casing lives in the RU catalog, so the style cannot normalise it.
- **Page-local markup.** Badge geometry and the Nutrition header are authored per call site.
- **Documented values.** `DESIGN.md` geometry and type values are prose, never compared with
  `Styles.xaml`.
- **Adaptation.** Unbounded title scaling and footer/safe-area overlap are not asserted.

Root causes: tokens are owned but composition is not; visual decisions live outside the style
system; the design document is not executable; and no per-page header/status/action contract is
enforced even though the shared primitives exist.

### A. Documented design vs implemented values

| ID | Inconsistency | Evidence | Priority | Seen |
| --- | --- | --- | --- | --- |
| S1 | `DESIGN.md` documents CardPrimary 18 pt inset / 16 radius and CardSecondary 14 / 14. Both ship identically at `UiInsets19` and radius 16; only tone and shadow differ. | `src/MuscleCuties.UI/Resources/Styles/Styles.xaml:552-601`; `DESIGN.md` §Layout | P2 | Source only |
| S2 | Buttons are documented to use `UiRadiusControl` (14) but use `UiRadiusButton` = 12. | `Resources/Styles/Colors.xaml:265-266`; `Styles.xaml:920` | P3 | Source only |
| S3 | The type table gives button text 15 bold; both button styles set `UiFontSize14` = 14. | `Styles.xaml:917, 955` | P3 | Source only |

### B. Button hierarchy and geometry

| ID | Inconsistency | Evidence | Priority | Seen |
| --- | --- | --- | --- | --- |
| S4 | Secondary is the app's default weight: 188 `ButtonSecondary` against 65 `ButtonPrimary` across pages and controls. | grep over `Pages/**` + `Controls/**` | P2 | Source only (count); visible effect in `shots/44-nutrition.png` |
| S5 | `ButtonPrimary` declares no `Padding`/`HeightRequest`; `ButtonSecondary` sets `Padding 12,0` and `HeightRequest -1`, so the two have different intrinsic sizing in the same row. | `Styles.xaml:911-948` vs `950-980` | P2 | Source only |
| S6 | Nutrition places primary and secondary in equal star columns with `WordWrap`, so "Добавить прием пищи" wraps to two lines while every other screen's primary is single-line full width — it reads as a different button family. | `Pages/Nutrition/NutritionPage.xaml:35-49`; `shots/44-nutrition.png`, `shots/70-nutrition-light.png` | P2 | Running app |
| S7 | The owner of the dominant action varies: list/overview screens carry zero primaries in page XAML (Profile 0/2, WorkoutHistory 0/3, PhotoJourney 0/6) while feature controls do carry them. | per-page style counts | P3 | Source only |

### C. Status and badge language

| ID | Inconsistency | Evidence | Priority | Seen |
| --- | --- | --- | --- | --- |
| S8 | Two badge geometries on the same Today screen: `PillBadge` is a true pill (`CornerRadius 999`) for "Нужна отметка", while the rest-day badge is `UiRoundRectangleRadius12`. | `Styles.xaml:621-635`; `Controls/Dashboard/DashboardWorkoutCard.xaml:22-34`; `shots/41-today.png` | P2 | Running app |
| S9 | One pill container carries three semantics: state ("ДЕНЬ ОТДЫХА", "Нужна отметка"), step ("Шаг 1 из 2"), count ("Фото: 0"). | `Pages/Profile/ProfilePage.xaml:68`; `Pages/Onboarding/ProfileSetupPage.xaml:18`; `shots/41-today.png`, `shots/45-profile.png`, `shots/16-profilesetup-empty.png` | P3 | Running app + source |

### D. Page headers

| ID | Inconsistency | Evidence | Priority | Seen |
| --- | --- | --- | --- | --- |
| S10 | `NutritionPage` is the only tab root without `PageHeader`; its title sits inside a rounded hero card, so its top edge is a card while Today/Cycle/Train/You start with the shared title block. | `Pages/Nutrition/NutritionPage.xaml:19-33`; `shots/44-nutrition.png` | P1 | Running app |
| S11 | The You tab renders a stray icon above the title, inside the top safe area; no other tab has a control there. | `shots/45-profile.png` | P2 | Running app |
| S12 | Header contextual actions differ in kind and slot per tab (bell / gear / heart-and-pulse / none). The component is shared, the placement contract is not. | `Pages/Dashboard/DashboardPage.xaml`, `Pages/Cycle/CyclePage.xaml`, `Pages/Workout/WorkoutPage.xaml`; `shots/41`, `42`, `43` | P3 | Running app |

### E. Section headings and typography

| ID | Inconsistency | Evidence | Priority | Seen |
| --- | --- | --- | --- | --- |
| S13 | The same `SectionTitle` style renders ALL CAPS on Cycle tips and You, but Title Case elsewhere, because casing is baked into copy. | `Services/Localization/RussianUiText.CycleWorkout.cs:206-215`; `RussianUiText.NutritionProfileAuth.cs:593-601`; `shots/42-cycle.png`, `shots/45-profile.png` | P2 | Running app |
| S14 | A Latin "&" appears in a Russian heading ("ЗДОРОВЬЕ & ТРЕНИРОВКИ") where every other conjunction is "и". | `RussianUiText.NutritionProfileAuth.cs:593` | P2 | Running app |
| S15 | Page-title format is not one language: greeting ("Добрый день, QA"), lower-case month ("ноябрь 2026"), phrase ("План на эту неделю"), section name ("Питание"), pronoun ("Вы"). | `shots/41`–`45` | P3 | Running app |
| S16 | Nutrition shows the same word twice as eyebrow and title; `EyebrowLabel` is used on only 4 surfaces app-wide, so the eyebrow role is not a system. | `shots/44-nutrition.png`; grep `EyebrowLabel` | P2 | Running app + source |

### F. Icons

| ID | Inconsistency | Evidence | Priority | Seen |
| --- | --- | --- | --- | --- |
| S17 | Three treatments for the same "entity marker" role: a large tinted rounded tile on Today (`DecorativeIconSurface`, used on exactly one page), bare glyphs on Nutrition, small line icons in Profile rows. | `DecorativeIconSurface` count = 1 (`Pages/Dashboard/DashboardPage.xaml`); `shots/41`, `44`, `45` | P2 | Running app + source |

### G. Empty states

| ID | Inconsistency | Evidence | Priority | Seen |
| --- | --- | --- | --- | --- |
| S18 | A shared empty-state pair exists and is used by Nutrition, Train, History, Quiz and Injury, but Today and Cycle hand-roll theirs, producing different anatomy and, on Cycle, a collapsed block. | `Styles.xaml` state pair; `StateEmptyTitle` usage list; `shots/41-today.png`, `42-cycle.png`, `43-train.png` | P2 | Running app |

### H. Surfaces, contrast, scale

| ID | Inconsistency | Evidence | Priority | Seen |
| --- | --- | --- | --- | --- |
| S19 | The avatar's inner circle uses the `SurfaceContrast*` role, producing the only near-black disc in an app built on warm rose surfaces. | `Pages/Profile/ProfilePage.xaml:37-43`; `shots/45-profile.png` | P3 | Running app |
| S20 | Light appearance: the Nutrition hero eyebrow is pale rose on pale rose and effectively unreadable. | `shots/70-nutrition-light.png` | P2 | Running app |
| S21 | At Accessibility XXXL the title scale is unbounded (the greeting fills the screen) while tab-bar labels do not scale at all — two scaling policies in one frame. | `shots/72-relaunch-xxxl.png` | P1 | Running app |

### I. Styling face of existing defects (cross-referenced)

| ID | Inconsistency | Existing defect | Evidence | Priority | Seen |
| --- | --- | --- | --- | --- | --- |
| S22 | Cycle primary action clips mid-word and the empty state collapses to one character per line. | D3 | `shots/42-cycle.png` | P1 | Running app |
| S23 | The onboarding footer overlaps the activity cards, cutting the last card. | D13 | `shots/24-date-open.png` | P3 | Running app |
| S24 | The check-in sheet's footer runs into the bottom screen edge. | D5 | `shots/53-after-checkin.png` | P2 | Running app |

### J. Purpose-level items exercised in the follow-up pass

| ID | Inconsistency | Evidence | Priority | Seen |
| --- | --- | --- | --- | --- |
| S25 | Tappable rows are composed two ways. You draws **inset-grouped** rows — transparent, hairline dividers, chevrons — inside one `CardPrimary`, while the diary and the session archive are authored as one `CardSecondary` per meal or session. Corrected from the earlier description of the You rows as plain borderless rows: the row border is absent, but the rows are grouped inside a card, which reads differently from a card per item. | You rows: `src/MuscleCuties.UI/Controls/Profile/ProfileDetailsContent.xaml:121-170`, `shots/81-s25-you-grouped-rows.png`, `shots/82-s25-you-rows-scrolled.png`. Diary card-per-meal rows: `shots/97-diary-meal-row.png` (Nutrition), `shots/100-foodlog-meal-entry.png`, `shots/102-foodlog-snack-row.png` (diary, "Перекус" card with the entry). Archive template: `Pages/Workout/WorkoutHistoryPage.xaml` | P3 | Running app for the You rows and the diary card-per-meal rows; source only for the session archive (no completed session exists and the session flow is still blocked) |
| S26 | Outcome feedback has no single vehicle: a floating accent toast on the diary and the session archive (`StatusToastSurface`), an inline line on Profile and plan surfaces, and — on the units save — a message destroyed by the page pop, so the destination shows no confirmation at all. The meal sheet's disabled save is explained only by a distant grey line, not beside the control. | Rendered toast: `shots/101-toast-notes-saved.png` ("Заметки сохранены." over the diary, page stays put); toast composition `Pages/Nutrition/FoodLogPage.xaml:968-980`, `Pages/Workout/WorkoutHistoryPage.xaml:447-456`. Lost confirmation: `shots/87-units-saved-inline-status.png`, `ViewModels/Profile/ProfileUnitsDisplayViewModel.cs:82`. Disabled save: `shots/88-meal-search.png` | P3 | Running app for all three vehicles (toast, lost confirmation, disabled save); no source-only leg remains |

Synthetic data note: the diary entry used to render S25's per-item row was created through the app's own
flow (Питание → Добавить приём пищи → Добавить еду → Свое → Кастомная еда, name "QA Apple", 100 kcal,
portion 100 g, no other fields). No direct database insert was used, no real user data was involved,
and nothing outside the disposable `mc_qa_20261112` database was touched.

## 10. Button audit

Scope: every control reachable on this build. The app contains 261 `Button` elements plus roughly 80
tap-gesture controls. This pass reached the active session screen, the check-in sheet end to end, five
pushed Profile routes and the photo vault, so the matrix grew from 20 to 68 rows. A PASS means the
control was tapped in the running app and its effect observed. Nothing is marked PASS from reading a
handler. "Tappable" is recorded separately because several controls are drawn but cannot be targeted.

| # | Screen | Control (label) | Tappable | Expected vs observed | Status | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | All tabs | Tab bar (5 items) | yes | Each navigates; all five did | PASS | `shots/41`–`45` |
| 2 | Today | Заполнить отметку (secondary) | yes | Opens the check-in sheet; opened after one scroll | PASS | `shots/120`, `shots/121` |
| 3 | Today | Открыть день отдыха (secondary) | no | Should open the rest-day detail; visible but absent from the accessibility tree and coordinate taps miss | FAIL | `shots/106`, log `2026-11-12_172658` |
| 4 | Nutrition | Добавить прием пищи | yes | Opens the meal sheet with type and time | PASS | `shots/88` |
| 5 | Nutrition | План на день | yes | Opens the day-plan sheet with suggestions and budget | PASS | `shots/105` |
| 6 | Day plan | Сытный / Сладкий | yes | Swaps the plan; selection visible | PASS | `shots/105`, `shots/115` |
| 7 | Day plan | Используйте прием пищи | yes | Opens a prefilled meal editor (582 kcal) | PASS | `shots/107` |
| 8 | Meal editor | Добавить еду | yes | Opens food search; label missing from the tree, coordinate press needed | PARTIAL | `shots/92` |
| 9 | Meal editor | Получить совет | no | Should request a suggestion; not resolvable by label, not reached | NOT RUN | `shots/92` |
| 10 | Custom food | Сохранить пользовательскую еду | yes | Adds the ingredient; label missing | PARTIAL | `shots/95` |
| 11 | Meal sheet | Приём пищи | yes | Commits the meal; listed in the diary | PASS | `shots/97` |
| 12 | Diary | Сохранить заметки | yes | Saves; toast shown | PASS | `shots/101` |
| 13 | Nutrition | Дневник питания link | yes | Opens the diary from the meal card; the label itself does not route | PARTIAL | `shots/99` |
| 14 | Train | Архив тренировок | yes | Archive opens | PASS | `shots/111` |
| 15 | You | Единицы и отображение | yes | Page opens | PASS | `shots/86` |
| 16 | Units | Сохранить единицы | yes | Saves and pops; destination shows no confirmation | PARTIAL | `shots/87` |
| 17 | You | Личная информация | yes | Page opens | PASS | `shots/112` |
| 18 | Personal info | Сохранить личную информацию | yes | Not exercised: rewrites the seeded profile | NOT RUN | `shots/112` |
| 19 | You | Выйти / Удалить аккаунт и данные | yes | Not exercised: destructive, outside this audit's authority. The sign-out dialog itself was opened and cancelled, session preserved | BLOCKED | `shots/277`, `shots/278`, `shots/279` |
| 20 | Feedback | Открыть черновик письма | yes | Page opens; the action is correctly disabled until text is entered and no mail is created | PASS | `shots/187` |
| 21 | Check-in | Качество сна 1–5 | yes | Selected tile takes the accent state | PASS | `shots/121` |
| 22 | Check-in | Энергия 1–5 | yes | Selected tile takes the accent state | PASS | `shots/121` |
| 23 | Check-in | Болезненность мышц 1–5 | yes | Selects and prints "1 / 5" | PASS | `shots/125`, `shots/197` |
| 24 | Check-in | Боль: Нет / Лёгкая / Умеренная / Сильная | yes | Selection kept and highlighted | PASS | `shots/126`, `shots/197` |
| 25 | Check-in | Вздутие живота: Нет / Да | yes | Selection kept and highlighted | PASS | `shots/197` |
| 26 | Check-in | Сон прошлой ночью (slider) | yes, drag only | Shows "6,2 ч" after a drag; a tap on the track does nothing, so the value stays "—" unless the knob is dragged | PASS | `shots/193`, `shots/194` |
| 27 | Check-in | Сохранить отметку (primary) | yes | Saves once sleep hours are set: sheet closes, Today shows Готовность 69/100 and Восстановление 84/100 | PASS | `shots/197`, `shots/198` |
| 28 | Check-in | Пока пропустить (secondary) | yes | Closes the sheet without recording a skip; Today still shows "Нужна отметка" | PARTIAL | `shots/195` |
| 29 | Check-in | Закрыть (×) | yes | Not exercised separately: the dismiss path was covered by "Пока пропустить" | NOT RUN | `shots/121` |
| 30 | Train | Week cells (MON 09 … SUN 15) | n/a | Not a control: no gesture is bound and a tap produces no navigation | N/A | `shots/132` |
| 31 | Train | Начать тренировку (primary) | yes | Opens the activity chooser for today's strength day | PASS | `shots/135` |
| 32 | Chooser | Strength & Sculpt row | yes | Opens the session preview | PASS | `shots/135`, `shots/137` |
| 33 | Preview | Начать (primary) | yes | Starts the session; exercise 1 of 5 with set logging | PASS | `shots/137`, `shots/138` |
| 34 | Session | Заметки | yes | Opens the note sheet | PASS | `shots/139` |
| 35 | Note sheet | Note field | yes | Typing auto-saves: "Заметка сохранена."; text survives closing and reopening | PASS | `shots/144`, `shots/145` |
| 36 | Note sheet | Сохранить заметку (primary) | yes | Not exercised: autosave fires first and already persisted the note | NOT RUN | `shots/139` |
| 37 | Note sheet | Закрыть (secondary) | yes | Closes and keeps the note | PASS | `shots/144` |
| 38 | Session | Начните 60-секундный отдых | yes | Starts the timer at 01:00 and counts down | PASS | `shots/146`, `shots/147` |
| 39 | Timer | Пауза | yes | Freezes the countdown (00:32) | PASS | `shots/148` |
| 40 | Timer | Продолжить | yes | Resumes counting | PASS | `shots/148` |
| 41 | Timer | +30 сек | yes | Adds 30 s (00:32 → 01:00) | PASS | `shots/149` |
| 42 | Timer | Готово | yes | Dismisses the timer, rest label returns | PASS | `shots/150` |
| 43 | Session | Обменять | yes | Opens the swap panel; correct empty state "нет альтернатив" for this exercise | PASS | `shots/151` |
| 44 | Session | Больно | yes | Opens "Остановите это движение" with three options | PASS | `shots/152` |
| 45 | Pain modal | Заменить упражнение | yes | Not exercised: would rewrite today's prescription | NOT RUN | `shots/152` |
| 46 | Pain modal | Остановить упражнение | yes | Not exercised: would end the exercise mid-audit | NOT RUN | `shots/152` |
| 47 | Pain modal | Отмена | yes | Closes without change | PASS | `shots/153` |
| 48 | Session | Повторы / кг fields | yes | Logging a set with 8 reps auto-advances to exercise 2 and shows "Первая запись: Dead Bug" | PASS | `shots/153` |
| 49 | Session | Пропустить | yes | Advances to the next exercise | PASS | `shots/154` |
| 50 | Session | Далее (primary) | yes | With кг left at 0 it refuses: "Для собственного веса используйте 0 кг", and the banner hides the inputs. Entering a weight first works and logs the set | PARTIAL | `shots/208`, `shots/210`, `shots/211`, `shots/214` |
| 51 | Session | Назад (top-left) | yes | Returns to the preview, which offers "Продолжить"-path "Начать" and "Прервать активность" | PASS | `shots/166` |
| 52 | Session | Назад (second, in the exercise header) | yes | Duplicate back affordance beside "Упражнение N из 5"; ambiguous with the page back | PARTIAL | `shots/138`, `shots/156` |
| 53 | Abort sheet | Продолжить | yes | Cancels the abort and keeps the session | PASS | `shots/168` |
| 54 | Abort sheet | Прервать активность | yes | Should end the session; the sheet closes but the session stays active, reopens at exercise 2, `completed_at` stays NULL (D26) | FAIL | `shots/169`, `shots/170`, `shots/172` |
| 55 | Chooser | Закрыть | yes | Not exercised: the chooser was always completed or abandoned by other paths | NOT RUN | `shots/135` |
| 56 | Error banner | Повторить | no | Retries the failed action; the button has no accessible label and could not be tapped by text | FAIL | `shots/156`, `shots/160` |
| 57 | You | Доступность для тренировок | yes | Page opens and lists the week; its back arrow lands on the Train tab, not Profile (D30) | PASS | `shots/179`, `shots/181` |
| 58 | You | Питание row | yes | Opens "Настройки питания"; row label collides with the tab of the same name, so it is only reachable by position | PARTIAL | `shots/180` |
| 59 | Nutrition settings | Цель питания picker | yes | Not exercised: would change the seeded goal | NOT RUN | `shots/180` |
| 60 | Nutrition settings | Вегетарианец / Веган / Без глютена / Без лактозы | yes | Toggles on and off with an accent border and check | PASS | `shots/184` |
| 61 | Nutrition settings | Дополнительные настройки | yes | Should open the macro editor; the "Целевые макро" card instead collapses into a clipped sliver of glyphs (D27) | FAIL | `shots/185` |
| 62 | Nutrition settings | Сохранить настройки питания (primary) | yes | Not exercised: would persist a dietary change | NOT RUN | `shots/184` |
| 63 | You | Политика конфиденциальности | yes | Renders the data-handling copy | PASS | `shots/186` |
| 64 | You | Отправить отзыв | yes | Opens the feedback form | PASS | `shots/187` |
| 65 | Feedback | Text fields | yes | No send was performed: the draft action stays disabled, so nothing external is created | PASS | `shots/187` |
| 66 | You | Выйти (secondary) | yes | Opens a native confirmation ("Вы уверены, что хотите выйти?"); cancelled with Отмена, session preserved | PASS | `shots/277`, `shots/278`, `shots/279` |
| 67 | You | Удалить аккаунт и данные | yes | Not exercised by choice: destructive | BLOCKED | `shots/189` |
| 68 | Vault | Фотоальбом путешествия icon | yes | Opens the vault; empty state is correct | PASS | `shots/191`, `shots/192` |
| 69 | Vault | + Добавить фото прогресса (secondary) | yes | Not exercised: requests Photos access and writes a file | NOT RUN | `shots/192` |
| 70 | Vault | Фото / reveal toggle on Profile | yes | Toggles the preview; the label "Покажать/Показать" is cut and the surrounding copy wraps to four lines | PARTIAL | `shots/176` |
| 71 | Session summary | Завершить сеанс (secondary) | yes | Should close the session; it returns to Train, adds a second open `workout_session_logs` row and a second 20 % `workout_logs` row, and leaves both `completed_at` NULL (D26) | FAIL | `shots/215`, `shots/218`, `shots/219` |
| 72 | Session summary | Продолжить (primary) | yes | Returns to the exercise list without finishing; used earlier in the flow and behaves as resume | PASS | `shots/215` |
| 73 | Today | Тренировка завершена card | yes | After `completed_at` was set directly, the card reads "Завершено сегодня" with "Открыть тренировку" and no resume path | PASS | `shots/221` |
| 74 | Train | Card after completion | yes | Still shows "Начать тренировку" with no completion state while Today shows the workout as done (D32) | FAIL | `shots/222` |
| 75 | Cycle | Quick-log primary on the phase card | yes | Opens the cycle quick-log sheet | PASS | `shots/230`, `shots/231` |
| 76 | Cycle quick log | Status chips ("Менструация началась / закончилась") | yes | Both chips are drawn but their text runs past the chip edge and is cut ("…закончил") | PARTIAL | `shots/231` |
| 77 | Cycle quick log | Flow intensity ("Обильные") | yes | Tile is too narrow for the word: renders as "Обиль / ные" over two lines | PARTIAL | `shots/231` |
| 78 | Cycle quick log | Symptom toggle (Спазмы) | yes | Toggles on with accent border and check | PASS | `shots/232` |
| 79 | Cycle quick log | Close (×) | yes | Dismisses without writing a record | PASS | `shots/233` |
| 80 | Cycle | Phase card ("Неизвестно фаза") | no | Reads as a card but opens nothing; `CyclePhaseDetailPage` is unreachable while the phase is unknown | PARTIAL | `shots/234` |
| 81 | Cycle | Settings gear | yes | Opens "Личная информация" rather than a cycle-specific settings page; the birth date renders as 15.03.1996 | PASS | `shots/235` |
| 82 | Meal sheet | Использовать шаблон | yes | Expands an inline "Сохранённые шаблоны приёмов пищи" list; empty-state text is cut by the tab bar | PARTIAL | `shots/237` |
| 83 | Meal sheet | Время field | yes | Shows "6:13 PM" — 12-hour English format in the Russian UI | PARTIAL | `shots/236` |
| 84 | Food search | Название продукта + Поиск | yes | Submits the query; "курица" returns "Нет совпадений", "chicken" returns two catalog rows (D33) | PARTIAL | `shots/242`, `shots/243` |
| 85 | Food search | Catalog result row | yes | Opens the portion sheet with quantity and unit | PASS | `shots/244` |
| 86 | Portion sheet | Добавить ингредиент (primary) | yes | Adds 143 kcal / P 17,4 g to the meal and returns to the editor | PASS | `shots/245` |
| 87 | Meal editor | Отмена (secondary) | yes | Cancels the meal; nothing is persisted (Nutrition still shows only the earlier snack) | PASS | `shots/246`, `shots/247` |
| 88 | Quiz | All question screens (8) | n/a | Not reachable: seeded birth date 1990-01-01 + in-app profile save + cold restart all land on Today; see §7 for the exact DB state and evidence | BLOCKED | `shots/226`, `shots/227` |
| 89 | Food search | Штрихкод tab | yes | Opens the barcode pane: explanation, "Сканировать камерой" and a numeric lookup field | PASS | `shots/252-barcode-tab.png`, `shots/256-barcode-tab.png` |
| 90 | Food search | Barcode field + Найти | yes | Submits and answers honestly: "Штрихкод 73500538500193 не найден в каталоге продуктов. Вы можете добавить продукт вручную." | PASS | `shots/254-barcode-lookup.png`, `shots/258-barcode-result.png` |
| 91 | Food search | Сканировать камерой (primary) | yes | Not exercised: it opens the camera scanner and the simulator has no camera. The gap is hardware, not missing test data | NOT RUN | `shots/252-barcode-tab.png` |
| 92 | Food search | Своё tab | yes | Opens the custom-food form (name, brand, serving size/unit, macros, micronutrients) in place of the tab row | PASS | `shots/253-custom-tab.png`, `shots/255-custom-empty.png` |
| 93 | Custom food | Сохранить пользовательскую еду (primary) | yes | Saves the row (confirmed in `app.user_food_items`), but "Углеводы (г)" and "Жиры (г)" sit under the button, so a food can be stored with kcal and no macros (D37) | PARTIAL | `shots/259-custom-filled.png`, `shots/260-custom-save-result.png` |
| 94 | Cycle quick log | Отметить начало менструации (secondary) | yes | Writes the cycle start and the page then reads "Менструальная фаза / День 1 из 28" (`app.cycle_logs` row created) | PASS | `shots/267-cycle-saved.png`, `shots/269-cycle-record-saved.png` |
| 95 | Cycle | Следующая фаза (secondary) | yes | Switches the phase to "Фолликулярная фаза" and **persists** it ("Manual phase advance" in `app.cycle_phase_logs`, `current_cycle_phase` updated) while the day stays "День 1 из 28" for every phase (D35) | PARTIAL | `shots/269`, `shots/270-next-phase.png` |
| 96 | Cycle | Phase card title and phase tile | yes | Both are drawn as tappable cards; taps do nothing and no detail page opens (D34) | FAIL | `shots/271-phase-detail.png`, `shots/272-phase-tile-tap.png` |
| 97 | Cycle | CyclePhaseDetailPage, phase summary sheet, cycle warning popup, date sheet | no | **BLOCKED for a code reason, not for missing data**: `CycleViewModel.OpenPhaseDetailsCommand` is bound in no XAML (`grep -rn "OpenPhaseDetailsCommand" src --include=*.xaml` → no match) and `CyclePage.xaml` holds exactly one `Command=` binding (`OpenCycleSettingsCommand`). The sheet controls exist (`PhaseSummarySheet`, `CycleWarningPopup`) but nothing toggles their visibility flags | BLOCKED | `shots/271`, `shots/272`; grep output in D34 |
| 98 | Auth | Password recovery | yes | **BLOCKED on an approval, not on data**: only reachable from a signed-out Login screen, and submitting an address sends a real reset email. Not attempted; the reason is now "needs the owner's go-ahead", not "no test data" | BLOCKED | — (`PasswordRecoveryPage` route, code only) |

Summary: **98 rows assessed — 57 PASS, 17 PARTIAL, 7 FAIL, 11 NOT RUN, 5 BLOCKED, 1 not a control.**

The failures cluster into three causes:

1. **The session cannot progress or end.** `Далее` is blocked by a weight validation whose own message
   points at the value already displayed (0 for bodyweight), and the banner that explains it hides the
   fields it names (row 50, D25). `Прервать активность` closes the sheet but leaves the session open,
   resumable at an earlier exercise, with `completed_at` still NULL in the database (row 54, D26).
2. **Drawn but not reachable.** "Открыть день отдыха" and the banner's "Повторить" are absent from the
   accessibility tree (rows 3, 56, D23).
3. **Layout eats content.** The macro card collapses to a sliver (row 61, D27), and three Russian labels
   break mid-word (row 70, D28).

The check-in verdict changed this pass: with the sleep slider dragged to a real value, Save enables and
records the check-in (readiness 69, recovery 84), so the earlier "cannot be saved" finding is retired.
What remains is that the sheet never says the sleep hours are required (D19).

## 11. Limitations of this report

- Eighteen of the 24 pages were exercised end to end or in their primary state. The rest are listed
  with their reason in §7; none is claimed as passed.
- Never rendered, and therefore not passed: the **Quiz** (a valid seeded birth date plus the app's own
  profile save still lands on Today — §7), and the Cycle quick-log sheets. The session's finish summary
  *was* rendered this pass; its persistence is what fails (D26).
- The session evidence was produced on a plan whose days were shifted four days earlier, because the
  simulator clock cannot be changed without administrator rights. The shift is recorded in §2; the
  defects it exposed (D25, D26, D31) are independent of it.
- S25/S26 are now primarily observed: the You rows, the diary's card-per-meal row and the toast were
  rendered on the simulator. The only remaining source-only leg is the session archive row, because
  no completed session exists and the session flow is still blocked; §9 labels it that way.
- The catalog food search was opened but no query was submitted, so whether the seeded catalog can
  return foods stays untested; the custom-food path was used instead to produce the diary entry.
- Two defects are stated as code-plus-test evidence only (D16, D17); everything else in §5 was
  reproduced on the running simulator.
- Visual judgements are per-screen observations from the simulator, not measured contrast or
  layout audits. VoiceOver, Reduce Motion, small-device and tablet behaviour were not verified.
- S1–S5, S7 are source-level observations: the values and counts were read from `Styles.xaml`,
  `Colors.xaml` and per-page style usage, not measured on screen. Every other S-item was observed on
  the simulator.
- No product source, XAML, contract or test file was modified for this report. The only changed
  files in the worktree are this report and `STATUS.md`.
