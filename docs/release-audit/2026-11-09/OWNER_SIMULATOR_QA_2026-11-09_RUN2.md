# Owner simulator QA — run 2 (Rider-attached simulator) — 2026-11-09

| Field | Value |
| --- | --- |
| Tree tested | `product/qa-fixes-20261109` @ `1a4ca7dd`. This is the base `f1918e1` plus the QA fixes in `docs/review_owner/list_of_changes.md`, **not** `f1918e1` alone. |
| Build/launch | The owner ran Rider's native `MuscleCuties.UI` iOS configuration from the worktree solution. The agent's `.run` shell configuration failed (see F-17). The installed binary (16:31) contains the new `ReportPain` code. |
| Device | iPhone 17 simulator `13EDBFDC-534D-4A9E-80CF-C5D2F26D5DBB`, iOS 26.3, light, English, default text size |
| Backend | Local API `http://127.0.0.1:5080` (Development, per-run keys); disposable PostgreSQL 16.15 `127.0.0.1:55461/mc_qa_20261109`, schema 7, dbcheck compatible |
| Account | Synthetic `qa.sim1109a@example.test` / `qasimtester`; OTP from the development log |
| Driver | Claude Code iOS simulator control (tap/type/screenshot), `xcrun simctl io` captures, Maestro for the accessibility tree |
| Screenshots | [screenshots-qa-2026-11-09/](screenshots-qa-2026-11-09/) |

## Results so far (light, EN, iPhone 17)

| ID | Result | Observation | Evidence |
| --- | --- | --- | --- |
| E-03 | PASS | Username stays "qasimtester" (lowercase); the same input method produced "Qabeta1" in the prior defect run | `E03-username-lowercase.png` |
| E-04 | PASS | "Please acknowledge the wellness notice to continue." sits directly above Continue without scrolling | `E04-after-continue-untouched.png` |
| E-01 | **FAIL (P0)** | With the birthday untouched, Continue proceeds to the quiz; DB `date_of_birth = 2008-11-09` (today−18y). Height 165 / weight 65 view-model defaults also saved untouched | `E01-continue-birthday-required.png`; `app.user_profiles` row |
| J02 | PASS | One weekday → "Choose 2 to 7 days when you are available to train." above Next; Next blocked | `J02-single-weekday.png` |
| J01 | PASS (with E-01 defect) | Registration → OTP → profile → 11-question quiz (pace skipped for the tone goal) → Today. Profile saved Mon/Thu, Beginner, Follicular, GeneralStrength | `J01-02-after-finish.png` |
| E-05 | PASS | Today "This week · Follicular"; Train "Follicular phase"; quiz answer Follicular | `J01-03-today-light-en.png`, `J03-train-plan-light-en.png` |
| E-06 | PASS | Beginner general strength day shows "Intensity Moderate" on Today and Train | same |
| G05 | PASS (week 1) | Schedule Mon 09 / Thu 12 only, "2 training days - 5 full rest days", ≤1 activity per day | `J03-train-plan-light-en.png` |
| J03 | PASS | Activity "Strength & Sculpt", 6 exercises (leg curl, dead bug, chest-supported row, machine chest press, hack squat, hip thrust) | `J03-activity-intro.png` |
| E-07 | PASS (sampled) | Frames sampled while the chooser and session opened show no "no activities" state | `E07-session-open-*.png` |
| E-02 | PASS | Session shows "Start light: pick a weight…" (no raw key) | `J05-01-after-start.png` |
| J05 / G10 | **FAIL (P1)** | Set 1 saved correctly (20 kg × 10, RIR 2, observed). The DB also holds **set 2 = 0 kg × 10, `is_observed = true`**, which the user never logged; only one row was on screen | `exercise_set_logs` rows |
| C-9 (S-04) | PASS (function) / FAIL (copy, layout) | The "This hurts" card renders; Switch opens the swap panel; Stop advances and marks the exercise skipped; no set rows written for it. But the empty swap panel says "You can keep it or skip it" in the pain flow, the card squeezes the exercise off screen, and the first tap was ignored while the celebration banner was up | `C09-*.png` |
| CR01 | **FAIL (P0)** | 2nd cycle: Back → Resume → chooser → intro shows Start/Abandon → Start → "Could not save this exercise… Try again", back at Exercise 1 of 6, inputs missing, "Previous: No previous log". API: `PUT /api/v1/workouts/optional-exercise` → 400 | `CR01-resume-start.png`, API log |
| A11Y | **FAIL (P0)** | After the session sheet was used, the Train tab exposes **no page content** to accessibility (Maestro tree shows only the tab bar) while the page is visible. The Today tab tree is complete | Maestro `inspect_screen` output |

## Results continued (dark, RU, largest Dynamic Type, offline, restart)

| ID | Result | Observation | Evidence |
| --- | --- | --- | --- |
| J10 | PASS | Cycle shows Follicular day 8/28 with predictions; Quick Log → Cramps → Save → `PUT /cycle/history/symptoms 200` | `J10-*.png` |
| J11 | PARTIAL | Nutrition renders 0/2010 kcal, macros and an empty state; Add meal → Log meal → Add food → search "banana" gives a proper no-match state (local catalog empty). Meal persistence not verified | `J11-*.png` |
| J12 | PASS (after config) | You first failed: the whole page errored because photo vault needs `Storage:S3:BucketName` (500). With a dummy local bucket the page loads; Personal info weight 65→62 → `PUT /profile 200`, DB weight 62 | `J12-*.png` |
| J13 | PASS | API stopped → Today "You're offline. Reconnect and try again." + Retry (no silent fallback); API back → Retry recovers, session kept | `J13-*.png` |
| G13 | PASS (restart) | Rider Stop-and-Rerun: still signed in, in-progress session and logged data persist | `RU-after-restart-dark.png` |
| E-06 (in progress) | **FAIL (P1)** | Once a session is in progress, the Today card shows "Intensity High", "Upcoming" and "In Progress" together, and Start instead of Resume; Train shows Moderate for the same session | `J13-after-retry.png`, `DARK-train-en.png` |
| J09 | **FAIL (P1)** | Workout Archive lists the still-in-progress session under "Past Completed Sessions" (Completed 1, 200 kg, 2 sets incl. the fabricated 0 kg set) | `J09-archive-dark.png` |
| Dark | PASS (Today/Train/Archive) | Role colours hold in dark; no contrast break observed on these screens | `DARK-*.png` |
| U-09 / RU | **FAIL (P1)** | RU Today: "General strength training" (title + chip), "TRAINING", "In Progress", "High", "Keep it moderate" stay English. Train: tag "Сегодня · силовая" localized, but badge "Strength", "45 min" and "General strength training" stay English | `RU-*.png` |
| U-10 | PASS | RU count tile "Подходы" / "Продолжительность" / "Интенсивность" localized | `RU-after-restart-dark.png` |
| J14 | **FAIL (P1)** | RU at accessibility-XXXL: mid-word breaks ("qasimte/ster", "Сег/одн/я"), clipped "Нет зафиксирова…", Save button "Сохрани…" cut. Dynamic Type also does not update live (needs relaunch) | `J14-*.png` |
| J04 | PARTIAL | Injury log opens from the bell; the new-injury form accepts a body area; Save sent no request (required fields below the fold at XXXL). Restriction/substitution behaviour not exercised in UI | `J04-*.png` |
| CR01 ×20, Reduce Motion | NOT RUN | Stopped to keep the remaining session for reporting. CR01 already failed on cycle 2. J15 needs uninstall + Rider rerun. VoiceOver is not drivable by this tool; the Maestro tree finding above stands in for it | — |
| QA01 | DEPRECATED → simulator | Owner instruction: physical-device QA replaced by the Rider-attached simulator | — |

**Counts superseded below (run 2b).** Original run-2a counts: PASS 8 (E-04, E-05, E-07, J01*, J02, J03, J10, J13) · FAIL 6 (E-06, J05, J09, J14, CR01, plus E-01 outside the 21) · PARTIAL 2 (J04, J11) · NOT RUN 4 (J06, J07, J08, J15) · J12 PASS after local config · QA01 deprecated. *J01 completes but carries the E-01 defect.

## Additional findings (run 2)

| ID | Sev | Finding |
| --- | --- | --- |
| F-01 | P1 | Today readiness 65/100 and recovery 70/100, sourced "Apple Health & baseline", before any check-in and with HealthKit off: values presented without user data |
| F-02 | P2 | The wellness acknowledgment checkbox uses iOS system blue; the registration consent checkbox uses the rose role colour. Set-row check icons are also system blue |
| F-03 | P2 | "Please acknowledge…" stays visible after the box is checked, until Continue is pressed again |
| F-04 | P2 | "Order 1 · General strength training" exposes an internal ordinal; the tag reads lowercase "Today · strength" |
| F-05 | P2 | "Notes" appears twice on the session screen (header and action bar) |
| F-06 | P2 | The celebration banner and the C-9 four-row action bar clip the Sets/Reps/kg and logged-set rows on iPhone 17 |
| F-07 | P2 | Train shows "Workout in progress → Resume workout" and "Start workout" together: two primary CTAs for one session |
| F-08 | P3 | The first tap on a freshly shown screen was ignored several times (Create account link, Train tab, Back); unconfirmed whether this is the app or the tool |
| F-09 | P2 | Today's featured card is one accessibility element ("Today's plan: General strength training."), so its Start workout button is not separately exposed |
| F-10 | P1 | The You page fails entirely when the photo-vault S3 setting is missing; one optional dependency should not take down the profile |
| F-11 | P1 | User-facing copy leaks infrastructure: "Stored in first-party PostgreSQL" and "AWS protection settings have not been verified" on the You page |
| F-12 | P2 | Quick Log flow intensity shows a single "Heavy" chip; the tab bar stays visible under shared sheets (Quick Log, Add meal, injury log) |
| F-13 | P2 | No confirmation after a successful Quick Log save |
| F-14 | P2 | "Delete Account and Cloud Data" uses the same secondary style as Log Out; the destructive action is not distinguished |
| F-15 | P2 | Resume from the activity sheet returns to the intro (Start / Abandon activity) instead of the current exercise |
| F-16 | P1 | Nutrition targets (2010 kcal) are derived from the fabricated defaults (DOB today−18y, 165 cm, 65 kg) when onboarding is left untouched |
| F-17 | P2 | Agent-added `.run` shell configuration `iOS Simulator - iPhone SE (3rd gen)` failed for the owner; the owner used Rider's native configuration instead |

## Run 2b — J06, J07, J08, J15 (owner request; iPhone SE and VoiceOver removed from scope)

Same device, tree and backend; relaunches via Rider Stop-and-Rerun / Run. The owner removed the iPhone SE and VoiceOver
requirements on 2026-11-09; they are out of scope for this record, not passed.

| ID | Result | Observation | Evidence |
| --- | --- | --- | --- |
| J08 | **FAIL (P0)** | The in-progress session cannot be completed. Today → Start opens a differently ordered workout → `POST /sessions/start` 409 "This workout is no longer in your active plan". Train → Resume → Start → `PUT /optional-exercise` 400 → "Could not save this exercise", back at exercise 1 with no inputs; Try again loops to the intro. **Abandon activity** does nothing (no request, no confirmation, also via Maestro). DB session log keeps `completed_at` empty | `J08-*.png`, API log, `workout_session_logs` |
| J06 | PASS (with notes) | Workout availability Mon/Thu preserved; adding Saturday → Continue → preview "General strength (low volume): 2 strength sessions per week", "Apply plan replaces your current plan. Until then, your current plan stays active."; plan unchanged before Apply (1 revision); native "Replace your current plan?" → Apply → `accept 200`, 2 plans / 1 active. Notes: weekdays saved to the profile before Apply; the confirmation is a native alert, not the shared sheet; the orphaned in-progress session from the old plan is still shown | `J06-*.png`, DB |
| J07 | PASS (schedule) / N/A (reschedule) | After replacement the schedule still shows Mon/Thu only, "2 training days - 5 full rest days". Rescheduling is disabled for Beta by design (`BetaWorkoutScope.ReschedulingEnabled => false`) and the UI hides it. Open question: a future Thursday session can be opened from the chooser today | `J07-*.png`, code |
| J15 | **FAIL (P1)** | After `simctl uninstall` and a fresh Rider install the app opens signed in on Today as the previous user: the session survives uninstall (Keychain/SecureStorage). Also: the notification permission prompt appears immediately on first launch, before any context | `J15-first-run*.png` |
| QA01 / iPhone SE / VoiceOver | Out of scope | Owner instruction | — |

### Final counts (21-row checklist, run 2a + 2b)

PASS 9 (E-04, E-05, E-07, J01*, J02, J03, J06, J10, J13) · FAIL 7 (E-06, J05, J08, J09, J14, J15, CR01) · PARTIAL 3
(J04, J07 reschedule N/A, J11) · PASS after local config 1 (J12) · out of scope 1 (QA01). Outside the 21: E-01 FAIL,
E-02/E-03/U-10/G05/G13 PASS, U-09 FAIL (RU). *J01 completes but carries the E-01 defect.

### Run 2b findings

| ID | Sev | Finding |
| --- | --- | --- |
| F-18 | P0 | Session lifecycle: cannot finish, resume or abandon an in-progress session (see J08); Abandon activity is inert |
| F-19 | P1 | Today's Start for an in-progress session opens a different (reordered) workout context and fails 409 |
| F-20 | P1 | Login session survives uninstall/reinstall (Keychain); first run skips login/onboarding |
| F-21 | P2 | Notification permission requested on first launch without context |
| F-22 | P2 | "Nutrition: Muscle Tone • Steady (+150 kcal)" shows a pace the user was never asked |
| F-23 | P2 | Plan replacement confirmation uses a native alert; availability is saved before Apply |
