# STATUS

Report: **`QA_REPORT.md`** (project root, untracked). This file is the handoff for the same pass.

## 1. Changed files

- `QA_REPORT.md` — the audit report; new file, not committed.
- `STATUS.md` — this handoff.
- No product source, XAML, HTTP contract, migration or test file was edited. `git status --short`:
  ` M STATUS.md` and `?? QA_REPORT.md`.

## 2. What was run, with output

Build and unit suites were **not re-run in this pass**; the last recorded result for this commit is in
§2 of `QA_REPORT.md` and is not restated as current. Everything below is this pass.

Control coverage: **98 rows — 57 PASS, 17 PARTIAL, 7 FAIL, 11 NOT RUN, 5 BLOCKED, 1 not a control.**

Session, driven to its summary and finished (flows 140–151, screenshots `200`–`224`):

    Tap on "Начать тренировку"... COMPLETED
    Tap on "Strength & Sculpt"... COMPLETED
    tap kg=20 first, then "Далее" → "Первая запись: Machine Chest Press. Отличное начало!" (shots/210–211)
    Tap on "Пропустить" ×2, then "Далее" → summary "Записано 2 из 5 · пропущено: 3" (shots/215)
    Tap on "Завершить сеанс"... COMPLETED → back on Train, card still "Начать тренировку"

    psql before repair: 2 workout_session_logs rows, completed_at NULL, confirmed_active_seconds 548 / 0
    psql after repair:  UPDATE 2 → completed_at = started_at + 9m08s for both
    cold restart → Today "Тренировка завершена / Завершено сегодня / Открыть тренировку" (shots/221)
    Train still "Начать тренировку", no completion state (shots/222)

Plan restore:

    UPDATE app.workout_days d SET date=b.date, day_of_week=b.day_of_week FROM app.qa_backup_workout_days_20261112 b WHERE d.id=b.id;
    UPDATE 24
    rows_still_shifted = 0
    Train after cold restart: "THU · день отдыха / Rest day" (shots/224)

Remaining pages reached this pass (screenshots `230`–`279`):

    Цикл → quick-log sheet opened; Спазмы toggled; closed without saving (shots/231–233)
    Цикл gear → "Личная информация", birth date renders 15.03.1996 (shots/235)
    Питание → Добавить прием пищи → Использовать шаблон → inline template list, empty state (shots/237)
    → Добавить еду → "курица" → "Нет совпадений" (shots/242); "chicken" → 2 catalog rows (shots/243)
    → result row → "Portion" sheet → Добавить ингредиент → 143 kcal in editor (shots/244–245)
    → Отмена → Nutrition unchanged, nothing persisted (shots/246–247)
    → Добавить еду → Штрихкод tab (shots/252); barcode 7350053850019 → Найти → "не найден … добавьте вручную" (shots/258)
    → Своё tab (shots/253); name + 99 kcal → Сохранить → row created in app.user_food_items (shots/259–260)

Cycle phase, created safely through the app (screenshots `261`–`275`):

    Цикл → Быстрая запись → symptom "Тяга к еде" → Сохранить запись
    validation on empty form: "Выберите отметку цикла, интенсивность или симптом перед сохранением." (shots/265)
    → "Отметить начало менструации" → app.cycle_logs row (start 2026-11-12, cycle_length 0)
    → page reads "Менструальная фаза / День 1 из 28" (shots/269)
    → "Следующая фаза" → "Фолликулярная фаза", still "День 1 из 28"; app.cycle_phase_logs gains
      note "Manual phase advance"; app.user_profiles.current_cycle_phase = Follicular (shots/270)
    → tap phase title (shots/271), tap phase tile (shots/272) → nothing happens

    grep -rn 'OpenPhaseDetailsCommand' src --include=*.xaml → (no match)
    grep -c 'Command=' Pages/Cycle/CyclePage.xaml → 1   (OpenCycleSettingsCommand only)
    → CyclePhaseDetailPage + phase summary/warning/date sheets have no entry point: BLOCKED in code, not for data

Cleanup, verified:

    DELETE symptom_logs (1) → DELETE cycle_phase_logs (1) → DELETE cycle_logs (1)
    DELETE app.user_food_items WHERE name='QA Custom Food' → DELETE 1
    UPDATE app.user_profiles SET current_cycle_phase=NULL → UPDATE 1
    cycle_logs 0 | cycle_phase_logs 0 | symptom_logs 0 | food_items 1 (pre-existing) | open sessions 0 | quiz 17
    cold restart → Цикл "Неизвестно фаза / Даты менструации не записаны" (shots/275)
    You → Выйти → native dialog "Вы уверены, что хотите выйти?" → Отмена, session kept (shots/276–279)

Quiz attempt:

    UPDATE app.user_profiles SET date_of_birth='1990-01-01' → SELECT confirms 1990-01-01
    You → Личная информация → "Сохранить личную информацию" → returns to You (shots/226)
    cold restart → Today (shots/227)
    date_of_birth restored to 1996-03-15; quiz rows 17

## 3. Not verified, and why

- **Quiz — BLOCKED, not untested.** Seeded a synthetic birth date, saved the profile through the app,
  and restarted: every path lands on Today. The only call site for `//QuizPage` is the callback on
  `ProfileSetupViewModel` (`MauiProgram.cs:159-161`), which only exists inside registration; the
  signed-in profile page uses another view model, and registration itself is blocked by D1. Clearing
  the 17 quiz rows (last pass) also did not route there. 8 question screens, 0 exercised.
- **Ten controls inside reachable screens** were intentionally not tapped; each has its reason in §10:
  nutrition goal picker and its save, vault add-photo (Photos access + write), pain modal's
  replace/stop, swap-panel close, chooser close, note-sheet explicit save (autosave already persisted),
  check-in × and weight field, "Получить совет" (no accessible label).
- **Cycle detail surfaces — BLOCKED, and no longer for lack of a phase.** A phase was created live and
  the app still offers no way in: `OpenPhaseDetailsCommand` is unbound in XAML (`CyclePage.xaml` has one
  `Command=` binding). More test data cannot unlock it (D34, row 97).
- **Password recovery — BLOCKED on approval, not on data.** Reachable only from a signed-out Login screen
  and submitting an address sends a real reset email; not attempted (row 98).
- **Barcode camera scan** — the simulator has no camera; the numeric path was tested (row 90).
- Responsiveness at tablet/desktop, VoiceOver focus order and contrast ratios remain unverified — §11.

## 4. Next step or blocker

**D34 is the most structural new finding**: a whole registered page plus four cycle sheets have no
binding, so a user can never see phase detail. **D26 remains the release blocker**: a finished session must persist `completed_at` once, without the
duplicate `workout_session_logs`/`workout_logs` rows, and both Today and Train must show the same
terminal state. The QA account was left terminal by writing `completed_at` in the QA database, which is
a repair, not a product behaviour — the fix must be in the client/server path. Then D25 (accept 0 kg or
stop advertising it, and never hide the fields an error names), D32 (Train card ignores completion), and
D1 (blocks all eight Quiz screens and all in-app registration).
