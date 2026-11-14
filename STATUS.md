# Status — Wave A close-out

Branch: `fix/qa-audit-followups`. Nothing pushed; no PR opened.

## Commits in this pass

- `6f520353` — Use the shared page header on Nutrition (D2).
- `6eb6d92e` — Let the macro-targets header stack beside its action (D22).
- `272958c1` — Use one 24-hour time field without native picker chrome (D48; birthday half of D24).
- `c8c94888` — Use the shared sheet header on check-in and breakdown (D36 adoption).
- `2c8a3dcc` — Guard the template empty state and workout metrics (D52, D44).

The earlier rest-card fix `cd9b544f` was not redone.

## What changed

- `NutritionPage.xaml` composes `sharedControls:PageHeader` in the root `Auto,*` row. The empty
  header row and the bespoke hero are gone.
- `ProfileNutritionSettingsPage.xaml` puts the macro-targets title and action in
  `AdaptiveRow Columns="2"`; at an accessibility text size they stack instead of squeezing the title.
- New `Controls/Shared/TimeField.xaml` / `.xaml.cs` owns one `InputContainer` frame, a fixed 24-hour
  `HH:mm` format and a two-way time value. Adopted by `MealEntryModal`, `MealEditorModal` and all
  three `HydrationLogView` time controls. No timer refreshes the value.
- `PlatformCompositionExtensions` clears the native `UITextField.BorderStyle` on iOS `DatePicker` and
  `TimePicker`, removing the concentric inner frame from both the birthday and meal-time fields.
- `DailyCheckInModal` and `NutritionBreakdownModal` now use `sharedControls:ModalSheetHeader` instead
  of hand-built title rows. Entry/editor/picker already used it.
- `WaveAWiringGuardTests.cs` pins the page header, adaptive macro row, shared time field/native chrome,
  shared sheet headers, template empty state and dashboard adaptive metric row.

## Verification

- Guard subset: 58 passed, 0 failed.
- Full `MuscleCuties.Core.Tests`: 1415 passed, 0 failed.
- iOS simulator build: 0 warnings, 0 errors.
- Simulator evidence under `docs/qa-evidence/wave-a/`, each captured light/dark at normal and
  `accessibility-large`:
  - `d2-nutrition-header-*`
  - `d22-macro-header-*`
  - `d24-birthday-field-*`
  - `d48-entry-time-*` and `d48-entry-time-template-*`
  - `d36-checkin-header-*` and `d36-breakdown-header-*`
  - `d52-entry-template-empty-*` (the template action was tapped and the no-templates state was
    visible at all four combinations).

## Limitations

- D44's adaptive metric row was already in the branch when this pass began, so its new guard was green
  on arrival rather than red-before/green-after. Today is a rest day on the QA account, and changing
  the account's plan was out of scope, so no training-day after frame was captured. The earlier
  `C24-today-loaded-light.png` remains the only dashboard evidence.
- D24 is only partly closed: the birthday frame is fixed, but `Рост 165` / `Вес 62` still have no
  unit and `Личные данные` still reads as a muted caption.
- D52 was driven with an empty template catalog. Selecting or editing a saved template was not
  exercised.
- The simulator was left in the last evidence state (light, `accessibility-large`) until the final
  reset to light/`large`.
