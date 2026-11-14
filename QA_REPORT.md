# QA audit — MuscleCuties iOS client (current working tree)

Audit date: 2026-11-13. Read-only audit: no product source, XAML, contract, test or resource file was modified. The only files this pass writes are this report and `STATUS.md`.

## 1. Summary

The app builds and runs, restores a signed-in session from encrypted storage, and renders all five
tab destinations in Russian. Tab switching, opening the Nutrition sheets, the food picker and the
macro breakdown all worked. Nothing crashed reproducibly and the local API answered 127 requests with
200.

The release is **not shippable as it stands**, for one accessibility blocker and a cluster of visual
consistency defects that the current uncommitted unification pass has not closed:

1. **The meal editor and its food picker expose no accessible elements at all.** While either is open,
   the entire app disappears from the iOS accessibility tree. Reproduced three times. VoiceOver cannot
   read or operate the sheet, and neither can UI automation, so food logging is unusable with assistive
   technology. The shared meal card has the same class of defect in miniature: its root
   `SemanticProperties.Description` collapses the card, hiding **Edit meal** and the save-template action.
2. **Nutrition is still the only tab without the shared page header.** It draws a bespoke pink hero
   (`● Питание / Питание / subtitle / heart button`) while every other page composes `PageHeader`
   (title, subtitle, one circular action). The page also carries a dead `Auto` grid row where a header
   would sit.
3. **The two nutrition screens disagree about how to present the same data**: Nutrition uses headerless
   hero + horizontal macro bars; Food Log uses the shared header + circular macro rings. Units and
   number formats disagree too — `32g / 99g` (Latin g), `33г` (Cyrillic г), `32,8 г` and `0,0mcg`
   inside one sheet. This is exactly the "edit meals on the history page" divergence the request asked
   about.
4. **Copy is not localized on the surfaces a user spends the most time in**: `Rest Focus`, `Rest day`,
   `FRI · день отдыха`, `Snacks`, `Protein / Carbs / Fats`, `Fiber`, `Vitamins`, `Planned`, and the
   mixed sentence `Дней полного отдыха: 1 training day - 6`. Two causes are visible in code:
   `MacroProgressGrid` and `MacroBreakdownItem` hold English label arrays as literals, and several meal
   names/descriptions come straight from the server/demo content.
5. **State is restated two to three times per card**, against the "state a fact once" rule: `День отдыха`
   plus a `ДЕНЬ ОТДЫХА` pill plus an inner rest notice; the readiness sentence three times; `День 14 из
   28` on two Cycle cards; three overlapping emptiness sentences per empty meal card.
6. **The check-in sheet's pinned footer is clipped by the screen bottom** — the primary commit
   `Сохранить отметку` is partly off-screen, and it is disabled with no explanation and near-invisible
   contrast.
7. **The Core suite is red**: 1 of 1354 tests fails because a referenced policy file
   (`docs/release-audit/2026-11-05/WORKOUT_POLICY.md`) is not in the checkout.

The UI guard suites are green (24/24), which is the core problem with the current gates: they check
tokens, colours and duplicate definitions, but they cannot see that Nutrition renders a second page
skin, that a sheet's footer is off-screen, or that a sheet is absent from the accessibility tree.

A second pass closed part of the coverage gap: a training day was staged (workout
days shifted by −3 days on the disposable database, backed up), so the workout start path, the
activity chooser, the day overview, the live session screen, set advancement and the notes sheet were
driven; the workout archive, the cycle phase detail page and six Profile subpages were reached through
the app's start-up route and captured in both appearances; and large Dynamic Type was exercised. That
pass raised the defect list from 17 to 28 entries (one earlier observation, D17, was retracted as an
automation artefact) and confirmed the systemic pattern behind most of them: **the page frame is
shared, but every form and sheet still re-implements its own footer, header row and inset**.

A third pass (rows 40–51, §11–§12) exercised the rest timer, the pain and swap panels, the cycle quick
log and next-phase preview, the archive detail, the four missing dark sheets and an iPad build, and it
proved the one thing the earlier passes could not: **finishing a workout is unreachable** (D29) and an
unfinished session is silently dropped on relaunch (D30). The defect list now runs to D38 (D17
retracted). Auth, password recovery and tablet/landscape remain untested or unavailable (§9).

A fourth pass (rows 52–62) closed the last Cycle gap: the month calendar, the day-tap date/phase sheet,
save, cancel, backdrop dismiss, an out-of-order edit, a far-future edit and the post-save propagation
were exercised in both themes. The sheet itself is sound; what fails is what surrounds it — a
contradictory legend (D40), no order validation (D39), future-day editing (D41), no save confirmation
(D42) and two different drawings of the same four phases (D43). The order-conflict popup could not be
reached and stays blocked (§9).

A fifth pass (rows 63–70) went after the modal surfaces nobody had driven: the meal entry sheet, the
Nutrition main-page meal editor and the history meal card's actions. The entry sheet and the editor
behave as their earlier defects describe, and the sheets themselves are structurally consistent; what
this pass settled is that **the history edit path is broken, not merely invisible** (D46) — the
`Изменить приём пищи` control is drawn, takes no tap, and the star beside it does. That closes the
last question about the two edit entry points: one works, one does not, so a logged meal can be created
and deleted but never corrected.

A sixth pass (rows 71–81) closed the three named gaps: the keyboard question, the notes persistence and
the alternate entry point. Two of the three are answered *positively* — the notes field keeps its text
when the keyboard is dismissed and the typed note is stored in `daily_nutrition_logs.notes` and comes back
on reopen (so D47 is narrowed to the false confirmation, not a broken save) — and the third is a dead
end: a deep link has no router, the template route is a silent no-op, and the main page's card has no edit
action, so the history editor stays blocked by D46. The keyboard test itself confirmed a real defect on
the one sheet that has a text field: the food picker never adapts, so results are hidden behind the
keyboard (D51). The same session found the entry sheet cannot scroll and its template action does nothing
(D52), and that its time field rewrites itself every minute (D53).

## 2. Environment and conditions

| Item | Value |
| --- | --- |
| Branch | `chore/repo-hygiene-and-testflight-sync` |
| Commit | `b2e509216c40a7fc394e59de81667b4c5d2dc4c8` plus the uncommitted unification pass present at session start (39 modified files, 3 untracked: `AdaptiveRow.cs`, `MealEditAction.cs`, `ModalTabBar.cs`). The build under test includes those changes. |
| Host | macOS, Xcode 26.3 (17C529), .NET SDK 10.0.203, PostgreSQL 16.15 (Homebrew) |
| Simulator | iPhone 17 Pro, iOS 26.3, UDID `678048F6-C4C7-406C-A970-7104AED3E00A`, phone form factor only |
| App | `com.musclecuties.app`, Debug `net10.0-ios` / `iossimulator-arm64`, version 1.0 build 4 |
| Backend | `scripts/run-local-api.sh` on `http://127.0.0.1:5080`, disposable database `mc_qa_20261112` (loopback only) |
| Account | `qa.audit.seed1117@example.com`; the client restored the existing encrypted session, so sign-in itself was not re-driven |
| Appearance / language | Light and dark appearance; simulator language `ru` |
| UI automation | Maestro 2.12.1 (XCUITest driver) plus `xcrun simctl` for launch, screenshots and appearance; accessibility trees read with `maestro hierarchy`. The driver terminates the app at the end of every flow (`maestro-driver-iosUITests-Runner: Terminating app com.musclecuties.app`), which is why states are set up again per flow; single pages were also entered with the app's own `MUSCLECUTIES_INITIAL_ROUTE` start-up variable |
| Text size | Default `large` and `accessibility-large` (`xcrun simctl ui … content_size`); the setting survives an app relaunch but not a simulator reboot, so each large-text capture was checked against `simctl ui content_size` immediately before it |
| Evidence | `/tmp/mc-qa-2026-11-13/` (`shots/`, `flows/`, `app.log`), local and not committed |

### Test-data changes

- A breakfast entry (`Scrambled Eggs with Toast`, 582 kcal) is present in `mc_qa_20261112` for today.
  It was created during this pass while exercising the meal editor; it is synthetic, on a disposable
  database, and left in place so the meal-card evidence is reproducible.
- To obtain a training day for the workout path, `app.workout_days` was shifted by −3 days after
  copying all 24 rows into `app.qa_backup_workout_days_20261113`. At the end of the pass the dates were
  restored from that table (`UPDATE 24`, then `select count(*) … where d.date is distinct from b.date`
  → `0`). The shift was needed because the QA account's current day was a rest day, and the shift is on
  the disposable QA database only.
- The session started during the pass (`7b8ca7ce-98d7-48ad-b458-3755412ad28e`, one set row) was removed
  afterwards so the client does not open with a phantom in-progress workout: `DELETE 1` set,
  `DELETE 1` session, `open_sessions = 0`. The meal entry from the earlier pass was left in place on
  purpose so the meal-card evidence stays reproducible.
- For the keyboard/persistence pass, `app.daily_nutrition_logs` (2 rows) was copied to
  `app.qa_backup_daily_nutrition_logs_20261113` before typing. The pass wrote the staged note
  `QA note keyboard test 14.11` to 2026-11-13 and then set that column back to the backup value (empty).
  Verified afterwards: rows **2/2**, mismatch **0** across `notes`, `water_intake_ml` and `total_calories`;
  meals **2**, entries **5**, templates **0**.
- For the modal pass, `app.logged_meals` (2 rows) and `app.logged_meal_entries` (5 rows) were copied to
  `app.qa_backup_logged_meals_20261113` and `app.qa_backup_logged_meal_entries_20261113` first. The pass
  wrote nothing to the day, but the star on the history card created one `app.saved_meal_templates` row
  (`Шаблон Завтрак (нояб. 13)`, 19:23 local), which was deleted at the end. Verified afterwards: meals
  **2/2**, entries **5/5**, templates back to **0**, mismatch count **0** in both tables.
- For the Cycle pass, `app.cycle_phase_logs` (2 rows) and `app.cycle_logs` (1 row) were copied to
  `app.qa_backup_cycle_phase_logs_20261113` and `app.qa_backup_cycle_logs_20261113` before any edit. The
  pass wrote three phase records (`2026-11-09`, `2026-11-14`, `2026-11-21`, all `Calendar phase edit`) and
  two derived cycle rows. After the pass those rows were deleted and both tables verified against the
  backups: phase logs back to **2**, cycle logs back to **1**, mismatch count **0** in both.
- No account or profile data was deleted or rewritten. No payment, message, e-mail or external service was
  triggered. The local API only ran against a loopback disposable database.

**Viewport coverage.** Exactly one viewport was available with a signed-in session: the iPhone 17 Pro
simulator at 402 × 874 pt, default text size. No smaller device was prepared, the iPad runs this build in
compatibility mode and stops on the login page (row 50), and the app ignores rotation (row 49). Every
sheet-height, scroll and keyboard result in this report therefore describes that one size; nothing here
certifies a tablet, landscape or compact-device layout, and the responsiveness rows are marked PARTIAL or
BLOCKED rather than passed.

### Tooling findings

- **No browser-control MCP tooling is applicable.** This is a native .NET MAUI iOS client with no web
  front end and no web routes, so nothing can be verified in a browser. Verification used the
  simulator through Maestro's XCUITest driver instead, which drives the real rendered app.
- **Skills present:** `impeccable`, `high-end-visual-design`, `taste-skill` and `wizard` are available
  in this environment. `maestro` is not a skill here; it is the installed CLI (2.12.1) used for the
  simulator runs. Nothing was installed.
- How the design skills were used is stated in §8: `impeccable`'s Operate-mode critique is applicable;
  `high-end-visual-design` and `taste-skill` are written for web/Tailwind surfaces and their directives
  (font substitution, glass, bespoke motion, layout archetypes) would break this repository's hard style
  budget and Open Sans alias, so only their critique lens was applied, never their visual recipes.
  `wizard` is the owner-driven `scripts/qa-simulator-wizard.sh`; it needs a human at the keyboard and was
  not run in this pass.

## 3. Page and modal map

24 page XAML files, 18 sheet hosts (`AnimatedModalView` subclasses), one non-dismissible in-page overlay.

| Area | Surface | Route / registration |
| --- | --- | --- |
| Startup | `AppStartupPage` | ShellContent |
| Auth | `LoginPage` | ShellContent |
| Auth | `RegisterPage`, `PasswordRecoveryPage` | `Routing.RegisterRoute` |
| Auth | `OtpVerificationPage` | ShellContent + route |
| Onboarding | `ProfileSetupPage`, `QuizPage` | ShellContent |
| Today | `DashboardPage` | Tab |
| Cycle | `CyclePage` | Tab |
| Cycle | `CyclePhaseDetailPage` | route |
| Train | `WorkoutPage` | Tab |
| Train | `StrengthPlanGenerationPage`, `WorkoutSessionPage`, `WorkoutHistoryPage` | routes |
| Nutrition | `NutritionPage` | Tab |
| Nutrition | `FoodLogPage` | route |
| You | `ProfilePage` | Tab |
| You | `ProfilePersonalInfoPage`, `ProfileNutritionSettingsPage`, `ProfileWorkoutPreferencesPage`, `ProfileUnitsDisplayPage`, `ProfileFeedbackPage`, `ProfilePrivacyPage`, `PhotoJourneyVaultPage` | routes |
| Sheets | daily check-in, injury log, meal entry, meal editor, meal detail, meal suggestion, nutrition breakdown, cycle quick log, cycle phase summary, cycle date/phase, cycle warning, profile image crop, workout swap, workout activity chooser, workout history detail, strength-plan preview, session notes/pain | `AnimatedModalView` hosts |

Roles: one authenticated end user; no admin surface, no role switch. Access control is a Shell guard
plus bearer-token ownership on the server.

## 4. Coverage matrix

Status: PASS = exercised and behaved; FAIL = exercised, defect reproduced; PARTIAL = exercised, some
states untested; BLOCKED = could not be reached or driven; NOT RUN = not attempted in this pass.
Nothing is marked PASS on code reading alone.

| # | Page / workflow | Preconditions | Steps | Expected | Actual | Status | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Cold launch | Signed-in cache present | Install current build, launch | Today tab, Russian UI | Today rendered in Russian with restored session | PASS | `02-fresh-launch.png` |
| 2 | Tab navigation | Signed in | Tap all five tab items, both directions | Each destination renders | All five rendered; Nutrition needs a second tap after cold start to switch | PARTIAL | `t-cycle.png`, `t-train.png`, `t-nutrition.png`, `t-profile.png` |
| 3 | Today render | Signed in | Open Сегодня | Hierarchy: next action, then status | Rest card + readiness card render; copy duplicated (D7) | FAIL | `dk-today.png` |
| 4 | Check-in sheet open | Today | Tap `Заполнить отметку` | Sheet with sleep, quality, energy | Opens; pinned footer clipped at screen bottom (D5) | FAIL | `checkin-light.png` |
| 5 | Check-in save | Sheet open | Drag sleep, answer, save | Saves and closes | Not driven this pass | NOT RUN | — |
| 6 | Cycle render | Signed in | Open Цикл | Phase hero, day, actions | Rendered; phase colour and `День 14 из 28` mismatch (D8, D9) | FAIL | `dk-cycle.png` |
| 7 | Cycle calendar / quick log / next phase | Cycle | Expand month, open quick log | Calendar grid, log sheet | Not driven | NOT RUN | — |
| 8 | Train render | Signed in | Open Тренировки | Week strip, day card, filters | Rendered; English day labels, broken summary sentence, clipped strip and chips (D4, D6, D11) | FAIL | `dk-train.png` |
| 9 | Workout archive | Signed in with one completed session | Open `Архив тренировок` (entered through the start-up route after the tab path failed) | Totals, month filters, session cards | Rendered with one session; `1800s`, `1 sets`, label split `Продолжительно/сть`, mixed date format, debug-style footer (D26) | FAIL | `rt-WorkoutHistoryPage.png` |
| 10 | Start session | A training day (QA data shifted by −3 days, backed up) | Today/Train card → `Начать тренировку` → `Активности тренировки` → `Strength & Sculpt` → `Начать` | Day overview, then the session screen | Both reached; chooser sheet overlaps its own rows and lingers after selection (D19); in-progress card then offers `Открыть день отдыха` (D18) | FAIL | `tr1-today.png`, `sess-normal-1.png`, `sess-dark-3.png`, `relaunch3.png` |
| 11 | Nutrition render | Signed in | Open Питание | Shared page header + summary | Rendered without any page header; bespoke pink hero (D2) | FAIL | `t-nutrition.png`, `d2-nutrition.png` |
| 12 | Meal entry sheet | Nutrition | Tap `Добавить прием пищи` | Compact context, then commit actions | Opens; 12-hour `3:02 PM` in Russian UI (D10) | FAIL | `n-entry.png` |
| 13 | Suggest meal sheet | Entry sheet | Tap `Предложить приём пищи` | Suggestion cards | Opens; English food names, descriptions and `Goal: Snack` (D4) | FAIL | hierarchy capture |
| 14 | Meal editor | Entry sheet | Tap `Добавить прием пищи` | Editor with summary and actions | Opens; exposes no accessible elements (D1); large empty body (D12) | FAIL | `s2-editor.png` |
| 15 | Food picker (search tab) | Editor | Tap `Добавить еду` | Search field and results | Opens; still no accessible elements (D1) | FAIL | `s4-foodpick.png` |
| 16 | Food search submit | Picker | Type a query, search, add | Results and save | BLOCKED by #15: sheet is coordinate-only; not completed | BLOCKED | hierarchy shows only status bar |
| 17 | Nutrition breakdown | Nutrition | Tap `Микроэлементы` | Sheet with donut, macros, micros | Opens; mixed language, units, nested cards (D3, D7) | FAIL | `breakdown-light.png` |
| 18 | Food log page | Nutrition | Tap `Дневник питания` | Shared header, date strip, rings, meals | Rendered; rings differ from Nutrition bars, water card holds three unrelated actions (D3, D13) | FAIL | `fl4-card.png`, `foodlog-dark.png` |
| 19 | Empty meal groups | Food log | Scroll past summary | One compact empty row per group | Three ~215 pt cards, each repeating three emptiness sentences (D7, D12) | FAIL | `fl-meals-dark.png` |
| 20 | Meal card **Edit meal** | Food log / Nutrition | Locate the card's edit action | Reachable action that opens the editor | Card root description hides the action; not reachable by automation/VoiceOver (D1) | BLOCKED | hierarchy: only `Scrambled Eggs with Toast` |
| 21 | Dark appearance, five tabs | Dark | Visit each tab | Same hierarchy, readable | Captured; contrast generally holds, duplication and frame defects persist | PARTIAL | `dk-*.png`, `d2-nutrition.png`, `foodlog-dark.png` |
| 22 | Dark appearance, sheets | Dark | Re-open sheets | Same sheet styling | Activity chooser and session notes captured in dark and show the same defects as light; check-in, editor, picker and breakdown still light-only | PARTIAL | `sess-dark-3.png`, `notes-sheet.png` |
| 23 | Profile subpages | Signed in | Open personal info, nutrition settings, units, feedback, privacy, photo vault (start-up route) | Pushed routes render inside the shared frame | All six render; sticky save bars cover the last field everywhere (D21), header sliver (D22), raw `Maintain Health` (D25), double-framed birthday and unitless measurements (D24), `imperial` in Russian copy (D23) | FAIL | `light-ProfilePersonalInfoPage.png`, `rt-ProfileNutritionSettingsPage.png`, `rt-ProfileUnitsDisplayPage.png`, `rt-ProfileFeedbackPage.png`, `rt-ProfilePrivacyPage.png`, `rt-PhotoJourneyVaultPage.png` and their `dark-` counterparts |
| 24 | Auth, registration, onboarding, OTP | Signed out | Sign out, register, verify | Complete onboarding | Not driven; session restored, no sign-out | NOT RUN | — |
| 25 | Password recovery | Signed out | Submit address | Reset e-mail | Not attempted; needs owner approval for a real e-mail | BLOCKED | — |
| 26 | Relaunch persistence | Signed in | Terminate, relaunch | Same state | Session restored; page returns to Today | PASS | `03-relaunch.png` |
| 27 | Accessibility tree per surface | Any | `maestro hierarchy` on each surface | Every control exposed | Tabs expose controls; meal editor/picker expose nothing; meal card exposes only its description (D1) | FAIL | hierarchy dumps |
| 28 | Keyboard / hardware navigation | — | Tab/arrow through controls | Focus order | No hardware-keyboard pass possible in the simulator session | NOT RUN | — |
| 29 | Dynamic type (large text) | — | Set `content_size accessibility-large` and relaunch | Layout reflows without losing controls | Session screen loses its set grid, exercises collide with tabs, banner covers the header (D20); chooser rows overlap (D19); card headers collapse to a vertical sliver (D22) | FAIL | `sess-dark-now.png`, `sess-dark-medium.png` |
| 30 | UI guard suites | — | `dotnet test --filter UiStyleGateTests\|UiContractGuardTests` | Green | 24 passed | PASS | §5 |
| 31 | Core suite | — | `dotnet test tests/MuscleCuties.Core.Tests` | Green | 1353 passed, 1 failed (missing policy file) | FAIL | §5 |
| 32 | Registration / contracts / server suites | — | `dotnet test` per project | Green | Not run | NOT RUN | — |
| 33 | Network and console | Local API + simulator log | Inspect during the pass | No 5xx, no app exceptions | 127×200, 4 transient 401s on cold start, recovered; no app exceptions in the 8,472-line log; second pass added a Maestro-teardown explanation for the app "termination" (D17 retracted) | PARTIAL | §5 |
| 34 | Session set logging | Session open, exercise 2 current | Tap `Далее` without entering a weight; read the record | Validation, or an explicit record with a confirmation | Advanced to exercise 3 and recorded `Dead Bug` with the celebration banner; the per-set fields stayed empty, so the row was recorded from defaults | PARTIAL | session hierarchy, `sess-dark-now.png` |
| 35 | Session notes sheet | Session open | `Заметки` → type → `Сохранить заметку` | Note persists with confirmation | Saved, confirmation `Заметка сохранена.` shown, sheet stayed open; text field and both actions exposed in the tree | PASS | `notes-sheet.png`, `notes-saved.png`, hierarchy |
| 36 | Session pain / swap / rest timer | Session open | `Больно`, `Обменять`, `Начните 60-секундный отдых` | Each opens its panel | Not driven: the automation session ended on the tab bar twice before these steps | BLOCKED | — |
| 37 | Finish session and summary | Session open, all exercises answered | `Далее` on the last exercise, then finish | Summary, session closed | Not reached; the session row is still open in `app.workout_session_logs` | BLOCKED | — |
| 38 | Cycle phase detail page | Signed in, no phase estimate | Open `Фаза цикла` detail (start-up route) | Phase page with guidance and a chart | Rendered with an English subtitle, a ~300 pt empty card region and a clipped chart (D27) | FAIL | `rt-CyclePhaseDetailPage.png` |
| 39 | Direct start-up routing | Signed in | Launch with `MUSCLECUTIES_INITIAL_ROUTE=<page>` | Requested page as the initial route | Worked for profile, history, phase and food-log routes; the `WorkoutSessionPage` attempt showed the loading screen and the process was gone 15 s later, with no crash report. Not repeatable enough to call a defect | PARTIAL | `route-personal.png`, `direct-session.png` |
| 40 | Rest timer | Session open | `Начните 60-секундный отдых` | Countdown with pause, extend and finish | Timer runs `01:00`, but the progress bar uses the default iOS blue (off-palette) and the three controls have three widths; starting it while weight is `0` raises the contradictory weight banner (D29) | FAIL | `S7-rest.png` |
| 41 | Pain / stop-exercise sheet | Session open | `Больно` | Guidance with a safe choice | Opens with `Остановите это движение`; destructive `Остановить упражнение` carries the brand primary fill while `Отмена` is the outline, and the sheet keeps the error banner behind it | FAIL | `S8-pain.png` |
| 42 | Exercise swap | Session open | `Обменять` | Replacement list, confirm disabled until a choice | List renders; confirm is enabled with nothing selected and does nothing (D35); equipment and muscles are English | FAIL | `S9-swap.png`, `S10-swap-confirm-empty.png` |
| 43 | Finish session and summary | Session open, exercises answered | `Далее` on the last exercise | Summary and a closed session | Unreachable: the weight banner removes the set grid and `Далее` stops responding (D29); no summary was seen and no session was completed | BLOCKED | `S13-after-last.png`, `S15-weight-typed.png` |
| 44 | Session survives relaunch | Session open | Terminate, relaunch, open Train | Same in-progress state | Lost when no set is logged (D30); restored once a set exists | FAIL | `now-train.png`, `L1-today-landscape-dark.png` |
| 45 | Cycle quick log | Cycle | `Записать сегодняшние симптомы и интенсивность выделений` | Sheet with status, flow, symptoms, save | Opens; the form scrolls inside its own body and the last rows (`Тяга к еде`, `Другое`) end above the pinned `Сохранить запись`; the save action is disabled with a stated prerequisite until something is chosen (D32). The hero is still one accessibility element, so the action is not individually reachable | PASS | `quicklog-savebar-light.png`, `quicklog-savebar-dark.png`, `quicklog-save-disabled-light.png` |
| 46 | Cycle next-phase action | Cycle | `Следующая фаза` | A forecast, or a confirmed change | The screen that followed showed `СЕГОДНЯ В ВАШЕЙ ФАЗЕ` + `Овуляторная: запись от нояб. 13`, which is truthful: the tap had already written the record. The defect is the unconfirmed write, not a mislabelled preview (D31 withdrawn, D45 live) | FAIL | `S19-cycle-dark.png`, `S23-nextphase-light.png` |
| 47 | Archive session detail | Archive | Tap a session card | Detail of the saved session | Opens; the exercise summary is now localized (`Подходы: 1 · Объём: 200 кг`); two date formats in one flow and the save bar over the keyboard remain (D33, narrowed) | PARTIAL | `archive-volume-summary-light.png` |
| 48 | Dark: check-in, editor, picker, breakdown | Dark | Open each surface | Same styling as light | All four captured; editor shows Latin `P/C/F`, breakdown mixes `г`/`g`/`mcg` and both decimal separators (D38); picker and entry sheet use two selection recipes (D36) | PARTIAL | `D1-checkin-dark.png`, `D4-meal-entry-dark.png`, `D5-editor-dark.png`, `D7-picker-dark.png`, `D8-breakdown-dark.png` |
| 49 | Landscape | Any page | `setOrientation LANDSCAPE_LEFT` | Layout adapts | The driver reported the rotation completed, but the app kept its portrait geometry (portrait-only target); the Today CTA stays under the tab bar | FAIL | `L1-today-landscape-dark.png` |
| 50 | iPad (Pro 11-inch, iOS 26.3) | Fresh install | Install the same build and launch | Tablet layout | Runs in iPhone compatibility mode: a phone-width card centred on black, showing the English login page; an authenticated tablet layout cannot be reached without signing in, which this pass may not do | PARTIAL | `iPad-launch.png` |
| 51 | Cross-surface data agreement | Nutrition + Today | Compare today's intake | One figure per day | The capture pair that first suggested a mismatch was a Today loading state: `Записано 0 ккал` / `0 g` while the breakdown showed `582 ккал` / 32,8 g. After load, Today also shows `582 ккал` (`C24-today-loaded-light.png`), so the data agrees and only the missing loading state remains (D34, narrowed) | PARTIAL | `D8-breakdown-dark.png`, `D2-today-s2.png`, `C24-today-loaded-light.png` |
| 52 | Cycle month calendar (expand/collapse) | Cycle | `Показать календарь месяца` / month arrows / `Сегодня` | Month grid with legend, clean layout | Expands and collapses, month arrows work, `Сегодня` appears off-month; legend contradicts the cell rendering (D40); four saturated hue families and an undefined dark-green “today” fill (D43) | FAIL | `C7-month-expanded-light.png`, `C17-december-light.png`, `C18-december-modal-light.png` |
| 53 | Day tap → date/phase sheet | Month calendar open | Tap 14 Nov | Sheet with the day's date and phase pre-selected | Opens with `суббота, 14 ноября 2026 г.` and `Фолликулярная фаза (прогноз)`, the current phase checked, save enabled | PASS | `C8-datephase-modal-light.png` (hierarchy) |
| 54 | Change phase and save | Sheet open | Choose `Овуляторная` → `Сохранить фазу` | Row persisted, calendar updated | Saved (DB `2026-11-14 Ovulatory · Calendar phase edit`), sheet closed, calendar repainted day 14 yellow and the tail re-derived | PASS | `C9-selected-ovulatory-light.png`, `C10-after-save-light.png` |
| 55 | Save reflected on other surfaces | After #54 | Open Today | Same phase as Cycle | Today header reads `На этой неделе · Овуляторная`; `12` remained follicular and `13` ovulatory as recorded | PASS | `C24-today-loaded-light.png` |
| 56 | Cancel the sheet | Sheet open | Tap the close control | No change persisted | Closed; `cycle_phase_logs` stayed at 3 rows at that point, calendar unchanged | PASS | `C12-after-cancel-light.png` |
| 57 | Dismiss by backdrop | Sheet open | Tap the dimmed area | No change persisted | Closed; no row written | PASS | `C13-after-backdrop-light.png` |
| 58 | Out-of-order / impossible sequence | Sheet open | Set 9 Nov to `Менструальная`, save; set 14 Nov to `Менструальная`, save | Order warning or rejection | Both saved silently; the calendar then paints 9–18 Nov as one menstrual block (10 days) and no warning appears (D39) | FAIL | `C20-day10-modal.png`, `C21-after-day10-save.png`, `C11-warning-light.png` |
| 59 | Future date edit | Month arrows → December | Tap 1 Dec | Forecast-day edit blocked or clearly marked | Opens the same editor, `Лютеиновая фаза (прогноз)`, Save enabled, no mention that the day has not happened (D41) | FAIL | `C19-future-day-modal-light.png` |
| 60 | Cycle order warning popup (`Quick cycle check`) | Any cycle edit | Reach it through a conflict | Popup with cancel/confirm | Not reachable: no driven edit produced an order conflict (D39), so the modal was never shown. Not claimed as tested | BLOCKED | — |
| 61 | Legend truthfulness | Month calendar | Compare legend with cells after saves | Recorded days differ from predicted ones | Saved days keep the `*` (9*, 14*, 21*) although the legend reserves `*` for estimates; recorded swatch maps to no distinct fill (D40) | FAIL | `C10-after-save-light.png`, `C16-after-save-dark.png` |
| 62 | Phase explorer copy and treatment | Cycle page | Scroll to `Исследователь 4-фазы` | Localized, one card recipe | Shows the machine-translated `Исследователь 4-фазы` and the English `NOW` chip; four saturated bars vs the editor's four uniform pink cards for the same options (D43) | FAIL | `C7-month-expanded-light.png`, `C18-december-modal-light.png` |
| 63 | Nutrition entry sheet, populated state | Nutrition page | `Добавить прием пищи` | Header, pre-selected meal type and time, primary action | Opens with `Ужин` pre-selected, `Сегодня`, and a time field reading `7:15 PM` next to a chip reading `19:15` in the same sheet (D48); three stacked full-width actions, the last two sharing the secondary treatment | FAIL | `M1-entry-sheet-light.png` |
| 64 | Nutrition entry sheet, dismiss | Entry sheet open | Tap backdrop, then `Отмена` | Sheet closes, nothing written | Both paths close the sheet; `logged_meals` stayed at 2 rows and `logged_meal_entries` at 5 | PASS | `M1-entry-sheet-light.png`, `M3-foodlog-light.png` |
| 65 | Nutrition main-page meal editor, open | Entry sheet | `Добавить прием пищи` | Editor with empty summary and a footer commit | Opens with `Запишите прием пищи`, `0 ккал`, `P 0,0 g / C 0,0 g / F 0,0 g`, `Ужин` / `Сегодня` / `19:15`, `Добавить еду` + `Получить совет`, and `Приём пищи` disabled; one grey hint line under a body that is blank for ~700 pt. No accessible elements at all (D1) | FAIL | `M2-editor-light.png` (dark: `D5-editor-dark.png`) |
| 66 | Nutrition history, meal card actions present | Food Log, scrolled | Inspect the breakfast card | Edit action and save-template action on the card | Both controls are drawn: a 400 pt outlined `Изменить приём пищи` and a circular star. Neither is in the accessibility tree (D1); the edit one does not respond to taps at its own centre (D46) | FAIL | `M5-foodlog-scrolled-light.png`, `M9-edit-from-log-light.png`, `M11-edit-exact-light.png` |
| 67 | Nutrition history, save meal as template | Food Log, scrolled | Tap the star | Template saved, visible confirmation, list unchanged | Writes one `saved_meal_templates` row with the auto-name `Шаблон Завтрак (нояб. 13)` and shows the toast `Шаблон «…» сохранён.`; the list and the day totals are untouched. Row removed during cleanup | PASS | `M10-star-tap-light.png` |
| 68 | Nutrition history, save notes with the field empty | Food Log, scrolled | Tap `Сохранить заметки` with the placeholder untouched | Validation or a disabled action | Reports `Заметки сохранены.` and writes nothing; the placeholder text is never an error and there is no dirty state, so success is claimed for a no-op (D47) | FAIL | `M6-foodlog-meal-tap-light.png` |
| 69 | Nutrition cross-surface agreement | Breakfast saved (582 kcal) | Compare Nutrition, Food Log and Today | Same day agrees everywhere | Nutrition `582 / 2080`, Food Log `582 / 2080`, Today `582`; the same meal reads `32g / 45g / 29g` on Nutrition and `33г / 45г / 30г` on Food Log — agreement on energy, rounding drift on macros (D3) | PARTIAL | `M3-foodlog-light.png`, `M8-edit-from-log-light.png`, `C24-today-loaded-light.png` |
| 70 | Toast placement | Any action that toasts | `Сохранить заметки`, `Сохранить заметки и РПП` | Banner above the tab bar, fully readable | Both hosts place the banner above the tab bar and the pinned sheet footer with the full text on one line, in light and dark (D50) | PASS | `toast-foodlog-notes-light.png`, `toast-foodlog-notes-dark.png`, `toast-workouthistory-notes-light.png`, `toast-workouthistory-notes-dark.png` |
| 71 | Notes field with the keyboard up, light | Food Log, notes card | Tap the field, type `QA note keyboard test 14.11` | Label, field and `Сохранить заметки` stay visible above the keyboard | The page scrolls the card up; `Заметки`, all four typed lines and the save button sit fully above the keyboard. The keyboard's own `Готово` is stock iOS blue, off the app palette, but that is system chrome | PASS | `N5-notes-keyboard-focus-light.png` |
| 72 | Keyboard dismissed mid-edit, light | Notes field focused, text typed | Tap `Готово` | Keyboard closes, text kept, page height and scroll restored | Text unchanged after dismissal; the full card and the meal list below return, tab labels visible again; nothing is saved or reset | PASS | `N6-notes-keyboard-dismissed-light.png` |
| 73 | Notes field with the keyboard up, dark | Same, dark theme | Same | Same as light | Field, label and save button remain above the keyboard and the text stays legible on the dark card; the `Готово` accessory bar sits over the tab bar's labels (D50 family), not over the field | PASS | `N17-notes-keyboard-dark.png`, `N18-notes-keyboard-dismissed-dark.png` |
| 74 | Notes typed value persistence | Food Log, 13 Nov | Type, `Сохранить заметки`, leave to Nutrition, return to Food Log | Value is stored and shown again on the same day | Toast `Заметки сохранены.`; after leaving and reopening the day the field still reads `QA note keyboard test 14.11`, and `app.daily_nutrition_logs.notes` for `2026-11-13` holds the text (was empty in the backup). Restored to empty afterwards | PASS | `N7-notes-saved-light.png`, `N8-notes-reopen-light.png` |
| 75 | Food picker search with the keyboard up, light | Meal editor → `Добавить еду` | Tap the name field, type `chicken` | Field, tabs and results stay usable; sheet adapts to the keyboard | Field and `Поиск` stay above the keyboard, but the results area is covered: the empty-state line `Нет сохранённых…` is sliced horizontally by the keyboard's top edge and no result row is reachable until the keyboard is dismissed (D51) | FAIL | `N13-picker-keyboard-light.png` vs `N14-picker-keyboard-dismissed-light.png` |
| 76 | Food picker search with the keyboard up, dark | Same, dark theme | Same | Same | Identical behaviour: `chicken` typed, `Поиск` visible, empty-state line cut mid-glyph, four matches appear only after `Готово` is tapped (D51) | FAIL | `N15-picker-keyboard-dark.png` vs `N16-picker-keyboard-dismissed-dark.png` |
| 77 | Entry sheet scroll at the bottom edge | Nutrition → `Добавить прием пищи` | Swipe up inside the open sheet | Sheet scrolls to reveal content below the third action | Nothing moves: the sheet is already showing its last visible element and has no scroll container, so anything below `Использовать шаблон` (including the template empty state the view model sets) cannot be reached (D52) | FAIL | `N22-entry-sheet-dark.png`, `N25-entry-sheet-scrolled-dark.png` |
| 78 | `Использовать шаблон` with zero saved templates | Entry sheet, `saved_meal_templates` = 0 | Tap the third action twice | Sheet of templates, or an explicit `нет шаблонов` state | Nothing happens on either tap; no sheet, no message, no scroll (D52). Code sets `Nutrition_MealEntry_NoTemplates` into `MealTemplateStatus`, so the state exists but is never rendered in reach | FAIL | `N23-template-sheet-dark.png`, `N24-template-list-dark.png` |
| 79 | Time field while the sheet stays open | Entry sheet left open three minutes | Watch the `Время` value | A value chosen at open time stays fixed | The field rewrites itself every minute: `8:58 PM`, then `8:59 PM`, then `9:00 PM` on three consecutive captures with no user action (D53) | FAIL | `N22-entry-sheet-dark.png`, `N24-template-list-dark.png`, `N25-entry-sheet-scrolled-dark.png` |
| 80 | Deep link to the meal editor | App running | `xcrun simctl openurl booted "musclecuties://nutrition/history"` | App routes to nutrition history / the editor | iOS raises `Открыть в приложении «MuscleCuties»?`; after `Открыть` the app stays on Today. The scheme is registered for the Whoop callback only and no path router exists, so this entry cannot reach the editor | FAIL (entry absent) | `N19-deeplink-dark.png`, `N21-deeplink-accepted-dark.png` |
| 81 | Saved-template route to the meal editor | Entry sheet, star-saved template deleted again | `Использовать шаблон` | Template list that can open/edit a meal | Dead per D52 — the action never opens anything, so no template can be chosen or edited; the history editor stays unreachable by this route too | BLOCKED by D52 | `N24-template-list-dark.png` |

## 5. Checks that were run

**Build (current working tree, iOS simulator target)**

```
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**UI guard suites** — green, and insufficient for the defects below (§1).

```
dotnet test tests/MuscleCuties.Core.Tests --filter "FullyQualifiedName~UiStyleGateTests|FullyQualifiedName~UiContractGuardTests"
Passed!  - Failed: 0, Passed: 24, Skipped: 0, Total: 24, Duration: 566 ms
```

**Core suite** — red.

```
dotnet test tests/MuscleCuties.Core.Tests
[xUnit.net 00:00:01.81] MuscleCuties.Core.Tests.PlanningReleaseApprovalTests.Approved_reference_is_the_policy_version_recorded_in_the_owner_decision [FAIL]
  System.IO.DirectoryNotFoundException : Could not find a part of the path
  '/Users/denispetruk/RiderProjects/MuscleCuties/docs/release-audit/2026-11-05/WORKOUT_POLICY.md'.
Failed!  - Failed: 1, Passed: 1353, Skipped: 0, Total: 1354, Duration: 1 s
```

**Local API traffic during the pass**

```
127 200
  4 401   (GET /api/v1/workouts/progress ×2, GET /api/v1/profile/units, GET /api/v1/nutrition/diary/day, all within the first seconds after a cold start)
```

Those four 401s are the expected expired-access-token window before refresh; the pages rendered
afterwards and no action failed because of them. The simulator log (`app.log`, 8,472 lines) contains
no app exception, only accessibility/test-driver chatter.

## 6. Defects

Every entry states whether it was reproduced on the running app or found by reading code.

### D1 — P0 — Meal editor and food picker are absent from the accessibility tree
**Reproduced in the running app** (three times, both sheets).

Repro: Nutrition → `Добавить прием пищи` → `Добавить прием пищи` → then `Добавить еду`.
Expected: the sheet's title, fields, tabs, food rows and footer are separate accessible elements.
Actual: `maestro hierarchy` returns only the window and the status bar. No title, no close button, no
text field, no rows. VoiceOver would announce nothing and offer nothing to activate; the food logging
path is unusable with assistive technology, and UI automation can only tap blind coordinates.
Related: `MealCardView` sets `SemanticProperties.Description` on its root `Border`
(`MealCardView.xaml:11-15`), so the card collapses to one element in the same way — its **Edit meal**
and save-template actions never appear in the tree (`Scrambled Eggs with Toast` is the only node).
Code corroboration: `ModalAccessibilityScope` in `Controls/Shared/AnimatedModalView.cs:390+` walks up
from the sheet and sets `AutomationProperties.ExcludedWithChildren` on every sibling. The exact reason
the sheet itself ends up unexposed is not proven from source, but the behaviour is reproducible and
must be fixed and covered by an accessibility-tree test.
Evidence: `s2-editor.png`, `s4-foodpick.png`, hierarchy dumps for both, `fl4-card.png` hierarchy.

**Correction (modal pass).** The last sentence above is only half true. The card's `Изменить приём пищи`
and star actions *are* drawn — the star works and writes a template, so the collapse hides them from
automation and VoiceOver but not from sight. The edit button, however, does not respond to a tap at its
own centre either, so the missing tree is not the only reason that path fails: see D46.

### D2 — P1 — Nutrition has no shared page header and uses a second page skin
**Reproduced in the running app.** Code: `Pages/Nutrition/NutritionPage.xaml` has no `PageHeader`; its
root grid still declares an empty `Auto` row (`RowDefinitions="Auto,*"`, CollectionView in row 1), and
the visible header is a pink `CardPrimary`-like block with an eyebrow dot, the title repeated, a
subtitle and a circular heart action inside the CollectionView header. Every other page composes
`sharedControls:PageHeader` (21 page files; Nutrition and the session page are the only exceptions).
The result is the one page a user visits several times a day looking like a different product: a
filled hero instead of the quiet title + action pattern, a second title treatment (27 pt inside a card
versus 27 pt on the page background), an extra large icon button competing with the actions below, and
no visible back/contextual affordances used elsewhere.
Evidence: `t-nutrition.png` vs `t-train.png`, `dk-train.png`, `dk-cycle.png`.

### D3 — P1 — The two nutrition screens present the same numbers three different ways
**Reproduced in the running app.** Reproduced units: Nutrition shows `32g / 99g`; Food Log shows
`33г` and `Цель: 99г`; the breakdown sheet shows `32,8 г`, `0,0mcg`, `75,0mg` and `25,0g` in one column.
The visual model also changes: Nutrition uses horizontal bars (`MacroProgressGrid`), Food Log uses
rings with macro icons, the breakdown sheet uses a donut plus bars plus per-micronutrient cards.
Design policy requires one role to use one contract across features. Evidence: `t-nutrition.png`,
`fl4-card.png`, `breakdown-light.png`.

### D4 — P1 — English copy inside the Russian interface
**Reproduced in the running app.** Concrete strings: `Rest Focus` (Today), `Rest day` and
`FRI · день отдыха` (Train), `Snacks` (Food Log meal heading, with Russian siblings), `Protein`,
`Carbs`, `Fats` (Nutrition, breakdown), `Fiber`, `Vitamins`, `Vitamin A/C` (breakdown),
`Planned`, `Goal: Snack`, and English food names/descriptions in the suggestion sheet.
Code causes found: `Controls/Shared/MacroProgressGrid.xaml.cs:44` holds
`["Protein", "Carbs", "Fats"]` as literals and `Models/UI/Nutrition/MacroBreakdownItem.cs:21-32` maps
the same English names to icons; neither resolves through `UiTextCatalog`. The design contract treats
an untranslated or unresolved string as a release defect, not a fallback.

### D5 — P1 — Check-in sheet footer is clipped by the screen bottom
**Closed and proved on screen.** The frame's 12 pt footer reserve plus the check-in sheet's own scroll body
keep both actions inside the sheet above the home indicator. Evidence: `checkin-sheet-footer-light.png`,
`checkin-sheet-footer-dark.png`, `entry-sheet-light.png`, `entry-sheet-dark.png`.
Repro: Today → `Заполнить отметку`.
Expected: both footer actions fully visible above the home indicator, sheet respecting the safe area.
Actual: `Пока пропустить` and `Сохранить отметку` are cut horizontally at their bottom edge; the commit
action is partly off-screen. `DailyCheckInModal.xaml:22` uses `Auto,Auto,*,Auto` rows with the footer
in the last row; the sheet's maximum height and the safe-area inset do not account for the pinned footer.
Evidence: `checkin-light.png`.

### D6 — P1 — Broken status sentence on Train
**Reproduced in the running app.** `Дней полного отдыха: 1 training day - 6` mixes Russian and English
and reads as a failed template substitution. Expected: one localized sentence such as
`Дней полного отдыха: 1 · Тренировок: 6`, or a short status label. Evidence: `dk-train.png`.
This is a runtime-format string, so it is also a localization-catalog gap rather than a one-off typo.

### D7 — P2 — Every surface states the same fact two to three times
**Reproduced in the running app.**
- Today rest card: title `День отдыха`, a `ДЕНЬ ОТДЫХА` pill directly under it, then an inner notice
  repeating that it is a rest day.
- Today readiness card: `Заполните отметку, чтобы увидеть готовность` as subtitle, again inside a
  tinted box, and a third sentence above the button.
- Cycle: `День 14 из 28` in the hero and as the next card's title; `Фолликулярная` in the title and
  again in `Фолликулярная: запись от нояб. 13`.
- Food Log: the date in the strip and again as the summary title; each empty meal card says
  `0 ккал`, `0% дневной цели`, `Пока ничего не добавлено`, `P 0g · C 0g · F 0g` and
  `Пока нет записанных продуктов.`
- Profile vault: `Предпросмотр скрыт для конфиденциальности`, `Нажмите, чтобы показать`, then a
  `Показать` button.
The design contract requires one statement per screen and that a status be a short label, never a
sentence. Evidence: `dk-today.png`, `dk-cycle.png`, `dk-profile.png`, `fl-meals-dark.png`.

### D8 — P2 — Meal card header misaligns the calorie block
**Reproduced in the running app.** In `MealCardView.xaml` the heading row is a wrapping `FlexLayout`
with the calorie stack set to `HorizontalOptions="End"` but no trailing justification on the parent, so
the calorie block is laid immediately after the heading rather than at the card's trailing edge
(`582 ккал` floats around mid-card; `0 ккал` sits under the meal name in empty cards, with
`0% дневной цели` centred under it). Every other block on both pages aligns to the gutter, so the meal
card reads as broken. Evidence: `t-nutrition.png`, `fl-meals-dark.png`.

### D9 — P2 — The same cycle phase is coloured differently on different screens
**Reproduced in the running app.** The follicular phase renders with a pink leaf and pink accents in
the Cycle hero, a green leaf icon and green-tinted circle in the next Cycle card, and a green pill on
Profile. Colour is not the only signal (labels exist), but a category colour that changes per screen
destroys the meaning the palette is supposed to carry. Evidence: `dk-cycle.png`, `dk-profile.png`.

### D10 — P2 — 12-hour clock inside the Russian interface
**Reproduced in the running app.** The meal entry sheet shows `3:02 PM` while every other surface and
the Russian locale use 24-hour time (`15:02` in the status bar at the same moment). Evidence:
`n-entry.png`.

### D11 — P2 — Day/filter strips differ per screen and clip at the right edge
**Reproduced in the running app.** Train's week strip is a text row (`MON`, `TUE`…) with Russian dates
and tick marks, clipped after `SA`/`14 н` with no fade or scroll affordance; Food Log's strip is
56 pt pill cells with a selected outline and a dot. Both are day selectors. Train's filter chips and
Food Log's water card are cut by the tab bar because the scroll content has no bottom tab-bar inset.
Evidence: `dk-train.png`, `fl4-card.png`.

### D12 — P2 — Nested card shells and oversized empty states
**Reproduced in the running app.** The check-in sheet contains two full cards inside the sheet surface;
the Cycle hero contains two tinted sub-cards; each of twelve micronutrients in the breakdown sheet is a
full card; the meal editor's body is a large blank area under a disabled pink commit; empty meal groups
occupy ~215 pt each. The specification prefers rows and separators inside one group and forbids nested
card shells. Evidence: `checkin-light.png`, `dk-cycle.png`, `breakdown-light.png`, `s2-editor.png`,
`fl-meals-dark.png`.

### D13 — P2 — The water card is used as a shelf for unrelated actions
**Reproduced in the running app.** The Food Log's `Вода` card contains `+250 мл`, the label
`Дневник за вчера`, a floating `Скопировать вчерашнее` button and a `Разбор питательных веществ`
button. The three buttons have three different widths and two different corner treatments inside one
card. Evidence: `fl4-card.png`.

### D14 — P2 — Disabled primary actions have no explanation and almost no contrast
**Reproduced in the running app.** Check-in `Сохранить отметку` and meal editor `Приём пищи` are pale
pink fill with muted mauve text (visually near 2:1), and nothing marks what is missing. The contract
requires a nearby explanation of the unmet prerequisite and a readable explanation even when the
control is disabled. Evidence: `checkin-light.png`, `s2-editor.png`.

### D15 — P3 — Section-heading and status language drift
**Reproduced in the running app / hierarchy.** Profile uses uppercase section headings
(`ЧАСТНОЕ ХРАНИЛИЩЕ`, `ЗДОРОВЬЕ & ТРЕНИРОВКИ` — including an ampersand in Russian) while Train uses
sentence case (`Расписание активности`). Profile's stat rows label themselves `Сессия: серия дней`
and expose `Полоса занятий: 1 дней` to accessibility (wrong plural form for 1). Evidence:
`dk-profile.png`, Profile hierarchy dump.

### D16 — P1 (build health) — Core suite fails on a missing policy file
**Fixed on `fix/qa-audit-followups`.** `docs/release-audit/2026-11-05/WORKOUT_POLICY.md` was restored from
`77f589ac^`; `dotnet test tests/MuscleCuties.Core.Tests` now reports `Failed: 0, Passed: 1379`. Commit
`19887dca`.
**Reproduced by command; classified as a genuine regression, not an environment problem.**
`PlanningReleaseApprovalTests.Approved_reference_is_the_policy_version_recorded_in_the_owner_decision`
throws `DirectoryNotFoundException` for `docs/release-audit/2026-11-05/WORKOUT_POLICY.md`.
Classification evidence: the file exists in history and was deleted by commit `77f589ac`
("Update project documentation and product workflows"); it is absent at HEAD and is not gitignored, and
the test reads it with `File.ReadAllText(Path.Combine(RepoRoot, "docs", "release-audit", "2026-11-05",
"WORKOUT_POLICY.md"))`. So the checkout is healthy and the artifact the release gate depends on is gone,
which means every gate that runs the Core suite is red for a real reason and masks genuine failures.
Fix by restoring the approved policy document, not by changing the test.

### D17 — Retracted — the "unexplained termination" was the test driver, not the app
A previous observation recorded the app disappearing to the Home screen while a sheet was open, with no
crash report. The simulator log explains it: `maestro-driver-iosUITests-Runner: (Foundation) [Start]
Terminating app com.musclecuties.app` at each driver session close, followed by `runningboardd`
re-launching the app for the next flow. No `MuscleCuties.UI` crash report was written at any point
(the newest is from 04:15, before this pass). The app is not terminating on its own; the automation
driver tears it down between flows. Recorded here so the earlier observation is not carried forward as
a defect.

### D18 — P1 — In-progress workout card offers "Открыть день отдыха"
**Reproduced in the running app.** With a session already running, Today shows a workout card titled
`High volume strength`, a `ТРЕНИРОВКА` pill, `В процессе` twice (`Сегодня · В процессе` and a status
row), and a full-width action labelled **`Открыть день отдыха`** rendered in the disabled treatment
(pale fill, muted text). The card therefore names the wrong action and looks inactive while the
session is live. Expected: one status line and an enabled action such as `Продолжить тренировку`, with
no rest-day copy on an active card. This is the same class as D6 — a stale string left behind when the
card switched state. Evidence: `relaunch3.png`.

### D19 — P1 — Activity chooser sheet: overlapping rows and a slow dismissal
**Reproduced in the running app, light and dark, at default and at accessibility text size.**
Repro: Train → `Продолжить тренировку` → sheet `Активности тренировки`.
Expected: sheet title on one line, each activity row with name and metadata on separate aligned lines,
and the sheet gone as soon as an activity is chosen.
Actual: the title is set at display size and dominates the sheet; inside the selected-activity row the
plan meta (`5 упражнений ›`) and the progress line (`0 из 5 отмечено`) overlap so the bottom one is
clipped; the sheet surface extends over the page card beneath it, leaving a large blank slab. After
tapping `Strength & Sculpt` the sheet stayed on screen for several seconds while the session screen had
already been created behind it — the screenshot taken four seconds after the tap still shows the sheet
(`sess-normal-1.png`), and the hierarchy taken later shows the session, so navigation and dismissal are
not synchronised. Evidence: `sess-dark-3.png`, `sess-normal-1.png`, chooser hierarchy.

### D20 — P1 — Session screen loses its set-entry grid at large text
**Reproduced in the running app.** At content size `accessibility-large` the session screen no longer
renders the `Подходы / Повторы / кг` column headers or their three inputs; only the accessible labels
remain (they are announced from hidden elements). The exercise name wraps onto three lines and runs
into the progress tabs, and the recorded-set celebration banner (`Празднование`, `Dead Bug записано`)
covers the header. At default text size the same screen is correct: hierarchy shows `Подходы`,
`Повторы`, `кг` and `Записанные подходы` with one row. Logging a set is therefore impossible on this
screen for users who enlarge text. Evidence: `sess-dark-now.png` (large) versus session hierarchy
(default size, §11 and the session hierarchy). Related English strings seen in the same state: `1 sets x 10 reps`,
`Last 20 kg with 1 x 10`.

### D21 — P1 — Sticky save bars cover form content on every Profile subpage
**Reproduced in the running app, light and dark.** On `Личная информация`, `Настройки питания`,
`Единицы измерения` and `Обратная связь` the primary action is pinned in a bar that floats over the
scrolling card, and the last field of each form sits half-hidden underneath it: the cycle-length field,
the `Изменить рассчитанные цели…` helper line, the `Энергия` field and the `Приоритет` field
respectively. There is no bottom content inset equal to the bar, so the field cannot be read to the
typed value, and the same defect reproduces identically in both appearances. Expected: the scroll
content ends above the pinned bar. Evidence: `light-ProfilePersonalInfoPage.png`,
`dark-ProfilePersonalInfoPage.png`, `dark-ProfileNutritionSettingsPage.png`,
`dark-ProfileUnitsDisplayPage.png`, `rt-ProfileFeedbackPage.png`.

### D22 — P1 — Section headers collapse into a vertical sliver beside a trailing action
**Reproduced in the running app.** On `Настройки питания` the card header `Целевые макросы` is squeezed
into a four-line column — `Целев` / `ые` / `макро` / `сы` — because `Дополнительные настройки` claims
the row width, and the same row is unreadable at both appearances. This is the header/action row
pattern used on several cards; it needs a wrapping rule that gives the title priority (title above the
action, or a full-width action below it). Evidence: `dark-ProfileNutritionSettingsPage.png`,
`rt-ProfileNutritionSettingsPage.png`.

### D23 — P2 — Units page mixes languages, and placeholders read as saved values
**Reproduced in the running app.** The explanatory sentence contains an untranslated word
(`Система imperial использует фунты, унции, дюймы и мили`), the four category fields display `kg`, `g`,
`cm`, `km` as text inside the inputs with no placeholder styling or unit hint, so a user cannot tell
whether a value is set, and the selected-chip recipe here (outline plus tint) differs from the filled
selected recipe on Train's filters and the day strip. Evidence: `dark-ProfileUnitsDisplayPage.png`,
`rt-ProfileUnitsDisplayPage.png`.

### D24 — P2 — Personal info: double-framed date field and unitless measurements
**Reproduced in the running app, both appearances.** The birthday control is drawn as a rounded field
inside another rounded field (`15.03.1996` sits inside two concentric borders), which no other input on
the screen has; `Рост 165` and `Вес 62` carry no unit and no unit suffix; and the card's trailing
`Личные данные` text is styled like the muted caption next to it, with no affordance that it is an
action. Evidence: `light-ProfilePersonalInfoPage.png`, `dark-ProfilePersonalInfoPage.png`.

### D25 — P1 — Nutrition goal is shown as a raw English enum
**Reproduced in the running app.** `Настройки питания` → `Цель питания` displays **`Maintain Health`**
inside the Russian interface, in both appearances. The value reaches the field as an enum name rather
than through `UiTextCatalog`, so it is a data-localization gap, not a missing translation string.
Evidence: `dark-ProfileNutritionSettingsPage.png`, `rt-ProfileNutritionSettingsPage.png`.

### D26 — P2 — Workout archive: raw duration, English plural, split label
**Reproduced in the running app.** One completed session renders `Продолжительность` broken mid-word
(`Продолжительно/сть`), the duration as `1800s`, the set count as `1 sets`, the date as
`Чт, нояб. 12, 2026` (Russian abbreviation inside a US-ordered date, while other screens use
`15.03.1996` and `13 ноября`), the volume label as `Объем` without `ё`, and a footer line
`Все упражнения загружены` that reads like a debug status. Expected: `30 мин`, `1 подход`,
`12 нояб. 2026` and no debug line. Evidence: `rt-WorkoutHistoryPage.png`.

### D27 — P2 — Cycle phase detail: bilingual subtitle, empty card body, clipped chart
**Reproduced in the running app.** With no phase estimate the page title is `Фаза цикла неизвестна`
with the English subtitle `No phase estimate`; the explanation card contains a ~300 pt empty region
between the paragraph and its tinted note; and the `Взгляд в общем` chart at the bottom is cut off
mid-row with its value truncated (`Неизвестн…`). Expected: one localized subtitle, a card sized to its
content, and the chart either fully visible or clearly scrollable. Evidence:
`rt-CyclePhaseDetailPage.png`.

### D28 — P2 — Calorie units switch between `ккал` and `kcal` inside one block
**Reproduced in the running app.** Today's nutrition card prints `582 ккал` as the value and
`/ 2 080 kcal` as the target in the same sentence pair, while Food Log prints `2080 ккал` for the same
target. Two alphabets and two digit groupings for one number; this is the same formatter gap as D3 and
should be closed by the single formatter proposed there. Evidence: `normal-check.png`,
`light-FoodLogPage.png`.

### D29 — P1 — A workout session cannot be completed: the set fields sit under the pinned controls
**Reproduced in the running app.**
Repro: Today/Train → `Начать тренировку` → `Strength & Sculpt` → `Начать` → tap `Далее` once → tap
`Начните 60-секундный отдых` (or `Далее` again).
Expected: the `Подходы / Повторы / кг` fields stay fully visible above the action block, and pressing
`Далее` either records the row or explains precisely what is missing.
Actual: in the default state the three inputs are drawn at y≈530–620 while the pinned action block
starts at y≈588, so the inputs are cut in half and their values cannot be read or reached; scrolling
does not fix it (the block is off-layout, not merely below the fold). After the first `Далее` a banner
appears — “Укажите корректный вес в кг. Для собственного веса используйте 0 кг.” — whose instruction
(0 kg) is exactly the value it rejects, and the set grid then disappears from the page altogether.
Five further taps on `Далее` changed nothing, `Повторить` does not clear the banner, and the only
weight field left is behind the pinned block. A `scrollUntilVisible` for `Вес в килограммах` reported
success and then the tap landed on the rest timer, starting a 60-second countdown instead of focusing
the field. The finish action is therefore unreachable in this state.
Evidence: `S6-park-simctl.png`, `S13-after-last.png`, `S15-weight-typed.png`, `S14-fields-visible.png`.

### D30 — P1 — An open session is not resumed after the app is relaunched
**Reproduced in the running app.**
Repro: start a session, leave the session screen without logging a set, terminate and relaunch the app,
open Train.
Expected: the same in-progress state as before the relaunch (`Тренировка продолжается` /
`Продолжить тренировку`), or an explicit statement that the session was discarded.
Actual: Train offers `Начать тренировку` again and the day is shown as not started. Two launches showed
this (`now-train.png` at 18:06, and the failed assertion on `Продолжить тренировку` at 18:04) after the
session screen had been reached and the app was restarted. A session that already has a logged set is
restored (`L1-today-landscape-dark.png` shows `В процессе` / `Продолжить тренировку` and `Подходы 2`),
and the database holds the row — so the loss is specific to a session with no recorded set, and the UI
gives no warning that work has been dropped.
Evidence: `now-train.png`, `S4-session-dark.png`, `L1-today-landscape-dark.png`, `S5.log` (failed
assertion).

### D31 — WITHDRAWN — the "preview" was an already-committed advance
> **Status: withdrawn (Cycle pass).** The tap on `Следующая фаза` did not preview anything — it wrote the
> phase record and repainted the page, so both screens were describing the same, genuinely current phase.
> The live defect is D45 (an irreversible write behind one tap). The observation below is kept only as the
> record of what was seen; it is not a valid defect and must not be counted as one.

**Reproduced in the running app.** (observation only — interpretation corrected, see the note above)
Repro: Cycle → `Следующая фаза`.
Expected: a preview of the next phase, labelled as a forecast, with the current phase still identified
as today's.
Actual: the destination reuses the “today” hero verbatim: eyebrow `СЕГОДНЯ В ВАШЕЙ ФАЗЕ`, title
`Овуляторная`, `День 14 из 28`, and the row `Овуляторная: запись от нояб. 13` — while the Cycle page
immediately before it said `Текущая фаза: Фолликулярная` with `Фолликулярная: запись от нояб. 13` for
the same day. Two contradictory claims about the current phase, one tap apart.
Evidence: `S19-cycle-dark.png` versus `S23-nextphase-light.png`.

### D32 — P2 — Quick-log save bar covers the symptom rows
**Closed and proved on screen** (Wave A close-out). The sheet keeps its own `ScrollView` inside an
`Auto, *, Auto` grid, so the form body ends above the pinned footer instead of running underneath it.
Driven from the real entry (`Cycle → Записать сегодняшние симптомы…`) in both themes: scrolled to the
last row, `Тяга к еде` and `Другое` sit fully above `Сохранить запись`, and the scrollbar is visible.
`Сохранить запись` is disabled until a period mark, a flow intensity or a symptom is chosen, and its
accessibility hint states that prerequisite (`Cycle_QuickLog_SaveRequiresInputHint`) instead of letting
the tap do nothing. Guard: `The_quick_log_save_states_its_prerequisite_when_disabled`.
Evidence: `quicklog-top-light.png`, `quicklog-savebar-light.png`, `quicklog-top-dark.png`,
`quicklog-savebar-dark.png`, `quicklog-save-disabled-light.png`, `quicklog-save-disabled-dark.png`.

### D33 — P2 — Archive detail: two date formats and English inside the Russian sheet
> **Status: narrowed (Wave A close-out).** The English summary is fixed: the exercise row now reads
> `Подходы: 1 · Объём: 200 кг` from one localized key (`Workout_History_VolumeSummaryFormat`) instead of
> the hard-coded `1 working sets · 200 kg vol`, and the metric label uses `Объём`. What remains open is
> the date-format mismatch (sheet header `четверг, 12 ноября 2026 г. 09:00` vs list `Чт, нояб. 12, 2026`)
> and the save bar sitting over the keyboard toolbar. Evidence: `archive-volume-summary-light.png`.

**Reproduced in the running app.**
Repro: Train → `Архив тренировок` → tap the session card.
Expected: the same date format as the list and Russian units.
Actual: the sheet header reads `четверг, 12 ноября 2026 г. 09:00` while the card that opened it reads
`Чт, нояб. 12, 2026`; the exercise row reads `1 working sets · 200 kg vol` (English plural and units);
the metric label is `Объем` without `ё`. The pinned `Сохранить заметки и РПП` sits on top of the
keyboard toolbar, and the `ПОВТОРЫ` field is hidden behind the keyboard when it is open.
Evidence: `S16-archive.png`, `S18-archive-tap.png`, archive-detail hierarchy.

### D34 — P2 — Today prints real-looking zeros while the day's intake loads
> **Status: narrowed (Cycle pass).** A capture taken after the dashboard finished loading showed `582 ккал`
> on Today, matching the breakdown, so the two surfaces read the same data. What remains — and what the
> entry below is now scoped to — is the missing loading state: zeros are shown as if they were values.
> See the Corrections section for the narrowing.

**Reproduced in the running app.**
Repro: Nutrition → `Микроэлементы` (shows `582 ккал`, `P 32,8g · C 45,2g · F 30,0g`, `0 из 12` goals),
then Today → progress card.
Expected: one figure for today's intake on every surface.
Actual: Today prints `Записано 0 ккал` with `Белки 0 g / Углеводы 0 g / Жиры 0 g` and `Серия питания 0`,
while the breakdown sheet for the same day prints `582 ккал` and 32,8 g protein. Both were captured
within two minutes of each other with no data change in between, so at least one surface is reading the
wrong day or the wrong field.
Evidence: `D8-breakdown-dark.png`, `D2-today-s2.png`.

### D35 — P2 — Swap sheet: confirm does nothing without a selection, and its content is English
**Reproduced in the running app.**
Repro: Session → `Обменять` → tap `Подтвердить замену` without choosing a replacement.
Expected: the primary action is disabled until a replacement is selected, or it explains what is
missing.
Actual: the button is enabled and the tap produces no change, no message and no selection state on
either card — the sheet simply stays as it was. The two options are labelled `Cables` and `Dumbbell`
with muscles `UpperBack, Lats, Biceps` (English enum names, no space after the comma) inside a Russian
sheet.
Evidence: `S9-swap.png`, `S10-swap-confirm-empty.png`.

### D36 — P2 — One flow uses two selection recipes and two sheet headers
**Reproduced in the running app, dark.**
Repro: Nutrition → `Добавить прием пищи` → meal editor → `Добавить еду`.
Expected: the same chip recipe for a selected option and the same sheet header anatomy across the
three nutrition sheets.
Actual: the meal-type chips in the entry sheet mark the selected option with an **outlined** pink border
on a tinted fill; the tabs in the picker mark it with a **solid** fill and white label; the picker adds
a back chevron next to the close button while the entry sheet and editor have only the close button.
The editor's body is also ~700 pt of empty space with the commit button disabled and unexplained.
Evidence: `D4-meal-entry-dark.png`, `D5-editor-dark.png`, `D7-picker-dark.png`.

### D37 — P2 — The check-in commit button is occluded by the tab bar
**Closed and proved on screen.** `PresentationPageContentPadding` now ends at 96 pt (49 pt bar + 34 pt home
inset + breathing room), so page content scrolls clear of the bar, and `AnimatedModalView.FindScrollBody`
reuses the check-in sheet's own nested body instead of wrapping it in a second one, which was pushing the
pinned footer off-screen. Evidence: `checkin-button-clear-of-tabbar-light.png` (`Заполнить отметку` fully
above the bar) and `checkin-sheet-footer-light.png` / `checkin-sheet-footer-dark.png` (footer inside the
sheet in both themes).
Repro: Today → scroll so the readiness card is on screen → tap `Заполнить отметку`.
Expected: the button is fully visible and tappable above the tab bar.
Actual: at rest the button's lower half is under the tab bar; a text tap failed with “element not
visible” twice and a coordinate tap at the button's visible area produced no response. It only opens
after an exact partial scroll (`D3-before-checkin.png` → sheet), which a user has no way to discover.
The same page also labels the photo action `Вход` under `Фото прогресса`, which reads as “log in”.
Evidence: `L1-today-landscape-dark.png`, `D0-today-scrolled.png`, `D2-today-s2.png`,
`D3-checkin-dark.png`.

### D38 — P2 — Dark surfaces mix decimal separators and unit alphabets
**Reproduced in the running app, dark.**
Repro: Nutrition → breakdown sheet; meal editor.
Expected: one number formatter.
Actual: the breakdown prints `P 32,8g` and `32,8 г` next to `0,0mcg / 700,0mcg` and `0,0mg / 75,0mg`,
the editor prints `P 0,0 g / C 0,0 g / F 0,0 g` with Latin single letters, and the Nutrition page prints
`32g / 99g` with a dot-less Latin `g`. Four unit spellings and two decimal separators across three
screens of the same feature; this is the same formatter gap as D3/D28 and is listed separately only
because the dark captures are new.
Evidence: `D8-breakdown-dark.png`, `D5-editor-dark.png`, `D4-meal-entry-dark.png`.

### D39 — P1 — The phase editor accepts an out-of-order cycle and paints an impossible projection
**Reproduced in the running app.**
Repro: Cycle → `Показать календарь месяца` → tap 9 November → choose `Менструальная` → `Сохранить фазу`;
then tap 14 November → `Менструальная` → `Сохранить фазу`.
Expected: the app's own order-conflict path (`Keep your rhythm clean` inline warning or the
`Quick cycle check` popup) fires, or the save is rejected, because 12 November is follicular and
13 November is ovulatory on the same cycle.
Actual: both saves succeed with a plain toast-free close. The database then holds
`2026-11-09 Menstrual · Calendar phase edit` and `2026-11-14 Menstrual · Calendar phase edit`, and the
calendar repaints 9–18 November as a single ten-day menstrual block while 13 November (a recorded
ovulatory day) is shown in the same menstrual region. No warning, no confirmation, no explanation.
Evidence: `C20-day10-modal.png`, `C21-after-day10-save.png`, `C11-warning-light.png`; rows quoted
above from `app.cycle_phase_logs` during the pass.

### D40 — P2 — The calendar legend contradicts what the calendar draws
**Reproduced in the running app, light and dark.**
Repro: Cycle → month calendar → save a phase for any day → compare the cells with the legend.
Expected: a day with a saved observation drops the estimate marker and uses the `Зафиксированное
наблюдение` swatch; a predicted day keeps `*`.
Actual: days saved during this pass keep their asterisk (`9*`, `14*`, `21*`) while today (`13`, which
has a record) has none and is painted in a dark-green fill that the two-swatch legend never mentions;
the rest of the grid uses four phase hues at two saturation levels. A reader cannot tell a recorded
day from an estimated one, which is the legend's whole purpose.
Evidence: `C10-after-save-light.png`, `C12-after-cancel-light.png`, `C16-after-save-dark.png`,
`C7-month-expanded-light.png` (legend).

### D41 — P2 — Far-future days are editable as if they had already happened
**Reproduced in the running app.**
Repro: Cycle → `Показать календарь месяца` → next month → tap 1 December.
Expected: editing a day three weeks ahead is either blocked or labelled as planning a forecast.
Actual: the same sheet opens with `Лютеиновая фаза (прогноз)` and an enabled `Сохранить фазу`; nothing
distinguishes writing an observation for a day that has not occurred from correcting a past one. The
subtitle says `(прогноз)` while the action records an observation, so the sheet contradicts itself.
Evidence: `C19-future-day-modal-light.png`.

### D42 — P2 — Save closes with no confirmation or undo, and cancel leaves a selection ring
**Reproduced in the running app.**
Repro: save a phase; then open another day, cancel with the close control; then open it again and
dismiss by tapping the backdrop.
Expected: a saved confirmation (and ideally an undo, since the calendar re-derives the whole month), and
a clean calendar after abandoning an edit.
Actual: the sheet simply disappears and the grid silently repaints; the abandoned day keeps its pink
selection ring after both cancel and backdrop dismiss (`21` ringed in `C12`/`C13`), so the calendar still
claims a selection that no longer means anything.
Evidence: `C12-after-cancel-light.png`, `C13-after-backdrop-light.png`, `C10-after-save-light.png`.

### D43 — P2 — The phase list is drawn two different ways and one label is machine-translated
**Reproduced in the running app, light and dark.**
Repro: Cycle → compare the `Исследователь 4-фазы` card with the phase list inside the date sheet.
Expected: one localization pass and one recipe for the same four options.
Actual: the explorer shows four full-width bars each in its own saturated hue (`Менструальная` rose,
`Фолликулярная` green, `Овуляторная` yellow, `Лютеиновая` lilac) with the English chip `NOW`, and its
heading is the literal `Исследователь 4-фазы`; the sheet shows the same four options as four identical
pink cards with only the phase name tinted. The page title is a lowercase month (`ноябрь 2026`) repeated
inside the calendar card, and weekdays are lowercase (`суббота, 14 ноября 2026 г.`).
Evidence: `C7-month-expanded-light.png`, `C18-december-modal-light.png`, `C14-datephase-modal-dark.png`.

### D44 — P2 — Today's workout metrics row collides at large text
**Reproduced in the running app.**
Repro: Today with a planned strength day at default text size.
Expected: three metrics on one row with labels that do not overlap.
Actual: the row reads `Продолжительность` / `Упражнения` / `Интенсивность` but the clock icon overlaps
the first label and the dumbbell icon is drawn on top of the second, so all three headers are partially
obscured; the values below are `45-60 min` (English unit) and `Strength Focus` above them. The card also
repeats the plan name three times (heading, pill, plain label).
Evidence: `C24-today-loaded-light.png`.

### D45 — P2 — `Следующая фаза` commits an irreversible change on a single tap
**Reproduced in the running app.**
Repro: Cycle → `Следующая фаза` (the button under the day counter).
Expected: a confirmation or an undo, because the action writes a phase record for today and re-derives
the cycle.
Actual: one tap writes `2026-11-13 Ovulatory · Manual phase advance` and repaints the page as the new
phase; there is no confirm step, no toast and no undo. This also corrects D31 below: the screen that
followed was not a mislabelled forecast, it was the already-committed advance.
Evidence: `S19-cycle-dark.png` → `S23-nextphase-light.png` from the previous pass, and the row quoted
above; the write path is one tap in `AdvancePhaseCommand`.

### D46 — P1 — `Изменить приём пищи` on the history card never opens the editor
**Reproduced in the running app** (four attempts, three points, both after a fresh launch).

Repro: Nutrition → `Дневник питания` → scroll to `Завтрак · Scrambled Eggs with Toast` → tap
`Изменить приём пищи` (outlined button, centred at roughly 51 % × 63 % of the screen).
Expected: the meal editor opens for that meal, as the same labelled action does nowhere else in the app.
Actual: nothing opens. The tap is absorbed by the card and the list scrolls slightly instead (the header
card moves up ~40 pt between `M9` and `M11`). Taps at 80 %, 65 % and 63 % of the height all miss, while
the star 60 pt to the right at the same y *does* fire and write a template — so the card is not
inert wholesale, the edit control specifically is. The result is that a logged meal can be created and
deleted but never corrected from the history screen.
Evidence: `M5-foodlog-scrolled-light.png` (control at rest), `M9-edit-from-log-light.png`,
`M11-edit-exact-light.png` (after the exact-centre tap: no sheet, page scrolled), `M10-star-tap-light.png`
(the star firing at the same y, proving the region is live). Dark theme not reached because the sheet
never opens; the card is unchanged in dark from the earlier pass.

### D47 — P2 — Saving day notes reports success without validating or writing anything
**Reproduced in the running app.**

Repro: Food Log → scroll → leave the `Заметки` placeholder `То, что стоит запомнить об этом дне` → tap
`Сохранить заметки`.
Expected: either the action is disabled until the field is dirty and non-empty, or the first tap on an
untouched field focuses it, and a save with no content says what will be stored.
Actual: the toast `Заметки сохранены.` appears immediately, with the field still showing placeholder text
and nothing changed on the page. The placeholder doubles as the only explanation of the field and as a
value, so an empty save is indistinguishable from a real one. No write occurred (row counts unchanged),
which means the confirmation is simply untrue.
Evidence: `M5-foodlog-scrolled-light.png` (before), `M6-foodlog-meal-tap-light.png` (toast).

**Narrowed by the keyboard/persistence pass.** The untouched case above still stands — an empty field is
reported as saved. But a *typed* value does persist correctly end to end: `QA note keyboard test 14.11`
was written to `app.daily_nutrition_logs.notes` for 2026-11-13, survived navigating to Nutrition and back,
and reappeared in the field on reopen (matrix row 74). So the defect is the false confirmation on a
no-op, not a broken save path; and the field is the one text input on these screens whose keyboard
handling is correct.

### D48 — P2 — The meal-time field mixes a 12-hour clock, a 24-hour chip, and a double frame
**Reproduced in the running app.** **Fixed on `fix/qa-audit-followups`.**

Repro: Nutrition → `Добавить прием пищи`.
Expected: one localized time representation for the meal being created, in one input frame.
Actual: the `Время` field reads `7:15 PM` (English meridiem, no localized day period) while the summary
chip directly above the button reads `19:15` — the same time in two systems inside one 600 pt sheet. The
field is also drawn twice: an outlined rounded rect inside a second outlined rounded rect, the same
defect already recorded for the birthday field on Personal info (D24), now on a sheet.
Evidence: `M1-entry-sheet-light.png`. Dark counterpart: `D4-meal-entry-dark.png`.

**Root cause and fix (build pass).** The field bound `<TimePicker Format="t">`, the culture-short
pattern, so an `en-US` device locale printed `8:58 PM` while the chip used `:t` interpolation of the
same clock; `SelectedMealTimeText` did the same. Both pickers and both text builders now go through
`Services/Formatting/MealTime` (`HH:mm`, invariant), so the field, the chip, the meal card and the editor
header cannot disagree. `UiContractGuardTests.Meal_times_render_one_24_hour_clock_instead_of_the_culture_short_pattern`
fails if the culture-short pattern returns; it was red on all six sites before the change and is green now.
Commit `cd65f85d`. The double frame around the field is untouched and still open.

### D49 — P2 — Read-only value chips in the editor look exactly like the tappable chips before it
**Reproduced in the running app.**

Repro: `Добавить прием пищи` → `Добавить прием пищи`.
Expected: values that cannot be changed (meal type, day, time inside the editor) read as text or as a
labelled summary row; only the tappable controls look tappable.
Actual: `Ужин`, `Сегодня` and `19:15` are rendered as three pale rounded pills with the same height,
radius and padding as the meal-type choice cells one screen earlier, where the selected one carries a
filled state — so the editor's first row invites three taps that do nothing. The adjacent titles also
spell the same word two ways: `Добавить приём пищи` on the entry sheet and `Запишите прием пищи` on the
editor (`ё` present, then absent), plus `Тип приема пищи` in the entry sheet's own heading.
Evidence: `M1-entry-sheet-light.png`, `M2-editor-light.png`.

### D50 — P2 — Toast banners collide with the tab bar and clip their second line
**Closed and proved on screen** (Wave A close-out). Both hosts keep the `UiInsets20_0_20_72` clearance
token (the guard `Toast_hosts_clear_the_floating_tab_bar` fails if a host drops back to 20 pt), and both
were raised from real user actions in one cold launch each: `Сохранить заметки` on the food log
(`Заметки сохранены.`) and `Сохранить заметки и РПП` on a workout-history session
(`Заметки и РПП обновлены.`). Both banners sit above the tab bar and the sheet footer with their full
text visible, in light and dark. The entry that reaches the notes host — `Дневник питания` — was also
fixed: it is a real button in the `Еда` row and opens the diary on the first tap after a cold launch
(`diary-cold-single-tap-light.png`).

Repro: Food Log → tap `Сохранить заметки`, then tap the star on the meal card.
Expected: a banner above the tab bar with its full text and safe-area padding.
Actual: the banner is drawn at the bottom of the content, its rounded capsule overlapping the tab bar's
top edge, and the two-line template message (`Шаблон «Шаблон Завтрак (нояб. 13)» сохранён.`) is clipped by
the screen and sits on top of the meal card's text. Nothing in the sheet stack or the toast host reserves
space for the tab bar.
Evidence: `M6-foodlog-meal-tap-light.png`, `M10-star-tap-light.png`.

### D51 — P2 — The keyboard covers the food picker's results, and the sheet does not adapt
**Closed and proved on screen** (`7023dc81`, Wave A close-out). Three changes, all shared: the frame
shortens the sheet and keeps one scroll body; the editor collapses its header, source tabs and ingredient
list while the search field is focused; and typing now schedules a debounced search, so hits appear while
the keyboard is up instead of only after dismissing it. A Debug loopback build also falls back to
`DevelopmentFoodCatalog` when the local API answers empty, which is what gave the picker real rows.
Evidence: `picker-keyboard-hit-light.png`, `picker-keyboard-hit-dark.png` (typed `chicken`, `Поиск` and
`Chicken feet` / `Chicken skin` rows on screen with the keys up) and
`picker-first-result-selected-light.png`, `picker-first-result-selected-dark.png` (tapping the first row
lands on the portion sheet for `Chicken feet`, keyboard still up).
**Before:** `Q12-searchmode-light.png`, `Q14-results-dark.png` (no-match rows).
**Fixed and verified on screen** (`3b55255c` then `7023dc81`). Two changes were needed. The frame learned to
shorten itself by the keyboard overlap and to keep one scroll body; when that still left the result row
behind the keys, the food editor gained a **search mode**: while its field is focused it drops the header,
the source tabs and the ingredient list, which together cost more than the ~250 pt the sheet has above the
keyboard, and restores them on blur. Now, with a term typed and the keyboard up, the field, `Поиск` and the
result row are all on screen in light and dark. Evidence: `Q12-searchmode-light.png`,
`Q14-results-dark.png` (after) versus `N13-picker-keyboard-light.png`, `N15-picker-keyboard-dark.png`
(before). Guard: `Food_picker_collapses_the_rows_that_do_not_belong_to_the_search`, plus
`Modal_frame_reveals_the_content_below_the_focused_field` for the frame half.
**Limit of the evidence:** the simulator's food search returned no matches (no reachable catalogue), so the
visible row is the no-match state, not a hit. The row area, its position and its tappability are what the
screenshots prove.

Repro: Nutrition → `Добавить прием пищи` → `Добавить прием пищи` → `Добавить еду` → tap `Название продукта`
→ type `chicken`.
Expected: the sheet shrinks or scrolls so the match list sits above the keyboard, the way the notes card on
Food Log already behaves.
Actual: the header, tabs, field and `Поиск` stay put and only the results region is covered. In light the
empty-state sentence `Нет сохранённых. Попробуйте другое имя или создайте…` is cut horizontally through
its glyphs by the keyboard's top edge; in dark the same line is sliced the same way. Nothing scrolls: a
swipe while the keyboard is up does not move the sheet. The four matches (`Chicken feet`, `Chicken skin`,
`Chicken back`, `Chicken tail`) appear only after `Готово` is tapped, so a user typing a search sees no
results until they dismiss the keyboard — the exact opposite of what a search field is for.
Affected: `AddFoodSheet` (`Добавить еду`) and any `ModalSurface` sheet whose body is a list; the meal
editor and entry sheet have no text field, so they are unaffected. **This amends §12's shared-style
verdict rather than overturning it:** the sheets do share a frame, but the frame carries neither a
keyboard inset nor a scroll container, so the unification those sheets are one component away from would
first have to add both — see "What the keyboard pass adds to this verdict" at the end of §12.
Evidence: `N13-picker-keyboard-light.png`, `N14-picker-keyboard-dismissed-light.png`,
`N15-picker-keyboard-dark.png`, `N16-picker-keyboard-dismissed-dark.png`.

### D52 — P2 — The entry sheet cannot scroll, and `Использовать шаблон` is a silent no-op
**Reproduced in the running app**, dark, three separate launches. **Scroll half fixed** (`3b55255c`, seen in
`R4-entry-sheet-dark.png`): every sheet whose surface draws straight into `ModalSurface` now gets a scroll
body from the frame, and the frame keeps a 12 pt footer reserve underneath it, so the entry sheet's last
action clears the edge. **Still open:** the template action itself is unchanged and untested since the change.

Repro: Nutrition → `Добавить прием пищи` → swipe up inside the sheet; then tap `Использовать шаблон`.
Expected: the sheet scrolls when its content exceeds the visible height, and with no saved templates the
action either opens a sheet that says so or shows the `нет шаблонов` state already produced by the view
model (`Nutrition_MealEntry_NoTemplates` → `MealTemplateStatus`).
Actual: a swipe inside the sheet moves nothing (`N22` and `N25` are identical apart from the clock), and
tapping `Использовать шаблон` twice produces no sheet, no message and no scroll — the action is
indistinguishable from a dead control. The template status string is set by the view model but sits below
the third action, outside the reachable area, so the empty state exists in code and is unreachable in the
app. The same missing scroll container is why the sheet's last action sits under the home indicator.
Evidence: `N22-entry-sheet-dark.png`, `N23-template-sheet-dark.png`, `N24-template-list-dark.png`,
`N25-entry-sheet-scrolled-dark.png`.

### D54 — P2 — The meal card's heading and calorie block lost their separation after the primitives commit
**Reproduced in the running app** (build from `3a2daf1e`, light, 2026-11-13 21:33). **Fixed and verified on screen**
in light and dark (`f28aa3b1`): the header row now declares `JustifyContent="SpaceBetween"` and the heading
carries `FlexLayout.Grow="1"`, so the card reads `Завтрак … 0 ккал` with the figure at the trailing edge.
Evidence: `P6-foodlog-cards-light.png`, `Q9-foodlog-cards-dark.png` (after) versus `P2-before-tap.png`
(before). Guard: `Meal_card_header_pushes_the_calorie_block_to_the_trailing_edge`.

Repro: Nutrition → `Дневник питания` on a day with no logged meals.
Expected: the meal type and the calorie value are two separate cells of the card header, as in the audit
captures (`M3-foodlog-light.png`: `Завтрак` left, `582 ккал` right).
Actual: they are concatenated with no gap — `Завтрак0 ккал`, `Обед0 ккал` — on both empty cards, and the
calorie figure sits immediately after the word at the same weight. The card header row that the pending
`AdaptiveRow`/`MealCardView` work introduced (`3a2daf1e`, "Unify shared UI primitives") no longer reserves
the gap the previous `FlexLayout` + `HorizontalOptions=End` pair gave it. This is a regression against the
earlier captures, not an audit finding against the pre-fix build.
Evidence: `P2-before-tap.png` (this build) versus `M3-foodlog-light.png` (audit build).

### D53 — WITHDRAWN — the time field was re-initialised per open, not ticking
> **Status: withdrawn (build pass, reading the code against the captures).** The three captures that
> suggested a self-rewriting field (`8:58`, `8:59`, `9:00 PM`) came from three separate Maestro runs, each of
> which launches the app and then opens the sheet; `OpenMealEntry()` assigns
> `SelectedMealTime = DateTime.Now.TimeOfDay` exactly once per open. Neither the view model nor the XAML owns
> a timer, so there is no in-sheet tick to fix. What the captures show is the defect D48 records: the value
> is printed in a 12-hour pattern, which made two consecutive opens look like a mutation. No code change was
> made for D53; the number is kept so earlier rows and evidence names do not shift.
>
> The original observation follows, for the record, and is superseded by the note above.

**Original observation**, dark, three captures four minutes apart.

Repro: Nutrition → `Добавить прием пищи` → leave the sheet open and watch `Время`.
Expected: a time captured once when the sheet opens, changing only when the user picks another value.
Actual: the field re-renders the current clock each minute — `8:58 PM`, `8:59 PM`, `9:00 PM` — with no
user action and no visible indication that the value is "now" rather than a choice. A user who waits
before confirming silently logs the later time, and the same field is the one D48 records as showing a
12-hour clock beside a 24-hour chip.
Evidence: `N22-entry-sheet-dark.png`, `N24-template-list-dark.png`, `N25-entry-sheet-scrolled-dark.png`.

### Corrections to earlier entries (Cycle pass)

- **D31 — withdrawn.** The tap on `Следующая фаза` was not a preview; it advanced the phase and wrote
the record, so `Овуляторная: запись от нояб. 13` and the `СЕГОДНЯ В ВАШЕЙ ФАЗЕ` eyebrow were both
truthful at the moment they were captured. The real defect — an irreversible write behind a single
tap — is D45.
- **D34 — narrowed.** A later capture after the dashboard finished loading showed `582 ккал` on Today,
matching the breakdown, so the two surfaces read the same data. What remains is the state defect: while
that load runs, Today renders real-looking zeros (`Записано 0 ккал`, `0 g` macros, `Серия питания 0`)
instead of a loading state. Severity stays P2 for the fabricated zeros, not for a data-source mismatch.
Evidence: `D2-today-s2.png` (loading) versus `C24-today-loaded-light.png` (loaded).

## 7. Styling audit — every page and modal

Legend: ✔ compliant; ✖ violation of `DESIGN.md` / the presentation specification; ? not seen running
this pass (source-only judgement).

| Surface | Page frame | Card/surface use | Selection recipe | Status/state | Notes | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Today | ✔ shared header | ✖ rest notice nested inside rest card | — | ✖ rest-day fact three times | 34 pt greeting; readiness card title competes with page title | ✖ |
| Cycle | ✔ shared header | ✖ two tinted sub-cards inside hero | — | ✖ `День 14 из 28` twice, phase name twice | primary CTA wraps to two lines in a filled button | ✖ |
| Train | ✔ shared header | ✔ day card, week pills | ✖ text strip vs pill strip elsewhere | ✖ `Rest day` + `День отдыха` pill | clip at right, chips under tab bar, heavy archive button | ✖ |
| Nutrition | ✖ no header, bespoke pink hero | ✔ MealCardView | ✔ choice cells | ✖ empty state + summary redundancy | hazard: empty `Auto` row left in root grid | ✖ |
| Food Log | ✔ shared header | ✔ MealCardView | ✖ pill strip (different from Train) | ✖ empty groups state everything three times | water card as action shelf, rings vs bars | ✖ |
| Profile | ✔ shared header | ✔ stat rows with separators | ✔ phase pill | ✖ uppercase headings, `&` in RU | vault placeholder cramped, content under tab bar | ✖ |
| Profile subpages (7) | ✔ shared header on all | ✖ save bar floats over the card | ✖ units chips use their own selected recipe | ✖ raw `Maintain Health`, unitless measurements | see rows below | ✖ |
| Personal info | ✔ | ✖ double-framed date input | ✔ | ✖ sticky bar covers the cycle-length field | no units on height/weight | ✖ |
| Nutrition settings | ✔ | ✖ header squeezed to a 4-line column | — | ✖ bar covers the change-goals action | `Maintain Health` untranslated | ✖ |
| Units and display | ✔ | ✔ one card with separators | ✖ outline+tint selected vs filled elsewhere | ✖ bar covers `Энергия` | `imperial` inside a Russian sentence; `kg`/`g`/`cm`/`km` read as values | ✖ |
| Feedback | ✔ | ✔ | — | ✖ bar covers `Приоритет` | primary action is the pale disabled treatment | ✖ |
| Privacy | ✔ | ✔ | — | ✖ last card and its button cut by the bottom edge | — | ✖ |
| Photo vault | ✔ | ✔ | — | ✔ one-page empty state | add-photo primary action styled as secondary | ✖ |
| Workout archive | ✔ | ✔ totals card + session cards | ✖ month chips vs Train's text strip | ✖ `1800s`, `1 sets`, split label | debug-style footer line | ✖ |
| Cycle phase detail | ✔ | ✖ ~300 pt empty body in the guidance card | — | ✖ bilingual subtitle, clipped chart | — | ✖ |
| Activity chooser sheet | ✔ shared sheet | ✖ blank slab above the options | ✔ selected row | ✖ meta and progress overlap | sheet lingers after selection | ✖ |
| Session notes sheet | ✔ | ✔ | — | ✔ saved confirmation | field clears the keyboard | ✔ |
| Workout session (default text) | ✔ back + title, no page header | ✔ tabs, set grid, add-set row | ✔ current/recorded states | ✔ recorded banner at default size | only screen missing `PageHeader` besides Nutrition | ✖ |
| Workout session (large text) | ✔ | ✖ set grid disappears, banner overlaps the tabs | — | ✖ no set can be logged | exercises wrap into three lines | ✖ |
| Auth / onboarding (5) | ? | ? | ? | ? | not opened this pass | ? |
| Meal entry sheet | ✔ shared sheet | ✔ | ✔ filled meal pills | ✖ 12-hour time | three stacked full-width actions | ✖ |
| Meal suggestion sheet | ✔ shared sheet | ✔ | — | ✖ English rows, `Goal: Snack` | food content not localized | ✖ |
| Meal editor sheet | ✖ invisible to accessibility | ✖ huge blank body | ? | ✖ disabled commit unreadable | header back/close correct | ✖ |
| Food picker | ✖ invisible to accessibility | ✔ tabs | ✔ pill tabs | ? | empty state = centred sentence | ✖ |
| Nutrition breakdown | ✔ shared sheet | ✖ card per micronutrient | — | ✖ status written as a sentence | donut has no legend labels | ✖ |
| Daily check-in | ✔ shared sheet | ✖ cards inside sheet | ✔ 1–5 cells, but no default | ✖ footer clipped, disabled unexplained | slider value shows `—` | ✖ |
| Cycle sheets (4) | ? | ? | ? | ? | not opened this pass | ? |
| Injury log / crop / swap / history detail / plan preview | ? | ? | ? | ? | not opened this pass | ? |

What is already right and should be preserved: `MealCardView` is genuinely shared by Nutrition and Food
Log; `MealEditAction`, `AdaptiveRow`, `ModalTabBar` and `ModalSurface` exist as shared pieces; `ButtonPrimary`
in dark appearance correctly flips to a light fill with dark text; the five tab pages share one
background, gutter and type scale; WCAG-large text in the hero cards is legible in both appearances.

### The nutrition pair, specifically

The request asks whether Nutrition and the Food Log (the "history" page holding **Edit meal**) can be
unified. They already share the meal block; what is not shared is everything around it.

Reasoned proposal, smallest viable set:

1. **One page frame.** Delete the pink hero and mount `PageHeader` with `Title=Питание` and the same
   circular action slot. Keep the phase-focus card as content below it. That alone removes the second
   page skin and the dead `Auto` row.
2. **One summary block.** Extract the summary into a shared `NutritionSummaryView` used by Nutrition,
   Food Log and the breakdown sheet, with rings **or** bars — not both — and a single formatter for
   values/units (`32.8 g`, not `32g`/`33г`/`32,8 г`). Keep `MacroProgressGrid` as the only macro
   visual if the bars are chosen, or retire it in favour of the rings; either way one survives.
3. **One action anatomy per meal card.** The shared card should expose exactly three actions in a fixed
   order with fixed trailing alignment: **Edit meal**, save-template star, and the disclosure tap. Today
   the calorie column floats and the root description hides both actions; both are fixed inside the
   component, so both pages benefit at once.
4. **One empty-meal row.** Replace the 215 pt empty card with a compact row (`Обед · пусто · Добавить`)
   and one sentence at most; the three empty groups then read as a list instead of three walls.
5. **One day strip.** Promote `DayStripCell` (Food Log) to the shared day-selector for Train as well,
   and give both pages a bottom inset equal to the tab bar. The Train strip's English weekdays come
   from the same catalog gap as D4.
6. **One action shelf.** Move `Скопировать вчерашнее` and `Разбор питательных веществ` out of the water
   card into a single trailing row of equal secondary buttons, or into the page's overflow action.

Doing 1–4 closes D2, D3, D8, D12 (meal part) and most of D7 on these two screens without a new style
family and without touching persistence, routes or bindings.

## 8. Design-skill critique notes

Reviewed against `impeccable`'s Operate-mode criteria and the repository's own budget (one page frame,
two button styles, two card styles, one status treatment, state a fact once):

- **Modes and hierarchy.** Every screen is Operate: the next action must lead. Several screens lead
  with chrome instead — the Nutrition hero, the Train archive button above the plan, Today's readiness
  paragraph above its button. Fix hierarchy before colour.
- **The budget is being evaded structurally, not by new styles.** No third card style was added, yet a
  second page skin (Nutrition hero), nested sub-cards (Today, Cycle, check-in, breakdown), three
  different `ButtonSecondary` widths in one card (Food Log water) and two day-selector recipes achieve
  the same fragmentation the style budget exists to prevent. The gates check declarations, not
  composition; a structural gate over page roots, sheet footers and inline card-inside-card layouts is
  what would catch this class.
- **State and copy.** `impeccable`'s "one fact, one place" and the repository's status rule are the same
  rule; §D7 lists where it is broken. Fixing copy is cheaper than any layout change and removes most of
  the visual noise.
- **Motion.** Native transitions only, no decorative motion observed; nothing to change.
- **`high-end-visual-design` and `taste-skill`.** Both are web-surface skills whose directives are
  font substitution, glass/backdrop blur, doubled whitespace, custom cubic-bezier choreography and
  layout archetypes. Applying them here would breach the owner-approved Soft Minimal palette, the
  Open Sans alias enforced by tests, and the "motion explains state" rule. Their critique principles
  (one dominant action, generous grouping, precise alignment, no decorative weight) were used;
  their visual recipes were not, because the brief and the repository contract win.
- **`wizard`.** Present as `scripts/qa-simulator-wizard.sh`; it is an owner-driven walkthrough that
  needs a human to answer gates, so it was not run.

## 9. Blocked and untested workflows

| Workflow | Why |
| --- | --- |
| Meal editor and food search completion (add food, save meal) | Blocked by D1: the sheet exposes no accessible elements; only blind coordinate tapping was possible, and no result row could be verified |
| **Edit meal** from a meal card | Blocked by D46, now after three alternate entries were tried and failed. (1) Deep link: `xcrun simctl openurl booted "musclecuties://nutrition/history"` raised the iOS `Открыть в приложении` prompt and then left the app on Today — the scheme is registered only for the Whoop callback, no path router exists. (2) Saved-template route: `Использовать шаблон` never opens anything (D52), so no template can be picked or edited. (3) The same sheet from another owner: the main Nutrition page's card has no edit action, and the picker only edits food rows, not the meal. Every entry is either the dead D46 button or absent; the history editor was therefore never driven in either theme |
| Food picker result selection after a search | Blocked by D51 in the sense that results are only reachable after dismissing the keyboard; selecting a row (the serving/quantity sheet) was not driven this pass — the only text-driven path in the app remains behind the same keyboard cover |
| Finish-session summary and the session record | Blocked by D29: the weight banner removes the set grid and `Далее` stops responding, so the last exercise cannot be answered. The session row created by this pass was deleted during cleanup, so no completion write was observed |
| `Quick cycle check` order-conflict popup | Unreachable: no driven cycle edit produced an order conflict, because out-of-order edits are accepted silently (D39). Not claimed as tested |
| Cycle phase summary sheet | Opened only through the phase explorer in the earlier pass's routing; its cancel/confirm paths were not driven in this pass |
| Session abort from the tab bar / backgrounding | Not exercised; `Пропустить` and leaving the session mid-exercise were not driven to completion |
| iPad authenticated surfaces | The iPad build runs in compatibility mode and shows the login page; reaching the signed-in layout would need a sign-in, which this pass may not perform |
| Auth: sign-out, sign-in, registration, OTP, onboarding, quiz | Session was restored from cache; signing out would have destroyed the only working session on this simulator and no second account was prepared |
| Password recovery | Sends a real e-mail; needs owner approval |
| VoiceOver walk-through | Not run; the accessibility-tree defect (D1) still blocks the meal path, and the simulator pass was driven by automation |
| Larger or smaller viewport | **Only one viewport exists with a signed-in session:** the iPhone 17 Pro simulator, 402 × 874 pt. The iPad simulator runs this build in compatibility mode and stops on the login page (row 50), and the app keeps portrait geometry in landscape (row 49). "Smallest" and "largest" could not be produced, so every keyboard, sheet-height and scroll result in rows 71–81 is measured at that single size only |
| Keyboard navigation | No hardware keyboard attached in this session |
| Dark appearance for the **history** meal editor | Never reached in either theme, because its entry button does not respond (D46). The check-in, entry sheet, editor, picker and breakdown all have dark captures (`D1-checkin-dark.png`, `D4-meal-entry-dark.png`, `D5-editor-dark.png`, `D7-picker-dark.png`, `D8-breakdown-dark.png`), and the picker and notes field have dark keyboard captures (`N15`, `N16`, `N17`, `N18`) |
| Registration, contracts and server test suites | Not run this pass; only the Core and UI guard suites were executed |

## 10. Prioritized recommendations

**P0 — before any further styling work**

1. Fix D1 first. Add an accessibility-tree assertion to the UI gates (for every sheet: title, first
   field and footer action must be reachable via the tree). Remove `SemanticProperties.Description`
   from the meal card root or move it to the heading label so the edit and template actions are exposed.

**P1 — the release blockers that remain after D1**

2. Restore the shared `PageHeader` on Nutrition and delete the hero (D2); remove the empty root row.
3. One nutrition summary component and one formatter (D3, D28), applied to Nutrition, Food Log and breakdown.
4. Localize the macro labels at their source (`MacroProgressGrid`, `MacroBreakdownItem`) and add the
   missing catalog keys for `Rest Focus`, `Rest day`, `Snacks`, `Planned`, `Goal:`, weekday abbreviations
   (D4, D6).
5. Fix the check-in footer inside the safe area and always keep the commit action fully visible (D5).
6. Restore the Core suite (D16): add the missing policy file or repoint the test, then re-run.
7. Give every form a bottom inset equal to its sticky action bar — personal info, nutrition settings,
   units and feedback fail the same way today (D21).
8. Give the header/action row a wrap rule, since `Целевые макросы` becomes a four-line column at large
   text (D22), and fix the session screen's large-text layout so the set grid survives (D20).
9. Route the nutrition goal through the catalog; `Maintain Health` must never reach the UI (D25).
10. Fix the chooser sheet's overlapping row and make navigation wait for the dismissal (D19); rename the
    in-progress card's action (D18).

**P2 — consistency and state quality**

11. Meal-card alignment and action anatomy (D8); one empty-meal row and one nested-shell cleanup (D12).
12. One phase colour mapping (D9); one day-strip and filter recipe plus a tab-bar bottom inset (D11).
13. Explain disabled prerequisites and raise their contrast (D14); 24-hour time in Russian (D10).
14. De-duplicate the rest-day, readiness, cycle-day and vault copy (D7); fix the water-card action shelf
    (D13) and the `&`/uppercase heading drift (D15).
15. Archive formatting (D26), units copy and placeholder treatment (D23), personal-info field framing
    (D24), and the phase page's empty body and clipped chart (D27).

**P3 — coverage and process**

16. Add layout gates for structure, not only tokens: page root must contain `PageHeader`; every sheet
    footer must be inside the safe area; no `CardPrimary` inside `CardPrimary`; every form must have a
    bottom inset equal to its action bar; a card header row must survive the largest Dynamic Type size.
17. Re-run the untested matrix rows in §9 with a second account and a fresh training day, then complete
    the session finish/summary, the Cycle sheets and the remaining dark sheets.

**Added by the third pass (D29–D38)**

18. **P1 — unblock finishing a workout.** Keep the set grid in the layout when the validation banner is
    present, make the banner's copy agree with the rule it enforces, and keep the pinned actions from
    covering the fields they ask about (D29). This is the only defect that makes a core workflow
    impossible rather than unpleasant.
19. **P1 — persist an unset session** or state plainly that it was discarded (D30).
20. **P1 — put a confirmation or an undo behind `Следующая фаза`** (D45; D31 is withdrawn — there is no
    preview screen to relabel, the tap commits the advance).
21. **P2 — a real loading state for Today's numbers** (D34 as narrowed): show a skeleton rather than
    `0 ккал / 0 g / Серия 0`, and add a contract test that the loaded totals equal the breakdown's.
22. **P2 — quick log, archive detail, swap and check-in polish:** reserve room for the pinned bar (D32),
    unify date/unit formats in the archive (D33), disable the swap confirm until a choice exists (D35),
    and lift the Today check-in CTA above the tab bar (D37).
23. **P2 — one selection recipe and one formatter across the nutrition sheets** (D36, D38), as proposed
    in §12.
24. **P3 — decide the tablet story.** Either ship an iPad layout or declare the app iPhone-only in the
    store metadata and stop shipping a letterboxed build (the tablet row in §7); landscape is currently ignored.

**Added by the keyboard, persistence and entry-point pass (D51–D53)**

31. **P2 — give `ModalSurface` a keyboard inset and one scroll container** (D51, D52). This is the single
    change that closes both: every sheet then resizes or scrolls its body, the picker's results stay
    above the keyboard, and the entry sheet's third action stops sitting under the home indicator.
32. **P2 — make template absence visible.** `Использовать шаблон` should open its list and show the
    `нет шаблонов` state the view model already produces (D52), rather than doing nothing.
33. **P3 — freeze the meal time at open** and let only the picker change it (D53); if the intent is
    "now", say so with a chip and a `сейчас` label instead of a mutating field.

**Added by the modal pass (D46–D50)**

28. **P1 — make the history edit action real.** Wire `Изменить приём пищи` to the same editor the entry
    flow opens (D46); until it does, the only way to correct a meal is to delete and re-add it.
29. **P2 — one time control and one frame per input** (D48), one text style for every non-editable value
    (D49), and a notes save that is disabled while clean and states what it stores (D47).
30. **P2 — give the toast host a tab-bar inset and a two-line layout** (D50) so confirmations stop
    landing on top of the content and the bar.

**Added by the Cycle pass (D39–D45)**

25. **P1 — validate cycle order before writing.** `Сохранение фазы` currently accepts a follicular day between
    two menstrual ones and repaints a ten-day period (D39). Run the existing order check on the calendar
    edit path as well as the advance path, and show the conflict sheet before any write.
26. **P2 — make the calendar honest:** recorded days must drop the `*` and use the recorded swatch, and
    the legend must name the today marker (D40); future days must be either read-only or explicitly a
    forecast (D41); a save should confirm and offer undo, and abandoning an edit should clear the
    selection ring (D42).
27. **P2 — finish the Cycle localization and one phase recipe** (D43) and fix the Today metrics row that
    overlaps itself (D44); put a confirmation or an undo behind `Следующая фаза` (D45).

## 11. Design-policy audit — every page and modal, both themes

Judged against the repository's own budget (one page frame, two card styles, two button styles, one
status treatment, one selection recipe, state a fact once) and `impeccable`'s Operate-mode criteria
(scanability, one dominant action, native expectations). ✔ = shared design holds; ✖ = divergence;
— = not applicable. Every row was seen in the running app in at least one theme; "both" marks the ones
captured in light and dark.

| Surface | Theme seen | Type scale | Spacing / rhythm | Colour tokens | Cards / sheet | Buttons | Fields | Empty / status | Verdict |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Today | both | ✔ shared title/greeting | ✖ page scrolled under the fixed header (text clipped at the top edge) | ✔ | ✖ readiness nested card | ✖ `Вход` label, CTA under tab bar (D37) | — | ✖ three-sentence readiness block | ✖ |
| Cycle | both | ✔ | ✖ 500 pt hero block before the first action | ✖ follicular is pink in the hero and green one card below (D9) | ✖ nested tinted sub-card | ✖ next-phase CTA is a wide outline block | — | ✖ day repeated twice | ✖ |
| Train | dark | ✔ | ✖ text week strip clipped at `SA` | ✔ | ✔ | ✖ archive button above the plan | — | ✖ `training day - 6` (D6) | ✖ |
| Nutrition | both | ✔ | ✖ dead `Auto` row | ✖ second pink hero skin (D2) | ✔ shared meal card | ✔ | — | ✖ hero + summary + phase card repeat the day | ✖ |
| Food Log | both | ✔ | ✖ content under tab bar | ✔ | ✔ shared meal card | ✖ water card holds three actions (D13) | — | ✖ three empty cards say three things | ✖ |
| Profile | dark | ✔ | ✔ | ✖ phase pill colour differs from Cycle (D9) | ✔ stat rows | — | — | ✖ uppercase headings, `&` in Russian | ✖ |
| Personal info | both | ✔ | ✖ save bar covers the last field (D21) | ✔ | ✔ | ✔ | ✖ double-framed date, unitless height/weight (D24) | — | ✖ |
| Nutrition settings | both | ✖ section header collapses to a 4-line column (D22) | ✖ | ✔ | ✔ | ✔ | ✖ raw `Maintain Health` (D25) | — | ✖ |
| Units | both | ✔ | ✖ bar over `Энергия` | ✔ | ✔ | ✔ | ✖ placeholder-as-value units, `imperial` in Russian (D23) | — | ✖ |
| Feedback | light | ✔ | ✖ bar over `Приоритет` | ✔ | ✔ | ✖ primary action rendered in the disabled treatment | ✔ | ✖ textareas empty with one placeholder each | ✖ |
| Privacy | light | ✔ | ✖ last card cut by the bottom edge | ✔ | ✔ | ✔ | — | — | ✖ |
| Photo vault | light | ✔ | ✔ | ✔ | ✔ | ✖ add-photo primary styled secondary | — | ✖ three ways to say the preview is hidden | ✖ |
| Archive | dark | ✔ | ✖ `Продолжительно/сть` split; footer line under the tab bar | ✔ | ✔ | ✖ month chips look like filters but are the only control | — | ✖ `Все упражнения загружены` | ✖ |
| Archive detail | dark | ✔ | ✖ keyboard covers `ПОВТОРЫ`; save bar stacked on the keyboard toolbar | ✔ | ✔ | ✔ | ✖ weight `20,0` vs list `200 кг` | — | ✖ |
| Phase detail | light | ✔ | ✖ ~300 pt empty body | ✔ | ✔ | ✔ | — | ✖ bilingual subtitle, clipped chart (D27) | ✖ |
| Session | both | ✖ exercise title wraps into the progress tabs at large text; two `Назад` buttons | ✖ set fields under the pinned block (D29) | ✖ default iOS blue timer bar | ✔ tabs + set rows | ✖ four stacked full-width actions with equal weight | ✖ fields unreachable | ✖ error banner unreadable sentence | ✖ |
| Session notes | dark | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| Activity chooser | both | ✖ sheet title at display size | ✖ blank slab below the options | ✔ | ✖ | ✔ | — | ✖ overlapping meta (D19) | ✖ |
| Rest timer | dark | ✔ | ✖ three control widths | ✖ blue progress | ✖ replaces the page content | ✖ | — | ✖ error banner above it | ✖ |
| Pain sheet | dark | ✔ | ✔ | ✖ destructive action uses the accent fill | ✔ | ✖ | — | — | ✖ |
| Swap sheet | dark | ✔ | ✔ | ✔ | ✔ | ✖ confirm enabled with no choice (D35) | — | ✖ no selection state | ✖ |
| Meal entry sheet | both | ✔ | ✖ time field `6:39 PM` next to an `18:39` chip (D10) | ✔ | ✔ | ✔ | ✖ 12-hour time | — | ✖ |
| Meal editor | both | ✔ | ✖ 700 pt blank body | ✔ | ✔ | ✖ disabled commit unexplained (D14) | ✖ invalid Latin `P/C/F` units | ✖ one hint line for an empty editor | ✖ |
| Food picker | dark | ✔ | ✔ | ✖ solid selected tab vs outlined selected chip (D36) | ✔ | ✔ | ✔ | ✖ centred sentence in a blank sheet | ✖ |
| Breakdown | both | ✔ | ✖ two spacing regimes, no legend | ✔ | ✖ card per micronutrient | ✔ | ✖ four unit spellings (D38) | ✖ sentence as subtitle | ✖ |
| Check-in sheet | both | ✔ | ✖ footer sits on the last row | ✔ | ✖ cards inside the sheet | ✖ disabled save unexplained | ✖ slider default `—` | ✖ three empty lines | ✖ |
| Quick log | both | ✔ | ✖ save bar over symptoms (D32) | ✔ | ✔ | ✖ save always enabled | ✖ chips have no pressed feedback | — | ✖ |
| Cycle month calendar | both | ✔ | ✖ calendar card leaves a 100 pt gap when collapsed | ✖ four phase hues + an undefined dark-green “today” fill (D40) | ✔ | ✖ full-width outline `Показать календарь` | — | ✖ legend contradicts the grid | ✖ |
| Date/phase sheet | both | ✔ lowercase month/weekday | ✔ | ✔ | ✔ one sheet surface | ✔ save; ✖ no confirmation or undo (D42) | — | ✖ no success state | ✖ |
| Phase explorer card | both | ✔ | ✔ | ✖ four saturated bars | ✔ | — | — | ✖ `Исследователь 4-фазы`, `NOW` (D43) | ✖ |
| Auth / onboarding | — | ? | ? | ? | ? | ? | ? | ? | not reachable this pass |

**What genuinely is shared and should be kept:** the five tab pages sit on one background, gutter and
type scale; `PageHeader` (title + one circular action) is used by 21 pages; `ButtonPrimary` /
`ButtonSecondary` / `CardPrimary` / `CardSecondary` produce consistent light and dark fills; the meal
card, the day strip cells, `AdaptiveRow`, `ModalTabBar` and `ModalSurface` are real shared pieces; the
notes sheet is the one sheet that gets header, field, keyboard and footer right in both themes.

**The four systemic divergences behind almost every ✖ above** (each already has a defect number):

1. **Footer/scroll contract.** Sheets and forms pin an action bar but never reserve room for it, so the
   last control of nine surfaces is covered — check-in (D5), quick log (D32), session (D29), four
   Profile forms (D21), archive detail (D33), Today (D37). One shared `PinnedActionBar` that reports its
   height to the scroll container fixes the whole family at once.
2. **Header/action wrapping.** A card header placed beside a trailing action has no wrap rule, so
   `Целевые макросы` becomes a vertical sliver (D22) and `Продолжительность` splits mid-word (D26).
3. **Selection and status vocabulary.** Selected states alternate between filled, outlined and tinted
   across chips, tabs, day cells and meal types (D11, D23, D36); destructive actions borrow the accent
   fill (the pain sheet's `Остановить упражнение`); disabled actions are almost invisible and never
   explained (D14).
4. **Formatters.** Numbers, units, dates and times come from at least four places: `32g / 99g`,
   `33г`, `32,8 г`, `0,0mcg`, `1 sets`, `1800s`, `6:39 PM` vs `18:39`, `Чт, нояб. 12, 2026` vs
   `четверг, 12 ноября 2026 г.` (D3, D10, D26, D28, D33, D38). One quantity formatter and one date/time
   formatter, both localized, removes this class.

## 12. Nutrition page vs edit-meal — can they share one style?

Yes, and the evidence says they already share more than they do not. The meal block is the same
component (`MealCardView`) on both screens, and the edit action is a shared control (`MealEditAction`).
What differs is everything wrapped around it:

| Element | Nutrition page | Edit-meal flow (Nutrition → entry sheet → editor) | Verdict |
| --- | --- | --- | --- |
| Page/sheet frame | no `PageHeader`; bespoke pink hero with eyebrow dot, repeated title, heart button, dead `Auto` row | `ModalSurface` sheet with title + subtitle + close, three sheets with three header anatomies | ✖ two containers, three headers |
| Summary | horizontal macro bars, `32g / 99g` | `P 0,0 g / C 0,0 g / F 0,0 g` in the editor; donut + bars + `Protein/Carbs/Fats` in the breakdown | ✖ three visual models, four unit styles |
| Primary action | heart icon button (no label) | `Добавить прием пищи` (filled) then `Приём пищи` (disabled, unexplained) | ✖ icon-only vs two labels |
| Empty state | phase card + repeated day | one grey line under a large blank body | ✖ |
| Selection | none on the page | meal-type chips: outlined-selected | ✖ definition exists only here |

**Recommendation (no code changes made).** Unify on the sheet's anatomy, because it is the newer and
better-specified of the two, and let the page inherit it:

1. Mount `PageHeader` on Nutrition (`Питание` + the same circular action slot) and delete the hero and
   its empty `Auto` row. The phase-focus card becomes ordinary content under the header.
2. Promote the editor's title/subtitle/close header into the single `ModalSurface` variant and use it for
   the entry sheet, editor, picker and breakdown; drop the second close/back pairing in the picker.
3. Extract one `NutritionSummaryView` (rings **or** bars, one choice) used by the page, the editor's
   header, the breakdown sheet and `MealCardView`; move every number through one quantity formatter
   (dot decimal, `г`, `мг`, `мкг`, space before the unit).
4. Give the page the same primary action the flow uses: a labelled `Добавить приём пищи` button in the
   header slot rather than an unlabelled heart, and keep the editor's commit label identical.
5. Use one selection recipe (the filled chip already used by the day strip and meal-type chips) for meal
types, picker tabs and macro toggles alike, and one disabled treatment that states its prerequisite.
6. Replace the editor's blank body with the same empty-state line the picker uses, so both sheets read as
   one system.

Doing 1–3 alone closes D2, D3, D28, D34 (presentation half) and D38 and removes the "second product"
impression; 4–6 close D14, D36 and the empty-state duplication. None of it needs a new style family,
a new route or a persistence change — it is composition of components that already exist.

### Sheet-by-sheet verdict (modal pass)

Measured against the same four tokens on every sheet — 24 pt outer radius on the `ModalSurface`, a
48 pt circular close at `MutedSurface`, title 24 pt / subtitle 15 pt muted, and one filled primary plus a
pale secondary footer — the sheets split three ways:

**Can share one style today (same anatomy, only copy differs).** The meal entry sheet, the meal editor and
the supplement/quick-add sheets already use the same header (title, muted subtitle, circular X), the same
card radius and the same footer pair. They differ only in how many stacked actions they place under the
header (three on the entry sheet, two in the editor), so a single `ModalSurface` variant with a declared
"1 primary + N secondary" slot would absorb all three without redesign.

**Share the frame but not the content recipe.** The food picker and the macro breakdown sheet both sit in
the correct surface, but neither uses the sheet header: the picker adds a second close/back pairing next
to the search field, and the breakdown opens straight onto a donut with no title at all. Both keep the
sheet's 24 pt radius and pink tokens; what blocks unification is the missing title/subtitle slot and, in
the picker, a duplicate dismissal affordance (D28, D6).

**Should stay distinct.** The cycle date/phase sheet and the cycle phase summary are legitimately a
different species: they are *choice* surfaces whose selected state is the whole point, so they carry the
filled selection chip and a taller content area. What they must not carry is their own selection colour —
their selected pill is the accent fill while the meal-type and unit chips use outline+tint (D11, D36);
once those two chips move onto one recipe, the cycle sheets can keep their height and still belong.

Specific divergences that block a single style today, each already recorded: three header anatomies and a
duplicate close (D28); three unit formats inside the same flow (D3, D38); two selection recipes for the
same concept (D11, D36); a 12-hour field beside a 24-hour chip in one sheet (D48); inert value pills that
reuse the tappable recipe (D49); and a toast host that ignores the tab bar (D50). None of them is a reason
to design a fourth sheet — they are six token decisions applied inconsistently.

**What the keyboard pass adds to this verdict.** The frame is unified but its *behaviour* is not: the notes
card on Food Log handles the keyboard correctly (scrolls the card above it, keeps the save action
reachable, retains the text when dismissed), while the picker sheet does not (D51) — its body never
resizes, so the results are covered. The entry sheet has no scroll container at all, so its third action
is clipped and the template state below it is unreachable (D52). The conclusion is not "design a fourth
sheet": it is that `ModalSurface` needs the two things the page already does — a keyboard inset and a
scrollable body — before the shared style can be applied to every sheet. With those in place, the entry
sheet, editor and picker become one component rather than three near-copies.

## 13. Report path and main risks

Report: `QA_REPORT.md` (repository root). Evidence: `/tmp/mc-qa-2026-11-13/shots/` and
`/tmp/mc-qa-2026-11-13/flows/` on this machine; the flow files make each captured step reproducible.

Main risks if this build ships:

- **Assistive-technology exclusion (P0).** A VoiceOver user cannot log a meal at all, and the camera-off
  path is not a workaround because every control in the sheet is invisible to the accessibility layer.
- **A second page skin (P1).** Nutrition is the primary daily surface; its bespoke hero makes the app
  read as two products and will keep attracting one-off fixes until it is removed.
- **An edit action that does nothing (P1).** `Изменить приём пищи` on the history card is drawn, is the
  obvious way to fix a wrong meal, and takes no tap (D46). A user correcting a mistake finds the app
  silently refusing, with no error and no alternative on that screen.
- **Data presentation drift (P1).** Three unit/number formats and two macro visualisations for the same
  numbers invite wrong readings of intake against target.
- **A picker that hides its own results (P2).** Typing a search and seeing nothing until the keyboard is
dismissed makes the one text-driven path in the app feel broken (D51), and the same missing scroll
container clips the entry sheet's last action (D52).
- **Unlocalized core copy (P1).** Macro names and rest-day strings are the most-read labels in the app.
- **A red release suite (P1).** The missing policy file keeps the Core gate red, so genuine regressions
  would be indistinguishable from the known failure.
- **Forms hiding their last field (P1).** Every Profile form ends under its own save bar, and the session
  screen loses its set grid at large text, so the two places where a user enters data are the two places
  that break at the sizes accessibility users actually pick.
- **A session that cannot be finished (P1).** The weight banner hides the very fields it asks for and
  `Далее` stops responding; a user who taps through once without typing a weight reaches a dead end with
  no route to the summary (D29), and a relaunch before the first set silently discards the session (D30).
- **Fabricated zeros (P2).** While Today's intake loads it prints `0 ккал` and `0 g` as if they were values,
  so the first seconds of every visit read as a lost day (D34, narrowed to the loading state). The data
  itself agrees across surfaces once loaded (row 51).
- **No tablet or landscape experience.** The iPad runs the phone build letterboxed on black and the app
  ignores landscape, so the audit cannot certify any larger-screen experience.
