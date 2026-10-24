# UI Dead-Code, Unreachable-Route & Covered-Element Cleanup Plan

## 1. Objective & Architectural Guardrails

Clean and organize `src/MuscleCuties.UI` by systematically auditing and removing or reconciling:
1. **Unused UI (`0` references in `.xaml` and `.cs`):** Controls, Pages, ViewModels, UI Models (`Models/UI/*`), Converters (`Resources/Converters/*`), and dead ViewModel properties/commands.
2. **Used-in-Code but Unreachable UI:** Pages or modals that exist in C#/DI (`MauiProgram.cs` / `AppShell.xaml.cs`) whose entry points were retired during the UI Workflow Audit (e.g., retired menu items, removed buttons, or unregistered routes).
3. **Covered / Permanently Hidden UI:** XAML blocks that are hardcoded `IsVisible="False"` with no dynamic binding/trigger, shadowed duplicate templates, or legacy blocks superseded by merged cards.

### Mandatory Safety Gate (`AGENTS.md` Rule 4.2 — XAML Protection)
- **Before deleting or renaming ANY `.xaml` control/page, C# property, `[ObservableProperty]`, `[RelayCommand]`, `Models/UI/*` class, or `IValueConverter`:**
  1. Search (`grep -rn`) across **all** `.xaml` and `.cs` files in `src/MuscleCuties.UI` and `tests/` to verify whether it has runtime XAML bindings (`Binding`, `x:DataType`, `x:Type`, `StaticResource`, `DataTemplate`, `xmlns`).
  2. Distinguish between:
     - **Intentionally Gated Features (Keep & Document):** UI elements gated by a policy/review flag documented in `UI_WORKFLOW_AUDIT.md` (such as the Cycle detailed phase guide or Readiness score/suggestion flags) must be preserved if bound to a ViewModel gate property, or cleanly wired to their gate.
     - **Truly Dead / Retired UI (Remove Cleanly):** Controls, pages, models, converters, and partial ViewModel files left behind after owner-approved UI removals in `docs/v1/UI_WORKFLOW_AUDIT.md`.

---

## 2. Target Audit & Cleanup Inventory (`UIC01`–`UIC05`)

### Block `UIC01`: Unreferenced & Retired XAML Controls (`src/MuscleCuties.UI/Controls/`)
Audit every `.xaml` control under `src/MuscleCuties.UI/Controls/` for XAML tag usage (`<controls:...`, `<cycle:...`, `<dashboard:...`, `<nutrition:...`, `<profile:...`, `<workout:...`, `<shared:...`) and C# instantiation:
- **Dashboard Controls:**
  - `Controls/Dashboard/DashboardCalendarCard.xaml` (+ `.xaml.cs`): The Today calendar card was explicitly removed per `UI_WORKFLOW_AUDIT.md` ("Remove the Today calendar card"). Verified 0 XAML/C# usages; removed `DashboardCalendarCard.xaml(.cs)`, dependent `DashboardMonthGrid.cs`, `ViewModels/Dashboard/DashboardViewModel.Calendar.cs`, and `Models/UI/Dashboard/DashboardCalendarDay.cs`.
- **Cycle Controls:**
  - Audited `Controls/Cycle/CycleDailyTipsCard.xaml`, `CycleHeroCard.xaml`, `CyclePhaseSummarySheet.xaml`, `CyclePhaseExplorerCarousel.xaml`, `CycleCalendarCard.xaml`, `CycleDatePhaseModal.xaml`, `CycleQuickLogBottomSheet.xaml`, `CycleWarningPopup.xaml`. Verified that `CycleHeroCard.xaml` embeds `CycleDailyTipsCard.xaml`; `CyclePhaseExplorerCarousel.xaml` is the stacked 4-phase rows control; `CyclePhaseSummarySheet.xaml` displays `SelectedPhaseSummary`; `CycleCalendarCard.xaml`, `CycleDatePhaseModal.xaml`, `CycleQuickLogBottomSheet.xaml`, and `CycleWarningPopup.xaml` are actively referenced and lazy-loaded in `CyclePage.xaml(.cs)`. All preserved per XAML protection gate.
- **Nutrition Controls:**
  - `Controls/Nutrition/NutritionBreakdownModal.xaml` (+ `.xaml.cs`): Reachability verified; actively invoked by `OpenMealBreakdownCommand` when tapping any logged meal row in `NutritionPage.xaml`. Other controls (`MealDetailModal.xaml`, `MealEditorModal.xaml`, `MealEntryModal.xaml`, `MealSuggestionModal.xaml`, `NutritionBalanceCard.xaml`, `NutritionPhaseFocusCard.xaml`, `QuickFoodItemRow.xaml`) are actively referenced. All preserved.
- **Profile & Workout Controls:**
  - `Controls/Profile/ProfileDetailsContent.xaml` is actively lazy-loaded in `ProfilePage.xaml` (`DetailsLazy`). `ProfileImageCropModal.xaml`, `ProfileWorkoutPreferencesChoices.xaml`, `FeaturedWorkoutCard.xaml`, `WorkoutFeaturedSection.xaml`, `WorkoutActivityCard.xaml`, and `WorkoutSwapPanel.xaml` are actively referenced and loaded.
- **Status:** Completed (2026-10-24) — Removed retired `DashboardCalendarCard.xaml` (+ `.xaml.cs`) and dependent `DashboardMonthGrid.cs`. Preserved all active and policy-gated controls per XAML Protection Gate (`AGENTS.md` Rule 4.2).

### Block `UIC02`: Unreachable Pages, Shell Routes & DI Registrations (`Pages/`, `AppShell`, `MauiProgram`)
Trace every page in `src/MuscleCuties.UI/Pages/` from `AppShell.xaml`, `AppShell.xaml.cs` (`RegisterRoutesOnce`), and all navigation calls (`GoToAsync`, `PushAsync`, `PushModalAsync`):
- **`Pages/Profile/InjuryLogPage.xaml` (+ `.xaml.cs`):**
  - The "Injury Log" row was removed from the You (`ProfilePage`) menu per `UI_WORKFLOW_AUDIT.md`, and Today/Train use `InjuryLogModal` (`Controls/Injury/InjuryLogModal.xaml`). Removed `InjuryLogPage.xaml(.cs)`, its route registration in `AppShell.xaml.cs`, and its page DI registration in `MauiProgram.cs`. Redirected `WorkoutViewModel.NavigateToInjuryLogCommand` to in-page `OpenInjuryModalAsync` while preserving `InjuryLogModal`, `InjuryLogContent`, and `InjuryLogViewModel`.
- **`Pages/Workout/PhaseAdjustmentReviewPageV010.xaml` (+ `.xaml.cs`):**
  - Registered route in `AppShell.xaml.cs` (`RegisterRoutesOnce`) and added DI registrations in `MauiProgram.cs` (`IPhaseAdjustmentProposalClientV010`, `PhaseAdjustmentReviewViewModelV010`, `PhaseAdjustmentReviewPageV010`).
- **`Pages/Cycle/CyclePhaseDetailPage.xaml` (+ `.xaml.cs`):**
  - Verified as intentionally gated behind the detailed phase guide review flag in `CycleViewModel`; route cleanly maintained and documented.
- **Status:** Completed (2026-10-24) — Removed unreachable `InjuryLogPage` and registered `PhaseAdjustmentReviewPageV010` in `AppShell.xaml.cs` and `MauiProgram.cs`.

### Block `UIC03`: Covered, Overlapped, or Permanently Hidden XAML Elements (`Pages/*.xaml` & `Controls/*.xaml`)
Scan all active `.xaml` files for:
- Hardcoded `IsVisible="False"` attributes: Verified all are dynamic runtime toggles (`PhaseSaluteOverlay`, `ProfileImageCropModal`, `AppStartupPage` error/recovery elements, `WorkoutPage.SessionActivitiesOverlay`, `ProfileSetupPage.AcknowledgmentError`).
- Duplicate/shadowed layout containers or dead `Grid`/`Border` blocks covered by z-order siblings.
- Cleaned unused `xmlns:mi` namespace in `ProfilePage.xaml` and `xmlns:dtos` in `WorkoutHistoryPage.xaml`.
- **Status:** Completed (2026-10-24) — Verified zero dead or permanently hidden covered containers; cleaned unused `xmlns:` declarations across `ProfilePage.xaml` and `WorkoutHistoryPage.xaml`.

### Block `UIC04`: Unused UI Models (`Models/UI/`), Converters (`Resources/Converters/`), Services, & Dead ViewModel Members
- **`Models/UI/*`:** Removed orphaned `Models/UI/Dashboard/DashboardCalendarDay.cs` and `Models/UI/Profile/PreferenceItem.cs`. Verified all remaining UI models (`PhaseExplorerPillItem`, `PhaseSummarySheetModel`, `FoodServingOptionItem`, `WorkoutSessionDetail`, etc.) have active bindings/references.
- **`Resources/Converters/*`:** Removed unused converter `ScoreToColorConverter.cs` and its registration in `App.xaml`. Verified all other converters (`InvertedBoolConverter`, `CyclePhaseToBrushConverter`, `CyclePhaseToTextColorConverter`, `FoodLogConverters`, etc.) have active registrations and usages.
- **ViewModel Dead Members:** Removed orphaned partial `DashboardViewModel.Calendar.cs` along with dead fields `_calendarCycleStart` and `_calendarPredictionSource` in `DashboardViewModel.cs`. Removed unused `_preferences` and `OpenPreferenceCommand` from `ProfileViewModel.cs`.
- **Status:** Completed (2026-10-24) — Removed orphaned `DashboardCalendarDay`, `PreferenceItem`, `ScoreToColorConverter` (and registration in `App.xaml`), and orphaned `DashboardViewModel.Calendar.cs` partial.

### Block `UIC05`: Automated UI Cleanliness Test, Build Verification & Dual Remote Push
- Added programmatic test suite `tests/MuscleCuties.Core.Tests/UiCleanlinessAndReachabilityTests.cs` verifying:
  - 100% of `Controls/**/*.xaml` files in `src/MuscleCuties.UI/Controls` are referenced by at least one Page, Control, or C# file.
  - 100% of routes registered in `AppShell.xaml.cs` (`Routing.RegisterRoute`) point to an existing Page class.
  - `PhaseAdjustmentReviewPageV010` is registered in `AppShell.xaml.cs` and `MauiProgram.cs`.
  - Zero retired controls or pages (`DashboardCalendarCard`, `InjuryLogPage`, `ScoreToColorConverter`, `DashboardMonthGrid`) exist on disk.
- Verified test pass: 532/532 passed in `MuscleCuties.Core.Tests`.
- Verified iOS simulator build: `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64` passed with 0 errors.
- Updated `docs/UI_CLEANUP_AND_ORGANIZATION_PLAN.md`, `docs/README.md`, `ROADMAP.md`, `STATUS.md`, `docs/v1/ROADMAP.md`, and `docs/v1/STATUS.md`.
- **Status:** Completed (2026-10-24) — Full test and iOS build pass verified; docs synchronized.
