# Remaining Work — Frontend (MAUI iOS)

## 1. Maestro Full UX, Styling & Scenario Review (Current Build)
*(Equipment selector IDs in the 3-option catalog: `QuizAnswerEquipment1` = Full gym, `QuizAnswerEquipment2` = Home Dumbbells & Bands, `QuizAnswerEquipment3` = Bodyweight only.)*

- [ ] **Russian (`ru_RU`) full-gym E2E (`QuizAnswerEquipment1`):** Re-verify end-to-end journey on the latest localization & `WorkoutSessionPage` build (localization catalog, quiz answers, program titles, exercise names, and effort terminology fixes have landed; simulator replay still pending).
- [ ] **Home Dumbbells + Bands (`QuizAnswerEquipment2`) E2E:** Re-verify full journey completion + open-keyboard value persistence on simulator (weight parsing, decimal separator handling, calibration-weight clearing, and unilateral set-input fixes have landed in `ExerciseSetInputItem.cs`, `WorkoutExerciseItem.cs`, and `WorkoutSessionViewModel.cs`).
- [ ] **Account-local Workout History date verification:** Re-verify on simulator that archive completion dates display the account's local calendar date rather than UTC (account-timezone queries, `AccountLocalStartedAt`, and `SessionDateConverter` fixes have landed with integration coverage in `WorkoutArchiveCalendarTests.cs`).
- [x] **English Bodyweight-only (`QuizAnswerEquipment3`) E2E:** Passed (`exit 0`, all 25 screenshots reviewed).

## 2. UI Polish & Activity Preferences Enhancements
- [x] **Workout activity preference descriptions:** Refined EN/RU descriptions across workout activity preferences (`WorkoutActivityOptionCatalog.cs`, `UiTextCatalog.RuntimeText.cs`).
- [x] **Per-activity SVG icons:** Configured `BaseSize="24,24"` for all 16 `activity_*.svg` assets in `MuscleCuties.UI.csproj` and wired dedicated per-activity icons with theme-aware tinting (`TextPrimary` / `TextPrimaryDark`) in `WorkoutActivityOptionItem.cs`, `WorkoutActivityOptionCatalog.cs`, and `WorkoutActivityPreferencePicker.xaml`, guarded by `LocalizationCatalogTests.Every_workout_activity_option_maps_to_a_unique_svg_asset_and_russian_copy`.

## 3. Pre-Existing UI / Accessibility Items Outside `s26.yaml`
- [x] **Nutrition sheets & accessibility tree (`D1`, `D46`, `D51`, `D52`):** Closed in commits `3b55255c`, `7023dc81`, `f43e251d`, and `56ccd240` (`MealCardView.xaml`, `AnimatedModalView.cs`, `MealEntryModal.xaml`, guarded in `UiContractGuardTests.cs` and `WaveAWiringGuardTests.cs`); pending full simulator regression sweep.
- [ ] **Dark Mode & Dynamic Type (`accessibility-large`):** Run a full-tab visual regression pass in Dark appearance and at `accessibility-large` text size on the latest build (including the newly wired per-activity SVG icons).
- [ ] **Password recovery flow:** End-to-end verification of password reset email flow (requires owner approval for real email delivery).
