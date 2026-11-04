# Soft Minimal binding inventory

**Generated from the completed source on 2026-11-02; exercise-condition bindings removed 2026-11-03.** Static source contract only: declared XAML binding expressions, `x:DataType` contexts, data-template item types and code-behind context wiring. It does not prove that a binding evaluated at runtime and does not replace owner device verification.

Regeneration method: balanced-brace scan for `{Binding ...}` in every `.xaml` under `src/MuscleCuties.UI` (excluding `bin/` and `obj/`), compared per file against the pre-redesign baseline at `git show HEAD:<path>`. Whitespace is normalised; additions and removals are reported rather than dropped.

- XAML surfaces inspected: **65**
- Distinct binding expressions: **1480**
- Expressions added vs baseline: **1**
- Expressions removed vs baseline: **6**
- Removed expressions that reference a command: **0**

Surfaces whose expression set changed: **4**.

## Page and control matrix

| Surface | XAML class | Declared `x:DataType` | Item contexts | Bindings | Delta |
| --- | --- | --- | --- | ---: | --- |
| `App.xaml` | `MuscleCuties.UI.App` | `—` | — | 0 | preserved |
| `AppShell.xaml` | `MuscleCuties.UI.AppShell` | `local:AppShell` | — | 0 | preserved |
| `Controls/Cycle/CycleCalendarCard.xaml` | `MuscleCuties.UI.Controls.Cycle.CycleCalendarCard` | `vm:CycleViewModel` | — | 35 | preserved |
| `Controls/Cycle/CycleDailyTipsCard.xaml` | `MuscleCuties.UI.Controls.Cycle.CycleDailyTipsCard` | `vm:CycleViewModel` | — | 2 | preserved |
| `Controls/Cycle/CycleDatePhaseModal.xaml` | `MuscleCuties.UI.Controls.Cycle.CycleDatePhaseModal` | `vm:CycleViewModel` | — | 23 | preserved |
| `Controls/Cycle/CycleHeroCard.xaml` | `MuscleCuties.UI.Controls.Cycle.CycleHeroCard` | `vm:CycleViewModel` | — | 4 | preserved |
| `Controls/Cycle/CyclePhaseExplorerCarousel.xaml` | `MuscleCuties.UI.Controls.Cycle.CyclePhaseExplorerCarousel` | `vm:CycleViewModel` | — | 10 | preserved |
| `Controls/Cycle/CyclePhaseSummarySheet.xaml` | `MuscleCuties.UI.Controls.Cycle.CyclePhaseSummarySheet` | `vm:CycleViewModel` | — | 14 | preserved |
| `Controls/Cycle/CycleQuickLogBottomSheet.xaml` | `MuscleCuties.UI.Controls.Cycle.CycleQuickLogBottomSheet` | `vm:CycleViewModel` | — | 23 | preserved |
| `Controls/Cycle/CycleWarningPopup.xaml` | `MuscleCuties.UI.Controls.Cycle.CycleWarningPopup` | `vm:CycleViewModel` | — | 9 | preserved |
| `Controls/Dashboard/DashboardNutritionCard.xaml` | `MuscleCuties.UI.Controls.Dashboard.DashboardNutritionCard` | `vm:DashboardViewModel` | — | 10 | preserved |
| `Controls/Dashboard/DashboardPhaseCard.xaml` | `MuscleCuties.UI.Controls.Dashboard.DashboardPhaseCard` | `vm:DashboardViewModel` | — | 13 | preserved |
| `Controls/Dashboard/DashboardTargetsCard.xaml` | `MuscleCuties.UI.Controls.Dashboard.DashboardTargetsCard` | `vm:DashboardViewModel` | — | 5 | preserved |
| `Controls/Dashboard/DashboardTrendCard.xaml` | `MuscleCuties.UI.Controls.Dashboard.DashboardTrendCard` | `vm:DashboardViewModel` | — | 29 | preserved |
| `Controls/Dashboard/DashboardWorkoutCard.xaml` | `MuscleCuties.UI.Controls.Dashboard.DashboardWorkoutCard` | `vm:DashboardViewModel` | — | 17 | preserved |
| `Controls/Dashboard/ReadinessRecoveryCard.xaml` | `MuscleCuties.UI.Controls.Dashboard.ReadinessRecoveryCard` | `{x:Type vm:DashboardViewModel}` | — | 18 | preserved |
| `Controls/Injury/InjuryLogContent.xaml` | `MuscleCuties.UI.Controls.Injury.InjuryLogContent` | `vm:InjuryLogViewModel` | `InjuryRow`→`ui:InjuryLogItem` | 47 | preserved |
| `Controls/Injury/InjuryLogModal.xaml` | `MuscleCuties.UI.Controls.Injury.InjuryLogModal` | `vm:InjuryLogViewModel` | — | 3 | preserved |
| `Controls/Nutrition/MealDetailModal.xaml` | `MuscleCuties.UI.Controls.Nutrition.MealDetailModal` | `vm:NutritionViewModel` | — | 26 | preserved |
| `Controls/Nutrition/MealEditorModal.xaml` | `MuscleCuties.UI.Controls.Nutrition.MealEditorModal` | `vm:NutritionViewModel` | — | 69 | preserved |
| `Controls/Nutrition/MealEntryModal.xaml` | `MuscleCuties.UI.Controls.Nutrition.MealEntryModal` | `vm:NutritionViewModel` | — | 11 | preserved |
| `Controls/Nutrition/MealSuggestionModal.xaml` | `MuscleCuties.UI.Controls.Nutrition.MealSuggestionModal` | `vm:NutritionViewModel` | — | 43 | preserved |
| `Controls/Nutrition/NutritionBalanceCard.xaml` | `MuscleCuties.UI.Controls.Nutrition.NutritionBalanceCard` | `vm:NutritionViewModel` | — | 10 | preserved |
| `Controls/Nutrition/NutritionBreakdownModal.xaml` | `MuscleCuties.UI.Controls.Nutrition.NutritionBreakdownModal` | `vm:NutritionViewModel` | — | 23 | preserved |
| `Controls/Nutrition/NutritionPhaseFocusCard.xaml` | `MuscleCuties.UI.Controls.Nutrition.NutritionPhaseFocusCard` | `vm:NutritionViewModel` | — | 5 | +0/-1 |
| `Controls/Nutrition/QuickFoodItemRow.xaml` | `MuscleCuties.UI.Controls.Nutrition.QuickFoodItemRow` | `uiModels:QuickFoodItemUiModel` | — | 8 | preserved |
| `Controls/Profile/ProfileDetailsContent.xaml` | `MuscleCuties.UI.Controls.Profile.ProfileDetailsContent` | `vm:ProfileViewModel` | — | 12 | preserved |
| `Controls/Profile/ProfileImageCropModal.xaml` | `MuscleCuties.UI.Controls.Profile.ProfileImageCropModal` | `local:ProfileImageCropModal` | — | 0 | preserved |
| `Controls/Profile/ProfileWorkoutPreferencesChoices.xaml` | `MuscleCuties.UI.Controls.Profile.ProfileWorkoutPreferencesChoices` | `vm:ProfileWorkoutPreferencesViewModel` | — | 12 | preserved |
| `Controls/Shared/IconSectionHeader.xaml` | `MuscleCuties.UI.Controls.Shared.IconSectionHeader` | `sharedControls:IconSectionHeader` | — | 2 | preserved |
| `Controls/Shared/PageLoadingOverlay.xaml` | `MuscleCuties.UI.Controls.Shared.PageLoadingOverlay` | `vm:IPageLoadAware` | — | 5 | preserved |
| `Controls/Shared/PhaseSaluteOverlay.xaml` | `MuscleCuties.UI.Controls.Shared.PhaseSaluteOverlay` | `sharedControls:PhaseSaluteOverlay` | — | 1 | preserved |
| `Controls/Workout/FeaturedWorkoutCard.xaml` | `MuscleCuties.UI.Controls.Workout.FeaturedWorkoutCard` | `workoutControls:FeaturedWorkoutCard` | — | 38 | preserved |
| `Controls/Workout/WorkoutActivityCard.xaml` | `MuscleCuties.UI.Controls.Workout.WorkoutActivityCard` | `workoutModels:WorkoutItem` | — | 16 | preserved |
| `Controls/Workout/WorkoutActivityPreferencePicker.xaml` | `MuscleCuties.UI.Controls.Workout.WorkoutActivityPreferencePicker` | `controls:WorkoutActivityPreferencePicker` | — | 10 | preserved |
| `Controls/Workout/WorkoutFeaturedSection.xaml` | `MuscleCuties.UI.Controls.Workout.WorkoutFeaturedSection` | `vm:WorkoutViewModel` | — | 15 | preserved |
| `Controls/Workout/WorkoutSwapPanel.xaml` | `MuscleCuties.UI.Controls.Workout.WorkoutSwapPanel` | `vm:WorkoutSessionViewModel` | — | 21 | preserved |
| `Pages/Auth/LoginPage.xaml` | `MuscleCuties.UI.Pages.Auth.LoginPage` | `vm:LoginViewModel` | — | 13 | preserved |
| `Pages/Auth/OtpVerificationPage.xaml` | `MuscleCuties.UI.Pages.Auth.OtpVerificationPage` | `vm:OtpVerificationViewModel` | — | 14 | preserved |
| `Pages/Auth/PasswordRecoveryPage.xaml` | `MuscleCuties.UI.Pages.Auth.PasswordRecoveryPage` | `vm:PasswordRecoveryViewModel` | — | 23 | preserved |
| `Pages/Auth/RegisterPage.xaml` | `MuscleCuties.UI.Pages.Auth.RegisterPage` | `vm:RegisterViewModel` | — | 14 | preserved |
| `Pages/Cycle/CyclePage.xaml` | `MuscleCuties.UI.Pages.Cycle.CyclePage` | `vm:CycleViewModel` | — | 6 | preserved |
| `Pages/Cycle/CyclePhaseDetailPage.xaml` | `MuscleCuties.UI.Pages.Cycle.CyclePhaseDetailPage` | `vm:CyclePhaseDetailViewModel` | — | 38 | +0/-1 |
| `Pages/Dashboard/DailyCheckInPage.xaml` | `MuscleCuties.UI.Pages.Dashboard.DailyCheckInPage` | `vm:DailyCheckInViewModel` | — | 24 | preserved |
| `Pages/Dashboard/DashboardPage.xaml` | `MuscleCuties.UI.Pages.Dashboard.DashboardPage` | `vm:DashboardViewModel` | — | 13 | preserved |
| `Pages/Nutrition/FoodLogPage.xaml` | `MuscleCuties.UI.Pages.Nutrition.FoodLogPage` | `vm:FoodLogViewModel` | — | 150 | +1/-2 |
| `Pages/Nutrition/NutritionPage.xaml` | `MuscleCuties.UI.Pages.Nutrition.NutritionPage` | `vm:NutritionViewModel` | — | 17 | preserved |
| `Pages/Onboarding/ProfileSetupPage.xaml` | `MuscleCuties.UI.Pages.Onboarding.ProfileSetupPage` | `vm:ProfileSetupViewModel` | — | 29 | preserved |
| `Pages/Onboarding/QuizPage.xaml` | `MuscleCuties.UI.Pages.Onboarding.QuizPage` | `vm:QuizViewModel` | — | 37 | preserved |
| `Pages/Profile/PhotoJourneyVaultPage.xaml` | `MuscleCuties.UI.Pages.Profile.PhotoJourneyVaultPage` | `vm:PhotoJourneyVaultViewModel` | — | 23 | preserved |
| `Pages/Profile/ProfileFeedbackPage.xaml` | `MuscleCuties.UI.Pages.Profile.ProfileFeedbackPage` | `vm:ProfileFeedbackViewModel` | — | 20 | preserved |
| `Pages/Profile/ProfileNutritionSettingsPage.xaml` | `MuscleCuties.UI.Pages.Profile.ProfileNutritionSettingsPage` | `vm:ProfileNutritionSettingsViewModel` | — | 38 | preserved |
| `Pages/Profile/ProfilePage.xaml` | `MuscleCuties.UI.Pages.Profile.ProfilePage` | `vm:ProfileViewModel` | — | 15 | preserved |
| `Pages/Profile/ProfilePersonalInfoPage.xaml` | `MuscleCuties.UI.Pages.Profile.ProfilePersonalInfoPage` | `vm:ProfilePersonalInfoViewModel` | — | 23 | preserved |
| `Pages/Profile/ProfilePrivacyPage.xaml` | `MuscleCuties.UI.Pages.Profile.ProfilePrivacyPage` | `local:ProfilePrivacyPage` | — | 1 | preserved |
| `Pages/Profile/ProfileUnitsDisplayPage.xaml` | `MuscleCuties.UI.Pages.Profile.ProfileUnitsDisplayPage` | `vm:ProfileUnitsDisplayViewModel` | — | 14 | preserved |
| `Pages/Profile/ProfileWorkoutPreferencesPage.xaml` | `MuscleCuties.UI.Pages.Profile.ProfileWorkoutPreferencesPage` | `vm:ProfileWorkoutPreferencesViewModel` | — | 16 | preserved |
| `Pages/Startup/AppStartupPage.xaml` | `MuscleCuties.UI.Pages.Startup.AppStartupPage` | `local:AppStartupPage` | — | 0 | preserved |
| `Pages/Workout/StrengthPlanGenerationPage.xaml` | `MuscleCuties.UI.Pages.Workout.StrengthPlanGenerationPage` | `vm:StrengthPlanGenerationViewModel` | — | 121 | changed |
| `Pages/Workout/WorkoutHistoryPage.xaml` | `MuscleCuties.UI.Pages.Workout.WorkoutHistoryPage` | `vm:WorkoutHistoryViewModel` | — | 48 | preserved |
| `Pages/Workout/WorkoutPage.xaml` | `MuscleCuties.UI.Pages.Workout.WorkoutPage` | `vm:WorkoutViewModel` | — | 66 | preserved |
| `Pages/Workout/WorkoutSessionPage.xaml` | `MuscleCuties.UI.Pages.Workout.WorkoutSessionPage` | `vm:WorkoutSessionViewModel` | — | 127 | preserved |
| `Platforms/Windows/App.xaml` | `MuscleCuties.UI.WinUI.App` | `—` | — | 0 | preserved |
| `Resources/Styles/Colors.xaml` | `` | `—` | — | 0 | preserved |
| `Resources/Styles/Styles.xaml` | `` | `—` | — | 1 | preserved |

## Intentional binding deltas

Each delta is presentation-only: the same view-model member was re-pointed after moving markup, or markup containing that expression was replaced. No view-model member, command or DTO was renamed or deleted.

### `Controls/Nutrition/NutritionPhaseFocusCard.xaml`


Removed:
- `{Binding CurrentPhase, Converter={StaticResource CyclePhaseToBrushConverter}}`

### `Pages/Cycle/CyclePhaseDetailPage.xaml`


Removed:
- `{Binding CurrentPhase, Converter={StaticResource CyclePhaseToBrushConverter}}`

### `Pages/Nutrition/FoodLogPage.xaml`

Added:
- `{Binding IsSelected}`

Removed:
- `{Binding IsSelected, Converter={StaticResource BoolToSelectionBgConverter}, FallbackValue=Transparent}`
- `{Binding IsSelected, Converter={StaticResource BoolToSelectionStrokeConverter}, FallbackValue=Transparent}`

## Complete per-surface binding expressions

### `App.xaml`

Class: `MuscleCuties.UI.App`


### `AppShell.xaml`

Class: `MuscleCuties.UI.AppShell` · `x:DataType="{local:AppShell}"`


### `Controls/Cycle/CycleCalendarCard.xaml`

Class: `MuscleCuties.UI.Controls.Cycle.CycleCalendarCard` · `x:DataType="{vm:CycleViewModel}"`

- `{Binding .}`
- `{Binding AccessibilityDescription, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding AdvancePhaseCommand}`
- `{Binding BackgroundColor}`
- `{Binding CalendarDays}`
- `{Binding CalendarEditHintText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CalendarNavigationStatusText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CalendarStripDays}`
- `{Binding CurrentMonthLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CurrentPhaseIconSource}`
- `{Binding CycleDaySummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DayOfWeekShort, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DisplayDayLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding HasActiveCycle}`
- `{Binding HasCalendarEditHint}`
- `{Binding HasCalendarNavigationStatus}`
- `{Binding IsMonthGridExpanded}`
- `{Binding IsViewingCurrentMonth, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding LastPhaseLogText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MonthExpandButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding NextMonthCommand}`
- `{Binding PeriodCountdownText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseLabel, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Cycle_Calendar_CurrentPhaseFormat}`
- `{Binding PhaseLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseLogActionText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseMarkerColor}`
- `{Binding PredictionSummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PreviousMonthCommand}`
- `{Binding ReturnToTodayCommand}`
- `{Binding SelectCalendarDayCommand, Source={RelativeSource AncestorType={x:Type vm:CycleViewModel}}, x:DataType=vm:CycleViewModel}`
- `{Binding SelectCalendarDayCommand}`
- `{Binding StrokeColor}`
- `{Binding StrokeThickness}`
- `{Binding TextColor}`
- `{Binding ToggleMonthGridExpandedCommand}`

### `Controls/Cycle/CycleDailyTipsCard.xaml`

Class: `MuscleCuties.UI.Controls.Cycle.CycleDailyTipsCard` · `x:DataType="{vm:CycleViewModel}"`

- `{Binding DailyNutritionTipText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DailyTrainingTipText, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Cycle/CycleDatePhaseModal.xaml`

Class: `MuscleCuties.UI.Controls.Cycle.CycleDatePhaseModal` · `x:DataType="{vm:CycleViewModel}"`

- `{Binding .}`
- `{Binding CloseDatePhaseModalCommand}`
- `{Binding Description, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding HasPhaseEditStatus}`
- `{Binding HasPhaseJumpWarning, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding HasPhaseJumpWarning}`
- `{Binding HasSuggestedPhase}`
- `{Binding IconSource}`
- `{Binding IsDatePhaseModalVisible}`
- `{Binding IsSelected}`
- `{Binding Name, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Cycle_DatePhaseModal_SelectPhaseFormat}`
- `{Binding Name, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseEditOptions}`
- `{Binding PhaseEditStatusText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseJumpWarningText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseJumpWarningTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ReviewEarlierPhaseRecordsCommand}`
- `{Binding SaveDatePhaseCommand}`
- `{Binding SelectPhaseOptionCommand, Source={RelativeSource AncestorType={x:Type vm:CycleViewModel}}, x:DataType=vm:CycleViewModel}`
- `{Binding SelectedCalendarDateText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedCalendarPhaseText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding TextColor}`
- `{Binding UseSuggestedPhaseCommand}`

### `Controls/Cycle/CycleHeroCard.xaml`

Class: `MuscleCuties.UI.Controls.Cycle.CycleHeroCard` · `x:DataType="{vm:CycleViewModel}"`

- `{Binding CycleDaySummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding OpenQuickLogCommand}`
- `{Binding PhaseLabel, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Cycle_Hero_PhaseFormat}`
- `{Binding PhaseVectorArtworkData, Converter={StaticResource StringToGeometryConverter}}`

### `Controls/Cycle/CyclePhaseExplorerCarousel.xaml`

Class: `MuscleCuties.UI.Controls.Cycle.CyclePhaseExplorerCarousel` · `x:DataType="{vm:CycleViewModel}"`

- `{Binding .}`
- `{Binding AccentColor}`
- `{Binding BackgroundColor}`
- `{Binding IconSource}`
- `{Binding IsActivePhase}`
- `{Binding Name, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Cycle_PhaseExplorer_ExplorePhaseFormat}`
- `{Binding Name, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseExplorerPills}`
- `{Binding SelectPhaseExplorerPillCommand, Source={RelativeSource AncestorType={x:Type vm:CycleViewModel}}, x:DataType=vm:CycleViewModel}`
- `{Binding TextColor}`

### `Controls/Cycle/CyclePhaseSummarySheet.xaml`

Class: `MuscleCuties.UI.Controls.Cycle.CyclePhaseSummarySheet` · `x:DataType="{vm:CycleViewModel}"`

- `{Binding ClosePhaseSummarySheetCommand}`
- `{Binding IsPhaseSummarySheetVisible}`
- `{Binding SelectedPhaseSummary.AccentColor}`
- `{Binding SelectedPhaseSummary.BackgroundColor}`
- `{Binding SelectedPhaseSummary.HormonalSummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedPhaseSummary.IconSource}`
- `{Binding SelectedPhaseSummary.Name, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedPhaseSummary.NutritionFocus, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedPhaseSummary.QuickRule, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedPhaseSummary.RecoveryFocus, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedPhaseSummary.Tagline, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedPhaseSummary.TextColor}`
- `{Binding SelectedPhaseSummary.TrainingFocus, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedPhaseSummary.Window, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Cycle/CycleQuickLogBottomSheet.xaml`

Class: `MuscleCuties.UI.Controls.Cycle.CycleQuickLogBottomSheet` · `x:DataType="{vm:CycleViewModel}"`

- `{Binding .}`
- `{Binding CloseQuickLogSheetCommand}`
- `{Binding FlowOptions}`
- `{Binding IconData, Converter={StaticResource StringToGeometryConverter}}`
- `{Binding IsPeriodEnded}`
- `{Binding IsPeriodStarted}`
- `{Binding IsQuickLogLoading}`
- `{Binding IsQuickLogPeriodStartNeeded}`
- `{Binding IsQuickLogSheetVisible}`
- `{Binding IsSelected}`
- `{Binding Name, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Cycle_QuickLog_FlowIntensityFormat}`
- `{Binding Name, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Cycle_QuickLog_SymptomFormat}`
- `{Binding Name, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding QuickLogDateText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding QuickLogStatusText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding QuickLogStatusText, Converter={StaticResource StringToBoolConverter}}`
- `{Binding SaveQuickLogCommand}`
- `{Binding SelectFlowCommand, Source={RelativeSource AncestorType={x:Type vm:CycleViewModel}}, x:DataType=vm:CycleViewModel}`
- `{Binding StartPeriodFromQuickLogCommand}`
- `{Binding SymptomOptions}`
- `{Binding TogglePeriodEndedCommand}`
- `{Binding TogglePeriodStartedCommand}`
- `{Binding ToggleSymptomCommand, Source={RelativeSource AncestorType={x:Type vm:CycleViewModel}}, x:DataType=vm:CycleViewModel}`

### `Controls/Cycle/CycleWarningPopup.xaml`

Class: `MuscleCuties.UI.Controls.Cycle.CycleWarningPopup` · `x:DataType="{vm:CycleViewModel}"`

- `{Binding CloseCycleWarningPopupCommand}`
- `{Binding CycleWarningSuggestedActionText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CycleWarningText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CycleWarningTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding HasCycleWarningSuggestedPhase, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding HasCycleWarningSuggestedPhase}`
- `{Binding IsCycleWarningPopupVisible}`
- `{Binding ReviewEarlierPhaseRecordsCommand}`
- `{Binding UseCycleWarningSuggestedPhaseCommand}`

### `Controls/Dashboard/DashboardNutritionCard.xaml`

Class: `MuscleCuties.UI.Controls.Dashboard.DashboardNutritionCard` · `x:DataType="{vm:DashboardViewModel}"`

- `{Binding CaloriesConsumed, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CaloriesGoal, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CaloriesProgress}`
- `{Binding CarbsProgress}`
- `{Binding CarbsText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding FatsProgress}`
- `{Binding FatsText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding OpenNutritionCommand}`
- `{Binding ProteinProgress}`
- `{Binding ProteinText, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Dashboard/DashboardPhaseCard.xaml`

Class: `MuscleCuties.UI.Controls.Dashboard.DashboardPhaseCard` · `x:DataType="{vm:DashboardViewModel}"`

- `{Binding CurrentPhaseColumn}`
- `{Binding HasKnownPhase}`
- `{Binding LoadAdjustmentText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding NextPeriodValue, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding OpenCycleCommand}`
- `{Binding PhaseCardBackgroundColor}`
- `{Binding PhaseCardTitle, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Dashboard_Phase_SemanticCurrentPhase}`
- `{Binding PhaseCardTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseIconSource}`
- `{Binding PhaseShortAdvice, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseStatusText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseTimeLeftLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseTimeLeftValue, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Dashboard/DashboardTargetsCard.xaml`

Class: `MuscleCuties.UI.Controls.Dashboard.DashboardTargetsCard` · `x:DataType="{vm:DashboardViewModel}"`

- `{Binding HydrationConsumed, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Dashboard_Targets_HydrationFormat}`
- `{Binding HydrationConsumed, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding HydrationGoal, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SleepGoal, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Dashboard_Targets_SleepFormat}`
- `{Binding SleepGoal, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Dashboard/DashboardTrendCard.xaml`

Class: `MuscleCuties.UI.Controls.Dashboard.DashboardTrendCard` · `x:DataType="{vm:DashboardViewModel}"`

- `{Binding FastestPaceText}`
- `{Binding HasHeaviestLiftExerciseName}`
- `{Binding HasWeeklyNutrition, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding HasWeeklyNutrition}`
- `{Binding HasWeeklyWeights, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding HasWeeklyWeights}`
- `{Binding HasWorkoutProgress, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding HasWorkoutProgress}`
- `{Binding HeaviestLiftExerciseName}`
- `{Binding HeaviestLiftText}`
- `{Binding HighestVolumeText}`
- `{Binding IsNutritionTrendVisible}`
- `{Binding IsWeightTrendVisible}`
- `{Binding IsWorkoutTrendVisible}`
- `{Binding LongestDistanceText}`
- `{Binding LongestSessionText}`
- `{Binding NextTrendCommand}`
- `{Binding PreviousTrendCommand}`
- `{Binding TrendPositionText}`
- `{Binding WeeklyCaloriesText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WeeklyCarbsShare}`
- `{Binding WeeklyCarbsText}`
- `{Binding WeeklyFatShare}`
- `{Binding WeeklyFatText}`
- `{Binding WeeklyProteinShare}`
- `{Binding WeeklyProteinText}`
- `{Binding WeeklyWeightPoints}`
- `{Binding WeeklyWeightSummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WeeklyWeightValuesText}`

### `Controls/Dashboard/DashboardWorkoutCard.xaml`

Class: `MuscleCuties.UI.Controls.Dashboard.DashboardWorkoutCard` · `x:DataType="{vm:DashboardViewModel}"`

- `{Binding Background}`
- `{Binding IsRestDay, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsRestDay}`
- `{Binding StartWorkoutCommand}`
- `{Binding TextColor}`
- `{Binding Title, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WorkoutActivityBackground}`
- `{Binding WorkoutActivitySections}`
- `{Binding WorkoutActivityTextColor}`
- `{Binding WorkoutBadgeText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WorkoutCountLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WorkoutCountValue, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WorkoutDurationText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WorkoutIntensity, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WorkoutSubtitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WorkoutTitle, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Dashboard_Workout_SemanticTodayPlan}`
- `{Binding WorkoutTitle, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Dashboard/ReadinessRecoveryCard.xaml`

Class: `MuscleCuties.UI.Controls.Dashboard.ReadinessRecoveryCard` · `x:DataType="{{x:Type vm:DashboardViewModel}}"`

- `{Binding HasTodayReadinessRecord}`
- `{Binding HasTodayReadinessScore}`
- `{Binding NeedsDailyCheckIn}`
- `{Binding OpenDailyCheckInCommand}`
- `{Binding ReadinessLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ReadinessProgress}`
- `{Binding ReadinessScore}`
- `{Binding ReadinessSubtitleText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RecoveryLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RecoveryProgress}`
- `{Binding RecoveryScore}`
- `{Binding TodayReadinessBandText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding TodayReadinessEnergyText}`
- `{Binding TodayReadinessGuidanceText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding TodayReadinessIncomplete}`
- `{Binding TodayReadinessReviewPending}`
- `{Binding TodayReadinessSleepQualityText}`
- `{Binding TodayReadinessSorenessText}`

### `Controls/Injury/InjuryLogContent.xaml`

Class: `MuscleCuties.UI.Controls.Injury.InjuryLogContent` · `x:DataType="{vm:InjuryLogViewModel}"`

Item templates: `InjuryRow` → `ui:InjuryLogItem`

- `{Binding .}`
- `{Binding ActiveInjuries}`
- `{Binding AvailableSites}`
- `{Binding AvailableStatuses}`
- `{Binding CancelEditCommand}`
- `{Binding ClearedInjuries}`
- `{Binding ClinicianAdvisedAvoid}`
- `{Binding EmptyStateDetail, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding EmptyStateTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ErrorText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding FormTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding HasActiveInjuries}`
- `{Binding HasClearedInjuries}`
- `{Binding HasError}`
- `{Binding Id}`
- `{Binding IsActive}`
- `{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsBusy}`
- `{Binding IsHistoryExpanded}`
- `{Binding IsLoadError}`
- `{Binding IsPageRefreshError}`
- `{Binding IsSelected}`
- `{Binding Label, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding LoadCommand}`
- `{Binding NewPainLevel}`
- `{Binding PainLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PainLabelText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RetryPageRefreshCommand}`
- `{Binding SaveInjuryCommand}`
- `{Binding SelectedWorseningOption}`
- `{Binding ShowAddForm}`
- `{Binding ShowEmptyState}`
- `{Binding ShowsSafetyGuidance}`
- `{Binding SinceDateValue}`
- `{Binding SinceText, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Injury_LogContent_SinceValue}`
- `{Binding SiteName, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Injury_LogContent_EditValueInjury}`
- `{Binding SiteName, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Injury_LogContent_MarkValueInjuryCleared}`
- `{Binding SiteName, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Source={RelativeSource AncestorType={x:Type vm:InjuryLogViewModel}}, Path=DeleteInjuryCommand, x:DataType=vm:InjuryLogViewModel}`
- `{Binding Source={RelativeSource AncestorType={x:Type vm:InjuryLogViewModel}}, Path=EditInjuryCommand, x:DataType=vm:InjuryLogViewModel}`
- `{Binding Source={RelativeSource AncestorType={x:Type vm:InjuryLogViewModel}}, Path=MarkClearedCommand, x:DataType=vm:InjuryLogViewModel}`
- `{Binding Source={RelativeSource AncestorType={x:Type vm:InjuryLogViewModel}}, Path=SelectSiteCommand, x:DataType=vm:InjuryLogViewModel}`
- `{Binding Source={RelativeSource AncestorType={x:Type vm:InjuryLogViewModel}}, Path=SelectStatusCommand, x:DataType=vm:InjuryLogViewModel}`
- `{Binding StatusName, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Today}`
- `{Binding ToggleHistoryCommand}`
- `{Binding WorseningTriggerOptions}`

### `Controls/Injury/InjuryLogModal.xaml`

Class: `MuscleCuties.UI.Controls.Injury.InjuryLogModal` · `x:DataType="{vm:InjuryLogViewModel}"`

- `{Binding CloseCommand, Source={x:Reference Root}, x:DataType=injuryControls:InjuryLogModal}`
- `{Binding ShowAddForm, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding ToggleAddFormCommand}`

### `Controls/Nutrition/MealDetailModal.xaml`

Class: `MuscleCuties.UI.Controls.Nutrition.MealDetailModal` · `x:DataType="{vm:NutritionViewModel}"`

- `{Binding AcceptSuggestionCommand}`
- `{Binding CloseMealDetailCommand}`
- `{Binding DailyMacroRemainingText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DailyProgressText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Description}`
- `{Binding DetailCaloriesText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DetailDirectionsText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DetailHasSpiceBlends}`
- `{Binding DetailMacrosText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DetailText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding IconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}, ConverterParameter=Accent}`
- `{Binding IsMealDetailVisible}`
- `{Binding IsRecommended}`
- `{Binding Items}`
- `{Binding Label, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealFillPercentText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Name}`
- `{Binding SelectedMealDetail.ComponentGroups}`
- `{Binding SelectedMealDetail.ConceptDescription, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedMealDetail.ConceptName, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedMealDetail.IconPathData, Converter={StaticResource StringToGeometryConverter}}`
- `{Binding SelectedMealDetail.SpiceBlends}`
- `{Binding SelectedMealDetail.StyleDescription, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedMealDetail.StyleLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedMealDetail}`
- `{Binding TotalText, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Nutrition/MealEditorModal.xaml`

Class: `MuscleCuties.UI.Controls.Nutrition.MealEditorModal` · `x:DataType="{vm:NutritionViewModel}"`

- `{Binding ., Converter={StaticResource LocalizedStringConverter}}`
- `{Binding .}`
- `{Binding AddFoodMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding AddIngredientCommand}`
- `{Binding AmountSummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding BrowseMoreFoodsButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding BrowseMoreFoodsCommand}`
- `{Binding CreateCustomFoodCommand}`
- `{Binding CustomFoodCalcium}`
- `{Binding CustomFoodCalories}`
- `{Binding CustomFoodCarbs}`
- `{Binding CustomFoodCompanySource}`
- `{Binding CustomFoodFats}`
- `{Binding CustomFoodIron}`
- `{Binding CustomFoodName}`
- `{Binding CustomFoodProtein}`
- `{Binding CustomFoodServingAmount}`
- `{Binding CustomFoodServingUnits}`
- `{Binding CustomFoodVitaminB12}`
- `{Binding CustomFoodVitaminD}`
- `{Binding EditIngredientCommand, Source={RelativeSource AncestorType={x:Type vm:NutritionViewModel}}, x:DataType=vm:NutritionViewModel}`
- `{Binding FoodGrams}`
- `{Binding FoodSearchResults}`
- `{Binding HasAddFoodMessage}`
- `{Binding HasSelectedFoodSource}`
- `{Binding IngredientActionText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding IsBusy}`
- `{Binding IsCustomFoodPanelVisible}`
- `{Binding IsFoodFinderCollapsed}`
- `{Binding IsFoodFinderVisible}`
- `{Binding IsIngredientSearchVisible}`
- `{Binding IsMealChoiceLocked, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsMealChoiceLocked}`
- `{Binding IsMealEditorVisible}`
- `{Binding IsSearchingFood}`
- `{Binding IsSelectedFoodEditorVisible}`
- `{Binding LogMealButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding LogMealCommand}`
- `{Binding MealChoiceSummary}`
- `{Binding MealDraftCaloriesText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealDraftCarbsText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealDraftFatsText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealDraftProteinText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealEditorBackCommand}`
- `{Binding MealEditorTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealIngredients}`
- `{Binding MealTypes}`
- `{Binding Name, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_MealEditor_ChooseValue}`
- `{Binding Name, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_MealEditor_EditValuePortion}`
- `{Binding Name, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_MealEditor_RemoveValue}`
- `{Binding Name}`
- `{Binding NutritionSummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding OpenFoodFinderCommand}`
- `{Binding RemoveIngredientCommand, Source={RelativeSource AncestorType={x:Type vm:NutritionViewModel}}, x:DataType=vm:NutritionViewModel}`
- `{Binding SearchFoodCommand}`
- `{Binding SearchQuery}`
- `{Binding SelectFoodResultCommand, Source={RelativeSource AncestorType={x:Type vm:NutritionViewModel}}, x:DataType=vm:NutritionViewModel}`
- `{Binding SelectedCustomFoodServingUnit}`
- `{Binding SelectedFoodAmountText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedFoodSourceText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedFoodText}`
- `{Binding SelectedMealTime}`
- `{Binding SelectedMealType}`
- `{Binding SelectedServingOption}`
- `{Binding ServingOptions}`
- `{Binding ShowBrowseMoreFoods}`
- `{Binding SourceSummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ToggleCustomFoodPanelCommand}`
- `{Binding ToggleMealEditorCommand}`

### `Controls/Nutrition/MealEntryModal.xaml`

Class: `MuscleCuties.UI.Controls.Nutrition.MealEntryModal` · `x:DataType="{vm:NutritionViewModel}"`

- `{Binding CloseMealEntryCommand}`
- `{Binding EntryMealTime}`
- `{Binding HasEntryMealTypeSelected}`
- `{Binding IsEntryBreakfastSelected}`
- `{Binding IsEntryDinnerSelected}`
- `{Binding IsEntryLunchSelected}`
- `{Binding IsEntrySnackSelected}`
- `{Binding SelectEntryMealTypeCommand}`
- `{Binding StartDayPlanCommand}`
- `{Binding StartManualMealCommand}`
- `{Binding StartSuggestedMealCommand}`

### `Controls/Nutrition/MealSuggestionModal.xaml`

Class: `MuscleCuties.UI.Controls.Nutrition.MealSuggestionModal` · `x:DataType="{vm:NutritionViewModel}"`

- `{Binding .}`
- `{Binding CaloriesText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CanRefresh}`
- `{Binding CloseSuggestionModalCommand}`
- `{Binding ComponentSummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ConceptDescription, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ConceptName, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DayPlanMeals}`
- `{Binding DayPlanSummaryText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding HasMealSuggestionError}`
- `{Binding HasMealSuggestions}`
- `{Binding HasNoDayPlanMeals}`
- `{Binding HasNoMealSuggestions}`
- `{Binding HasSuggestion}`
- `{Binding IconPathData, Converter={StaticResource StringToGeometryConverter}}`
- `{Binding IsDayPlanMode, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsDayPlanMode}`
- `{Binding IsLoadingSuggestions}`
- `{Binding IsSavouryBreakfast}`
- `{Binding IsSuggestionModalVisible}`
- `{Binding IsSweetBreakfast}`
- `{Binding LoadingSuggestionText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MacrosText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealSuggestionError, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealSuggestions}`
- `{Binding MealSummaryText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealTitle, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_MealSuggestion_UseValue}`
- `{Binding MealTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealTypeIconPathData, Converter={StaticResource StringToGeometryConverter}}`
- `{Binding MealTypeLabel, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_MealSuggestion_FindAnotherValueIdea}`
- `{Binding MealTypeLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RefreshDayMealCommand, Source={RelativeSource AncestorType={x:Type vm:NutritionViewModel}}, x:DataType=vm:NutritionViewModel}`
- `{Binding RefreshSuggestionsCommand}`
- `{Binding SetBreakfastPreferenceCommand}`
- `{Binding ShowBreakfastPreferenceToggle}`
- `{Binding ShowDayPlanMeals}`
- `{Binding ShowSuggestionResults}`
- `{Binding Source={RelativeSource AncestorType={x:Type vm:NutritionViewModel}}, Path=OpenMealDetailCommand, x:DataType=vm:NutritionViewModel}`
- `{Binding StyleLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SuggestionPageTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SuggestionTargetText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding TargetSummaryText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding UsePlannedMealCommand, Source={RelativeSource AncestorType={x:Type vm:NutritionViewModel}}, x:DataType=vm:NutritionViewModel}`

### `Controls/Nutrition/NutritionBalanceCard.xaml`

Class: `MuscleCuties.UI.Controls.Nutrition.NutritionBalanceCard` · `x:DataType="{vm:NutritionViewModel}"`

- `{Binding CaloriesProgress}`
- `{Binding CarbsProgress}`
- `{Binding CarbsText}`
- `{Binding ConsumedCalories, StringFormat='{0:F0}'}`
- `{Binding FatsProgress}`
- `{Binding FatsText}`
- `{Binding OpenMicronutrientsModalCommand}`
- `{Binding ProteinProgress}`
- `{Binding ProteinText}`
- `{Binding TargetCalories, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_Format_TargetCaloriesValueKcal}`

### `Controls/Nutrition/NutritionBreakdownModal.xaml`

Class: `MuscleCuties.UI.Controls.Nutrition.NutritionBreakdownModal` · `x:DataType="{vm:NutritionViewModel}"`

- `{Binding AmountText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CanEditSelectedBreakdown}`
- `{Binding CloseMicronutrientsModalCommand}`
- `{Binding Color}`
- `{Binding EditSelectedBreakdownMealCommand}`
- `{Binding GoalText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Group, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding IconPathData, Converter={StaticResource StringToGeometryConverter}}`
- `{Binding IsBreakdownModalVisible}`
- `{Binding IsGoalHit}`
- `{Binding Name}`
- `{Binding ProgressText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Progress}`
- `{Binding SelectedBreakdownCaloriesText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedBreakdownCarbsCalories}`
- `{Binding SelectedBreakdownFatsCalories}`
- `{Binding SelectedBreakdownFiberText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedBreakdownMacroItems}`
- `{Binding SelectedBreakdownMacrosText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedBreakdownMicronutrients}`
- `{Binding SelectedBreakdownNutrientSummaryText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedBreakdownProteinCalories}`
- `{Binding SelectedBreakdownTitle, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Nutrition/NutritionPhaseFocusCard.xaml`

Class: `MuscleCuties.UI.Controls.Nutrition.NutritionPhaseFocusCard` · `x:DataType="{vm:NutritionViewModel}"`

- `{Binding CurrentPhase, Converter={StaticResource CyclePhaseToTextColorConverter}}`
- `{Binding CurrentPhaseIconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding PhaseFocusBadgeText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseFocusCopy, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseFocusTitle, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Nutrition/QuickFoodItemRow.xaml`

Class: `MuscleCuties.UI.Controls.Nutrition.QuickFoodItemRow` · `x:DataType="{uiModels:QuickFoodItemUiModel}"`

- `{Binding .}`
- `{Binding Calories, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_Format_CaloriesValueKcal}`
- `{Binding Detail, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding IsSelected}`
- `{Binding Name}`
- `{Binding QuickLogFoodCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding SelectQuickFoodCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding ToggleQuickFoodSelectionCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`

### `Controls/Profile/ProfileDetailsContent.xaml`

Class: `MuscleCuties.UI.Controls.Profile.ProfileDetailsContent` · `x:DataType="{vm:ProfileViewModel}"`

- `{Binding AvailableWeekdaysText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding HasVaultPhotos, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding HasVaultPhotos}`
- `{Binding IsVaultRevealed, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsVaultRevealed}`
- `{Binding LatestVaultPhotoPath}`
- `{Binding NutritionPacingText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding OpenRouteCommand}`
- `{Binding OpenVaultCommand}`
- `{Binding ToggleVaultPrivacyCommand}`
- `{Binding UnitsDisplayText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding VaultPhotoCountText, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Profile/ProfileImageCropModal.xaml`

Class: `MuscleCuties.UI.Controls.Profile.ProfileImageCropModal` · `x:DataType="{local:ProfileImageCropModal}"`


### `Controls/Profile/ProfileWorkoutPreferencesChoices.xaml`

Class: `MuscleCuties.UI.Controls.Profile.ProfileWorkoutPreferencesChoices` · `x:DataType="{vm:ProfileWorkoutPreferencesViewModel}"`

- `{Binding EquipmentOptions}`
- `{Binding ExerciseExclusionSearchText, Mode=TwoWay}`
- `{Binding FilteredExerciseExclusionOptions}`
- `{Binding GroupedWorkoutActivityOptions}`
- `{Binding HasExerciseExclusionQuery}`
- `{Binding HasNoExerciseExclusionMatches}`
- `{Binding Label, Converter={StaticResource LocalizedStringConverter}, x:DataType=commonModels:ILabeledOption}`
- `{Binding Name}`
- `{Binding SelectedEquipmentOption, Mode=TwoWay}`
- `{Binding SelectedExerciseExclusions}`
- `{Binding ToggleCommand}`
- `{Binding ToggleWorkoutActivityCommand}`

### `Controls/Shared/IconSectionHeader.xaml`

Class: `MuscleCuties.UI.Controls.Shared.IconSectionHeader` · `x:DataType="{sharedControls:IconSectionHeader}"`

- `{Binding IconGlyph, Source={x:Reference Root}, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding Text, Source={x:Reference Root}}`

### `Controls/Shared/PageLoadingOverlay.xaml`

Class: `MuscleCuties.UI.Controls.Shared.PageLoadingOverlay` · `x:DataType="{vm:IPageLoadAware}"`

- `{Binding BackCommand, Source={x:Reference OverlayRoot}}`
- `{Binding HasBackCommand, Source={x:Reference OverlayRoot}}`
- `{Binding IsLoadError}`
- `{Binding IsPageLoading}`
- `{Binding LoadDataCommand}`

### `Controls/Shared/PhaseSaluteOverlay.xaml`

Class: `MuscleCuties.UI.Controls.Shared.PhaseSaluteOverlay` · `x:DataType="{sharedControls:PhaseSaluteOverlay}"`

- `{Binding IconSource, Source={x:Reference Root}}`

### `Controls/Workout/FeaturedWorkoutCard.xaml`

Class: `MuscleCuties.UI.Controls.Workout.FeaturedWorkoutCard` · `x:DataType="{workoutControls:FeaturedWorkoutCard}"`

- `{Binding AccentColor, Source={x:Reference Root}}`
- `{Binding AccentTextColor, Source={x:Reference Root}}`
- `{Binding ActionIcon, Source={x:Reference Root}}`
- `{Binding ActionText, Source={x:Reference Root}}`
- `{Binding BadgeText, Source={x:Reference Root}}`
- `{Binding DurationText, Source={x:Reference Root}}`
- `{Binding ExercisePreviews, Source={x:Reference Root}}`
- `{Binding ExercisePreviewsCountText, Source={x:Reference Root}}`
- `{Binding ExercisesCount, Source={x:Reference Root}, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Featured_ExercisesCountFormat}`
- `{Binding HasExercisePreviews, Source={x:Reference Root}}`
- `{Binding HasMultipleActivities, Source={x:Reference Root}}`
- `{Binding Intensity, Source={x:Reference Root}}`
- `{Binding IsActionVisible, Source={x:Reference Root}}`
- `{Binding IsPreviewExpanded, Source={x:Reference Root}}`
- `{Binding IsRestDay, Source={x:Reference Root}, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsRestDay, Source={x:Reference Root}}`
- `{Binding MovementNotes, Converter={StaticResource StringToBoolConverter}}`
- `{Binding MovementNotes}`
- `{Binding MuscleGroups, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Featured_TargetFormat}`
- `{Binding Name}`
- `{Binding PreviewChevron, Source={x:Reference Root}}`
- `{Binding PrimaryActivityBackground, Source={x:Reference Root}}`
- `{Binding PrimaryActivityTextColor, Source={x:Reference Root}}`
- `{Binding PrimaryActivityTitle, Source={x:Reference Root}}`
- `{Binding SecondaryActivityBackground, Source={x:Reference Root}}`
- `{Binding SecondaryActivityTextColor, Source={x:Reference Root}}`
- `{Binding SecondaryActivityTitle, Source={x:Reference Root}}`
- `{Binding SetsReps, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding StartCommand, Source={x:Reference Root}}`
- `{Binding Subtitle, Source={x:Reference Root}}`
- `{Binding Tempo, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Tempo, Converter={StaticResource StringToBoolConverter}}`
- `{Binding TogglePreviewCommand, Source={x:Reference Root}}`
- `{Binding UnavailableGuidanceText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding UnavailableGuidanceText, Converter={StaticResource StringToBoolConverter}}`
- `{Binding WarmUpNote, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Featured_WarmupFormat}`
- `{Binding WarmUpNote, Converter={StaticResource StringToBoolConverter}}`
- `{Binding WorkoutTitle, Source={x:Reference Root}}`

### `Controls/Workout/WorkoutActivityCard.xaml`

Class: `MuscleCuties.UI.Controls.Workout.WorkoutActivityCard` · `x:DataType="{workoutModels:WorkoutItem}"`

- `{Binding ActivityBackground}`
- `{Binding ActivityTextColor}`
- `{Binding CanMarkEasierAlternative}`
- `{Binding CanMarkOptional}`
- `{Binding CommandParameter, Source={x:Reference Root}, x:DataType=workoutControls:WorkoutActivityCard}`
- `{Binding DayLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding EasierAlternativeStatusText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding IsCompleted}`
- `{Binding IsOptional}`
- `{Binding OpenCommand, Source={x:Reference Root}, x:DataType=workoutControls:WorkoutActivityCard}`
- `{Binding OptionalStatusText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SessionProgressText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Title, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Activity_OpenWorkoutFormat}`
- `{Binding Title, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ToggleEasierAlternativeCommand, Source={x:Reference Root}, x:DataType=workoutControls:WorkoutActivityCard}`
- `{Binding ToggleOptionalCommand, Source={x:Reference Root}, x:DataType=workoutControls:WorkoutActivityCard}`

### `Controls/Workout/WorkoutActivityPreferencePicker.xaml`

Class: `MuscleCuties.UI.Controls.Workout.WorkoutActivityPreferencePicker` · `x:DataType="{controls:WorkoutActivityPreferencePicker}"`

- `{Binding .}`
- `{Binding ActivityGroups, Source={x:Reference PreferencePickerRoot}}`
- `{Binding GroupIconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding GroupTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding IconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding IsSelected}`
- `{Binding Items}`
- `{Binding Source={x:Reference PreferencePickerRoot}, Path=ToggleActivityCommand, x:DataType=controls:WorkoutActivityPreferencePicker}`
- `{Binding Subtitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Title, Converter={StaticResource LocalizedStringConverter}}`

### `Controls/Workout/WorkoutFeaturedSection.xaml`

Class: `MuscleCuties.UI.Controls.Workout.WorkoutFeaturedSection` · `x:DataType="{vm:WorkoutViewModel}"`

- `{Binding FeaturedWorkoutActionText}`
- `{Binding FeaturedWorkoutActivityBackground}`
- `{Binding FeaturedWorkoutActivitySections}`
- `{Binding FeaturedWorkoutActivityTextColor}`
- `{Binding FeaturedWorkoutBadgeText}`
- `{Binding FeaturedWorkoutDurationText}`
- `{Binding FeaturedWorkoutExercisesCount}`
- `{Binding FeaturedWorkoutHasAction}`
- `{Binding FeaturedWorkoutHasMultipleActivities}`
- `{Binding FeaturedWorkoutIntensity}`
- `{Binding FeaturedWorkoutIsEditAction}`
- `{Binding FeaturedWorkoutIsRestDay}`
- `{Binding FeaturedWorkoutSubtitle}`
- `{Binding FeaturedWorkoutTitle}`
- `{Binding StartFeaturedWorkoutCommand}`

### `Controls/Workout/WorkoutSwapPanel.xaml`

Class: `MuscleCuties.UI.Controls.Workout.WorkoutSwapPanel` · `x:DataType="{vm:WorkoutSessionViewModel}"`

- `{Binding .}`
- `{Binding CanDismissSwap}`
- `{Binding CanInteract}`
- `{Binding CloseSwapPanelCommand}`
- `{Binding ConfirmSwapCommand}`
- `{Binding Equipment, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding FinishSessionCommand}`
- `{Binding HasSwapCandidates}`
- `{Binding HasSwapError}`
- `{Binding IsSelected}`
- `{Binding IsSwapLoading}`
- `{Binding MuscleMatch, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Name}`
- `{Binding PrimaryMuscles, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectSwapCandidateCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutSessionViewModel}}, x:DataType=vm:WorkoutSessionViewModel}`
- `{Binding ShowNoSwapCandidates}`
- `{Binding SwapCandidates}`
- `{Binding SwapConfirmText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SwapErrorText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SwapNeedsReload}`
- `{Binding SwapTargetName, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Swap_ReplaceFormat}`

### `Pages/Auth/LoginPage.xaml`

Class: `MuscleCuties.UI.Pages.Auth.LoginPage` · `x:DataType="{vm:LoginViewModel}"`

- `{Binding Email}`
- `{Binding ErrorMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ErrorMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding GoToRecoveryCommand}`
- `{Binding GoToRegisterCommand}`
- `{Binding InfoMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding InfoMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding IsAppleSignInAvailable}`
- `{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsBusy}`
- `{Binding LoginCommand}`
- `{Binding Password}`
- `{Binding SignInWithAppleCommand}`

### `Pages/Auth/OtpVerificationPage.xaml`

Class: `MuscleCuties.UI.Pages.Auth.OtpVerificationPage` · `x:DataType="{vm:OtpVerificationViewModel}"`

- `{Binding CanResend, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding CanResend}`
- `{Binding Email}`
- `{Binding ErrorMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ErrorMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding GoBackCommand}`
- `{Binding InfoMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding InfoMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsVerifying}`
- `{Binding OtpCode}`
- `{Binding ResendButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ResendCodeCommand}`
- `{Binding VerifyCommand}`

### `Pages/Auth/PasswordRecoveryPage.xaml`

Class: `MuscleCuties.UI.Pages.Auth.PasswordRecoveryPage` · `x:DataType="{vm:PasswordRecoveryViewModel}"`

- `{Binding CanResend, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding CanResend}`
- `{Binding ConfirmPassword}`
- `{Binding EmailOrUsername}`
- `{Binding ErrorMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ErrorMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding GoBackCommand}`
- `{Binding InfoMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding InfoMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsBusy}`
- `{Binding IsStep1}`
- `{Binding IsStep2, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsStep2}`
- `{Binding IsStep3}`
- `{Binding MaskedEmail}`
- `{Binding NewPassword}`
- `{Binding OtpCode}`
- `{Binding ResendButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ResendCodeCommand}`
- `{Binding ResetPasswordCommand}`
- `{Binding StartRecoveryCommand}`
- `{Binding VerifyCodeCommand}`

### `Pages/Auth/RegisterPage.xaml`

Class: `MuscleCuties.UI.Pages.Auth.RegisterPage` · `x:DataType="{vm:RegisterViewModel}"`

- `{Binding ConfirmPassword}`
- `{Binding Email}`
- `{Binding ErrorMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ErrorMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding GoBackCommand}`
- `{Binding GoToPrivacyPolicyCommand}`
- `{Binding HasAcceptedPrivacyNotice}`
- `{Binding IsAppleSignInAvailable}`
- `{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsBusy}`
- `{Binding Password}`
- `{Binding RegisterCommand}`
- `{Binding SignInWithAppleCommand}`
- `{Binding Username}`

### `Pages/Cycle/CyclePage.xaml`

Class: `MuscleCuties.UI.Pages.Cycle.CyclePage` · `x:DataType="{vm:CycleViewModel}"`

- `{Binding CurrentMonthLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding IsCycleWarningPopupVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsDatePhaseModalVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsPhaseSummarySheetVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsQuickLogSheetVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding OpenCycleSettingsCommand}`

### `Pages/Cycle/CyclePhaseDetailPage.xaml`

Class: `MuscleCuties.UI.Pages.Cycle.CyclePhaseDetailPage` · `x:DataType="{vm:CyclePhaseDetailViewModel}"`

- `{Binding BackCommand}`
- `{Binding CurrentPhase, Converter={StaticResource CyclePhaseToTextColorConverter}}`
- `{Binding Headline, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Hook, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding IconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}, ConverterParameter=Warning}`
- `{Binding IconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding IsWatchExpanded, Converter={StaticResource BoolToChevronConverter}}`
- `{Binding IsWatchExpanded}`
- `{Binding NutritionIconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding NutritionLead, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding NutritionPoints}`
- `{Binding NutritionSignalText, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Cycle_PhaseDetail_NutritionLevelFormat}`
- `{Binding NutritionSignalText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding NutritionSignal}`
- `{Binding PhaseIconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding PhaseImageSource}`
- `{Binding PhaseName, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Cycle_PhaseDetail_OverviewFormat}`
- `{Binding PhaseName, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseWindow, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhaseWindow}`
- `{Binding QuickRule, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RecoveryIconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding RecoverySignalText, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Cycle_PhaseDetail_RecoveryLevelFormat}`
- `{Binding RecoverySignalText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RecoverySignal}`
- `{Binding Text, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Title, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ToggleWatchCommand}`
- `{Binding TrainingIconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding TrainingSignalText, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Cycle_PhaseDetail_TrainingLevelFormat}`
- `{Binding TrainingSignalText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding TrainingSignal}`
- `{Binding WatchIconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}, ConverterParameter=Warning}`
- `{Binding WatchPoints}`
- `{Binding WhyLead, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WhyPoints}`
- `{Binding WorkoutLead, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WorkoutPoints}`

### `Pages/Dashboard/DailyCheckInPage.xaml`

Class: `MuscleCuties.UI.Pages.Dashboard.DailyCheckInPage` · `x:DataType="{vm:DailyCheckInViewModel}"`

- `{Binding BodyWeight}`
- `{Binding EnergyDisplay}`
- `{Binding ErrorMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ErrorMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding HasBloating}`
- `{Binding HasSessionSummary}`
- `{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsBusy}`
- `{Binding IsCompleted, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsCompleted}`
- `{Binding IsReadinessScoreDisplayEnabled, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsReadinessScoreDisplayEnabled}`
- `{Binding PlanningSleepDisplay}`
- `{Binding PlanningSorenessDisplay, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PlanningSorenessDisplay}`
- `{Binding PlanningSorenessSlider}`
- `{Binding ReadinessScoreText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ReadinessTierText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SessionSummaryText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SkipCommand}`
- `{Binding SleepDisplay, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Dashboard_CheckIn_SleepA11yFormat}`
- `{Binding SleepDisplay, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SleepHours}`
- `{Binding SubmitCommand}`

### `Pages/Dashboard/DashboardPage.xaml`

Class: `MuscleCuties.UI.Pages.Dashboard.DashboardPage` · `x:DataType="{vm:DashboardViewModel}"`

- `{Binding CycleCount, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DashboardPhaseHeaderText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Greetings, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding HasActiveInjuries}`
- `{Binding IsInjuryModalVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsRefreshing}`
- `{Binding NutritionStreakDays, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding OpenInjuryModalCommand}`
- `{Binding OpenPhotoVaultCommand}`
- `{Binding PhotoButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PhotoCheckInText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RefreshCommand}`
- `{Binding WorkoutStreakDays, Converter={StaticResource LocalizedStringConverter}}`

### `Pages/Nutrition/FoodLogPage.xaml`

Class: `MuscleCuties.UI.Pages.Nutrition.FoodLogPage` · `x:DataType="{vm:FoodLogViewModel}"`

- `{Binding ., Converter={StaticResource LocalizedStringConverter}}`
- `{Binding .}`
- `{Binding AddRecipeIngredientCommand}`
- `{Binding AddWaterCommand}`
- `{Binding BarcodeInputText}`
- `{Binding BarcodeScanStatus, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding BatchLogSelectedFoodsCommand}`
- `{Binding CalorieProgressRatio}`
- `{Binding CalorieRemainingText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Calories, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_Format_CaloriesValueKcal}`
- `{Binding Calories}`
- `{Binding CanCreateRecipes}`
- `{Binding CancelQuickAddCaloriesCommand}`
- `{Binding CancelRecipeBuilderCommand}`
- `{Binding CarbsProgressRatio}`
- `{Binding Carbs}`
- `{Binding ClearQuickFoodSelectionCommand}`
- `{Binding CloseBarcodeScannerCommand}`
- `{Binding CloseEditModalCommand}`
- `{Binding CopyYesterdayDiaryCommand}`
- `{Binding CopyYesterdayMealCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding DatePickerSelectedCommand}`
- `{Binding DateStripDays}`
- `{Binding DayName, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DayNumber, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DeleteFoodItemCommand}`
- `{Binding DeleteRecipeCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding DeleteTemplateCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding EditRecipeCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding EditTemplateCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding EditingCalories}`
- `{Binding EditingCarbs}`
- `{Binding EditingFat}`
- `{Binding EditingIngredients}`
- `{Binding EditingItemName}`
- `{Binding EditingMealType}`
- `{Binding EditingProtein}`
- `{Binding EditingServingSize}`
- `{Binding EditingServingsCount}`
- `{Binding FatProgressRatio}`
- `{Binding Fats}`
- `{Binding FoodSearchQuery}`
- `{Binding FoodSearchResults}`
- `{Binding FoodSearchStatus, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding FormattedSelectedDate, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Grams}`
- `{Binding HasItems, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding HasItems}`
- `{Binding HasRecentIngredients, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding HasRecentSuggestions, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding HasRecipePreview}`
- `{Binding HasSavedRecipes}`
- `{Binding HasSavedTemplates}`
- `{Binding HasSelectedQuickFoods}`
- `{Binding IsBarcodeScannerVisible}`
- `{Binding IsDatePickerOpen}`
- `{Binding IsEditModalVisible}`
- `{Binding IsEditingExisting, Converter={StaticResource BoolToEditTitleConverter}}`
- `{Binding IsEditingExisting, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsEditingExisting}`
- `{Binding IsExpanded, Converter={StaticResource BoolToChevronConverter}}`
- `{Binding IsExpanded}`
- `{Binding IsFastLoggingVisible}`
- `{Binding IsGroupedEditor}`
- `{Binding IsLoadError}`
- `{Binding IsLoadingFastLogging}`
- `{Binding IsLoading}`
- `{Binding IsManualEditorVisible}`
- `{Binding IsPlainFoodEditor}`
- `{Binding IsProcessingBarcode, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsProcessingBarcode}`
- `{Binding IsQuickAddCaloriesVisible}`
- `{Binding IsRecipeBuilderVisible}`
- `{Binding IsSearchingFood}`
- `{Binding IsSelectedDateToday, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsSelected}`
- `{Binding IsStatusVisible}`
- `{Binding IsToday}`
- `{Binding IsWriting, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsWriting}`
- `{Binding ItemCount, StringFormat='{0} items'}`
- `{Binding Items}`
- `{Binding JumpToTodayCommand}`
- `{Binding LogMealTemplateCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding LogRecipeCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding LookupBarcodeCommand}`
- `{Binding MealCategories}`
- `{Binding MealSections}`
- `{Binding MealType}`
- `{Binding Name}`
- `{Binding NavigateBackCommand}`
- `{Binding NextDayCommand}`
- `{Binding NotesText}`
- `{Binding OpenAddFoodCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding OpenBarcodeScannerCommand}`
- `{Binding OpenEditFoodCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding OpenQuickAddCaloriesCommand}`
- `{Binding OpenRecipeBuilderCommand}`
- `{Binding PerServingCalories, StringFormat='Per serving • {0:F0} kcal'}`
- `{Binding PickerDate}`
- `{Binding PreviewRecipeCommand}`
- `{Binding PreviousDayCommand}`
- `{Binding Protein, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_FoodLog_ValueGP}`
- `{Binding ProteinProgressRatio}`
- `{Binding Protein}`
- `{Binding QuickCaloriesName}`
- `{Binding QuickCaloriesValue}`
- `{Binding QuickCarbsValue}`
- `{Binding QuickFatValue}`
- `{Binding QuickProteinValue}`
- `{Binding RecentIngredients}`
- `{Binding RecentSuggestions}`
- `{Binding RecipeCookedWeightText}`
- `{Binding RecipeEditorIngredients}`
- `{Binding RecipeEditorStatus, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RecipeName}`
- `{Binding RecipePreviewSummary}`
- `{Binding RecipeServingsText}`
- `{Binding RemoveRecipeIngredientCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding RemoveWaterCommand}`
- `{Binding RetryLoadCommand}`
- `{Binding SaveFoodItemCommand}`
- `{Binding SaveMealAsTemplateCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding SaveNotesCommand}`
- `{Binding SaveQuickCaloriesCommand}`
- `{Binding SaveRecipeCommand}`
- `{Binding SavedMealTemplates}`
- `{Binding SavedRecipes}`
- `{Binding ScanBarcodeWithCameraCommand}`
- `{Binding SearchFoodCommand}`
- `{Binding SearchUsdaCommand}`
- `{Binding SelectDateCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding SelectedQuickFoodsSummaryText}`
- `{Binding ServingsCount, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_FoodLog_ValueServ}`
- `{Binding StatusMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SummaryText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding TargetCalories, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_Format_TargetCaloriesValueKcal}`
- `{Binding TargetCarbs, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_FoodLog_TargetValueG}`
- `{Binding TargetFat, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_FoodLog_TargetValueG}`
- `{Binding TargetProtein, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_FoodLog_TargetValueG}`
- `{Binding Title, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ToggleDatePickerCommand}`
- `{Binding ToggleMealSectionCommand, Source={RelativeSource AncestorType={x:Type vm:FoodLogViewModel}}, x:DataType=vm:FoodLogViewModel}`
- `{Binding TotalCalories, StringFormat='{0:F0} kcal'}`
- `{Binding TotalCalories, StringFormat='{0:F0}'}`
- `{Binding TotalCarbs, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_FoodLog_ValueG}`
- `{Binding TotalFat, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_FoodLog_ValueG}`
- `{Binding TotalProtein, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_FoodLog_ValueG}`
- `{Binding UseManualFoodCommand}`
- `{Binding WaterIntakeMl, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_FoodLog_ValueMl}`

### `Pages/Nutrition/NutritionPage.xaml`

Class: `MuscleCuties.UI.Pages.Nutrition.NutritionPage` · `x:DataType="{vm:NutritionViewModel}"`

- `{Binding .}`
- `{Binding CaloriesText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding HasNoMeals}`
- `{Binding IsBreakdownModalVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsMealDetailVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsMealEditorVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsMealEntryModalVisible}`
- `{Binding IsSuggestionModalVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding MacrosSummaryText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MealType, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Meals}`
- `{Binding Name, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Nutrition_Page_MealValueTapToViewBreakdown}`
- `{Binding Name}`
- `{Binding NavigateToFoodLogCommand}`
- `{Binding OpenMealBreakdownCommand, Source={RelativeSource AncestorType={x:Type vm:NutritionViewModel}}, x:DataType=vm:NutritionViewModel}`
- `{Binding OpenMealEntryCommand}`
- `{Binding Time, Converter={StaticResource LocalizedStringConverter}}`

### `Pages/Onboarding/ProfileSetupPage.xaml`

Class: `MuscleCuties.UI.Pages.Onboarding.ProfileSetupPage` · `x:DataType="{vm:ProfileSetupViewModel}"`

- `{Binding BirthDate}`
- `{Binding ErrorMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ErrorMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding FeetOptions}`
- `{Binding GroupedWorkoutActivityOptions}`
- `{Binding HasNoProfileImage}`
- `{Binding HasProfileImage}`
- `{Binding ImperialWeightOptions}`
- `{Binding InchesOptions}`
- `{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsBusy}`
- `{Binding MaxBirthDate}`
- `{Binding MetricHeightOptions}`
- `{Binding MetricWeightOptions}`
- `{Binding MinBirthDate}`
- `{Binding Name}`
- `{Binding ProfileImageSource}`
- `{Binding SelectImperialUnitsCommand}`
- `{Binding SelectMetricUnitsCommand}`
- `{Binding SelectedFeet}`
- `{Binding SelectedHeightCm}`
- `{Binding SelectedInches}`
- `{Binding SelectedWeightKg}`
- `{Binding SelectedWeightLbs}`
- `{Binding ToggleWorkoutActivityCommand}`
- `{Binding UseImperialSystem}`
- `{Binding UseMetricSystem, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding UseMetricSystem}`
- `{Binding WeightUnit, Converter={StaticResource LocalizedStringConverter}}`

### `Pages/Onboarding/QuizPage.xaml`

Class: `MuscleCuties.UI.Pages.Onboarding.QuizPage` · `x:DataType="{vm:QuizViewModel}"`

- `{Binding AccountCreatedNeedsLogin, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding AccountCreatedNeedsLogin}`
- `{Binding Answer.Text, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CanRetryQuestionsLoad}`
- `{Binding CurrentAnswers}`
- `{Binding CurrentQuestionIconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding CurrentQuestionText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding EnergySliderLabel, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Onboarding_Quiz_TrainingEnergyLevelValue}`
- `{Binding EnergySliderLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding EnergySliderValue, Mode=TwoWay}`
- `{Binding ErrorMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding GoToLoginCommand}`
- `{Binding HasError}`
- `{Binding IconGlyph, Converter={StaticResource StringToFluentIconImageSourceConverter}}`
- `{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsBusy}`
- `{Binding IsCurrentQuestionAvailableWeekdays}`
- `{Binding IsCurrentQuestionPhasePair}`
- `{Binding IsEmptyStateVisible}`
- `{Binding IsLayoutVisible}`
- `{Binding IsLoadingVisible}`
- `{Binding IsPreparingWorkoutPreferences}`
- `{Binding IsSelected}`
- `{Binding IsStandardAnswerListVisible}`
- `{Binding NextButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PainSliderLabel, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Onboarding_Quiz_DiscomfortLevelValue}`
- `{Binding PainSliderLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PainSliderValue, Mode=TwoWay}`
- `{Binding ProgressText, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Onboarding_Quiz_QuestionValue}`
- `{Binding ProgressText, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Onboarding_Quiz_QuizProgressValue}`
- `{Binding ProgressText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ProgressValue}`
- `{Binding QuestionsStateMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding QuestionsStateTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RetryLoadQuestionsCommand}`
- `{Binding SelectedWeekdayCount, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Profile_WorkoutPrefs_SelectedWeekdaysFormat}`
- `{Binding ShowQuizBackButton}`

### `Pages/Profile/PhotoJourneyVaultPage.xaml`

Class: `MuscleCuties.UI.Pages.Profile.PhotoJourneyVaultPage` · `x:DataType="{vm:PhotoJourneyVaultViewModel}"`

- `{Binding .}`
- `{Binding CloseComparisonCommand}`
- `{Binding ComparisonFirstImagePath}`
- `{Binding ComparisonSecondImagePath}`
- `{Binding DateFormatted, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DeletePhotoCommand, Source={RelativeSource AncestorType={x:Type vm:PhotoJourneyVaultViewModel}}, x:DataType=vm:PhotoJourneyVaultViewModel}`
- `{Binding HasNotes}`
- `{Binding HasWeight}`
- `{Binding IsComparisonOpen}`
- `{Binding IsImageAvailable, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsImageAvailable}`
- `{Binding IsImageHidden}`
- `{Binding IsImageShown}`
- `{Binding NavigateBackCommand}`
- `{Binding Notes}`
- `{Binding Photos}`
- `{Binding PrivacyBlurButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RevealedImagePath}`
- `{Binding SelectFirstComparisonPhotoCommand, Source={RelativeSource AncestorType={x:Type vm:PhotoJourneyVaultViewModel}}, x:DataType=vm:PhotoJourneyVaultViewModel}`
- `{Binding SelectSecondComparisonPhotoCommand, Source={RelativeSource AncestorType={x:Type vm:PhotoJourneyVaultViewModel}}, x:DataType=vm:PhotoJourneyVaultViewModel}`
- `{Binding ToggleItemRevealCommand, Source={RelativeSource AncestorType={x:Type vm:PhotoJourneyVaultViewModel}}, x:DataType=vm:PhotoJourneyVaultViewModel}`
- `{Binding TogglePrivacyBlurCommand}`
- `{Binding WeightFormatted, Converter={StaticResource LocalizedStringConverter}}`

### `Pages/Profile/ProfileFeedbackPage.xaml`

Class: `MuscleCuties.UI.Pages.Profile.ProfileFeedbackPage` · `x:DataType="{vm:ProfileFeedbackViewModel}"`

- `{Binding AdjustmentText}`
- `{Binding AttachmentName}`
- `{Binding AttachmentStatus, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding AttachmentStatus, Converter={StaticResource StringToBoolConverter}}`
- `{Binding BackCommand}`
- `{Binding ContactEmail}`
- `{Binding FeedbackText}`
- `{Binding HasAttachment}`
- `{Binding IncludeContactEmail}`
- `{Binding IsReadyToSend}`
- `{Binding Label, x:DataType=commonModels:ILabeledOption, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PriorityOptions}`
- `{Binding RemoveAttachmentCommand}`
- `{Binding ScreenName}`
- `{Binding SelectedPriorityOption}`
- `{Binding SelectedTopicOption}`
- `{Binding SendFeedbackCommand}`
- `{Binding StatusMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding StatusMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding TopicOptions}`

### `Pages/Profile/ProfileNutritionSettingsPage.xaml`

Class: `MuscleCuties.UI.Pages.Profile.ProfileNutritionSettingsPage` · `x:DataType="{vm:ProfileNutritionSettingsViewModel}"`

- `{Binding AdvancedSettingsButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding AreAdvancedSettingsVisible}`
- `{Binding BackCommand}`
- `{Binding CalciumGoal}`
- `{Binding CaloriesGoal}`
- `{Binding CarbsGoal}`
- `{Binding FatsGoal}`
- `{Binding FiberGoal}`
- `{Binding FolateGoal}`
- `{Binding GoalOptions}`
- `{Binding IronGoal}`
- `{Binding IsGlutenFree}`
- `{Binding IsGoalPaceVisible}`
- `{Binding IsLactoseFree}`
- `{Binding IsVegan}`
- `{Binding IsVegetarian}`
- `{Binding Label, x:DataType=commonModels:ILabeledOption}`
- `{Binding MagnesiumGoal}`
- `{Binding PotassiumGoal}`
- `{Binding ProteinGoal}`
- `{Binding SaveCommand}`
- `{Binding SelectedGoalOption}`
- `{Binding SelectedWeightGoalPaceOption}`
- `{Binding StatusMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding StatusMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding ToggleAdvancedSettingsCommand}`
- `{Binding ToggleGlutenFreeCommand}`
- `{Binding ToggleLactoseFreeCommand}`
- `{Binding ToggleVeganCommand}`
- `{Binding ToggleVegetarianCommand}`
- `{Binding VitaminAGoal}`
- `{Binding VitaminB12Goal}`
- `{Binding VitaminB6Goal}`
- `{Binding VitaminCGoal}`
- `{Binding VitaminDGoal}`
- `{Binding WaterGoal}`
- `{Binding WeightGoalPaceOptions}`
- `{Binding ZincGoal}`

### `Pages/Profile/ProfilePage.xaml`

Class: `MuscleCuties.UI.Pages.Profile.ProfilePage` · `x:DataType="{vm:ProfileViewModel}"`

- `{Binding AccountActionError, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CurrentPhase, Converter={StaticResource CyclePhaseToBrushConverter}}`
- `{Binding CurrentPhaseLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CyclesTracked, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Profile_Page_CyclesLoggedValue}`
- `{Binding CyclesTracked, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding HasAccountActionError}`
- `{Binding HasNoProfileImage}`
- `{Binding HasProfileImage}`
- `{Binding MemberSince, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding NutritionStreakDays, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Profile_Page_NutritionLogStreakValueDays}`
- `{Binding NutritionStreakDays, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ProfileImageSource}`
- `{Binding UserName}`
- `{Binding WorkoutStreakDays, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Profile_Page_WorkoutSessionStreakValueDays}`
- `{Binding WorkoutStreakDays, Converter={StaticResource LocalizedStringConverter}}`

### `Pages/Profile/ProfilePersonalInfoPage.xaml`

Class: `MuscleCuties.UI.Pages.Profile.ProfilePersonalInfoPage` · `x:DataType="{vm:ProfilePersonalInfoViewModel}"`

- `{Binding BackCommand}`
- `{Binding BirthDate}`
- `{Binding ChangePasswordCommand}`
- `{Binding ConfirmNewPassword}`
- `{Binding ContraceptionOptions}`
- `{Binding CurrentPassword}`
- `{Binding CycleLength}`
- `{Binding Email}`
- `{Binding HeightCm}`
- `{Binding Label, x:DataType=commonModels:ILabeledOption}`
- `{Binding MaxBirthDate}`
- `{Binding MinBirthDate}`
- `{Binding Name}`
- `{Binding NewPassword}`
- `{Binding PasswordStatusMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PasswordStatusMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding PregnancyOptions}`
- `{Binding SaveCommand}`
- `{Binding SelectedContraceptionOption}`
- `{Binding SelectedPregnancyOption}`
- `{Binding StatusMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding StatusMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding WeightKg}`

### `Pages/Profile/ProfilePrivacyPage.xaml`

Class: `MuscleCuties.UI.Pages.Profile.ProfilePrivacyPage` · `x:DataType="{local:ProfilePrivacyPage}"`

- `{Binding BackCommand}`

### `Pages/Profile/ProfileUnitsDisplayPage.xaml`

Class: `MuscleCuties.UI.Pages.Profile.ProfileUnitsDisplayPage` · `x:DataType="{vm:ProfileUnitsDisplayViewModel}"`

- `{Binding BackCommand}`
- `{Binding BodyWeightUnit}`
- `{Binding DistanceUnit}`
- `{Binding EnergyUnit}`
- `{Binding FoodMassUnit}`
- `{Binding HeightUnit}`
- `{Binding SaveCommand}`
- `{Binding SelectImperialUnitsCommand}`
- `{Binding SelectMetricUnitsCommand}`
- `{Binding StatusMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding StatusMessage, Converter={StaticResource StringToBoolConverter}}`
- `{Binding UnitSystemText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding UseImperialSystem}`
- `{Binding UseMetricSystem}`

### `Pages/Profile/ProfileWorkoutPreferencesPage.xaml`

Class: `MuscleCuties.UI.Pages.Profile.ProfileWorkoutPreferencesPage` · `x:DataType="{vm:ProfileWorkoutPreferencesViewModel}"`

- `{Binding .}`
- `{Binding AvailableWeekdays}`
- `{Binding BackCommand, Source={x:Reference Root}}`
- `{Binding CalendarColumn}`
- `{Binding CanSave}`
- `{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsBusy}`
- `{Binding IsSelected}`
- `{Binding Label}`
- `{Binding SaveActionText}`
- `{Binding SaveCommand}`
- `{Binding SelectedWeekdayCount, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Profile_WorkoutPrefs_SelectedWeekdaysFormat}`
- `{Binding ShortLabel}`
- `{Binding Source={RelativeSource AncestorType={x:Type vm:ProfileWorkoutPreferencesViewModel}}, Path=ToggleWeekdayCommand}`
- `{Binding StatusMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding StatusMessage, Converter={StaticResource StringToBoolConverter}}`

### `Pages/Startup/AppStartupPage.xaml`

Class: `MuscleCuties.UI.Pages.Startup.AppStartupPage` · `x:DataType="{local:AppStartupPage}"`


### `Pages/Workout/StrengthPlanGenerationPage.xaml`

Class: `MuscleCuties.UI.Pages.Workout.StrengthPlanGenerationPage` · `x:DataType="{vm:StrengthPlanGenerationViewModel}"`

- `{Binding .}`
- `{Binding AcceptProposalCommand}`
- `{Binding AccessibilityDescription}`
- `{Binding ActivityDecisions}`
- `{Binding Activity}`
- `{Binding AverageSessionsPerWeek}`
- `{Binding BackCommand}`
- `{Binding CanChooseSwimmingStyle}`
- `{Binding CanEditInputs}`
- `{Binding CanKeepCurrentPlan}`
- `{Binding CanOfferExploreOtherFeatures}`
- `{Binding CanReviewPreview}`
- `{Binding CanUseLongerDuration}`
- `{Binding ChosenStrengthSummary}`
- `{Binding ClosePreviewCommand}`
- `{Binding ConsistentWeeks}`
- `{Binding CooldownMinutes}`
- `{Binding CreateProposalCommand}`
- `{Binding DaysSinceLastSession}`
- `{Binding Day}`
- `{Binding Details}`
- `{Binding Duration}`
- `{Binding EditDetailsCommand}`
- `{Binding EvidenceYesNoChoices}`
- `{Binding ExpandActivityDecisions, Mode=OneWay}`
- `{Binding ExploreOtherFeaturesButtonText}`
- `{Binding ExploreOtherFeaturesCommand}`
- `{Binding FittingStrengthStyleChoices}`
- `{Binding Frequency}`
- `{Binding HasActiveOtherPreview}`
- `{Binding HasActivityDecisions}`
- `{Binding HasChosenStrengthSummary}`
- `{Binding HasDurationChoice}`
- `{Binding HasFittingStrengthStyleChoices}`
- `{Binding HasFrequency}`
- `{Binding HasMixedSchedule}`
- `{Binding HasNoMixedActivitySessions}`
- `{Binding HasPlanDetails}`
- `{Binding HasPreviewNotes}`
- `{Binding HasPreview}`
- `{Binding HasReasons}`
- `{Binding HasSavedAvailability}`
- `{Binding HasSavedStrengthActivity}`
- `{Binding HasStatusMessage}`
- `{Binding HasStrengthPreviewSessions}`
- `{Binding HasUnavailableApplyReason}`
- `{Binding HiitEvidenceCapture.AerobicSessionThresholdHelp}`
- `{Binding HiitEvidenceCapture.ClearanceScreeningChoices}`
- `{Binding HiitEvidenceCapture.ConfirmCurrentWeeks}`
- `{Binding HiitEvidenceCapture.InvalidAverageSessions}`
- `{Binding HiitEvidenceCapture.InvalidConsistentWeeks}`
- `{Binding HiitEvidenceCapture.InvalidDaysSinceLastSession}`
- `{Binding HiitEvidenceCapture.MissingClearance}`
- `{Binding HiitEvidenceCapture.PreviousFourWeeksAerobicEvidence}`
- `{Binding HiitEvidenceCapture.SelectedClearanceScreeningChoice}`
- `{Binding HiitEvidenceCapture.UnderlyingCardioAverageSessionsPerWeek}`
- `{Binding HiitEvidenceCapture.UnderlyingCardioConsistentWeeks}`
- `{Binding HiitEvidenceCapture.UnderlyingCardioDaysSinceLastSession}`
- `{Binding HiitEvidenceCapture.UnderlyingCardioExperienceHelp}`
- `{Binding HiitUnderlyingCardioActivities}`
- `{Binding InvalidSessionCount}`
- `{Binding IsBeforePreview}`
- `{Binding IsBusy}`
- `{Binding IsRequired}`
- `{Binding IsRockClimbingSelected}`
- `{Binding IsSwimmingSelected}`
- `{Binding IsTrainNavigationPending, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsTrainNavigationPending}`
- `{Binding IsTrainingDetailsExpanded}`
- `{Binding IsYogaSelected}`
- `{Binding KeepCurrentPlanCommand}`
- `{Binding Label}`
- `{Binding LongerDurationText}`
- `{Binding MissingClimbingCompetence}`
- `{Binding MissingClimbingInstruction}`
- `{Binding MissingHiitUnderlyingActivity}`
- `{Binding MissingSwimmingAbility}`
- `{Binding MissingSwimmingFacility}`
- `{Binding MissingSwimmingStyle}`
- `{Binding MissingYogaType}`
- `{Binding MixedActivityDays}`
- `{Binding Name}`
- `{Binding OpenTrainCommand}`
- `{Binding PreviewNotes}`
- `{Binding QualifyingManageableSessionCount}`
- `{Binding Reasons}`
- `{Binding RecentTrainingSummary}`
- `{Binding RecentTrainingWeeks}`
- `{Binding ReducedVolumeNoticeText}`
- `{Binding RequiresReducedVolumeAcknowledgment}`
- `{Binding SavedAvailabilitySummary}`
- `{Binding SelectedActivity.Description}`
- `{Binding SelectedActivity.Label}`
- `{Binding SelectedClimbingInstructionChoice}`
- `{Binding SelectedFittingStrengthStyle}`
- `{Binding SelectedHiitUnderlyingCardioActivity}`
- `{Binding SelectedIndependentSwimmingChoice}`
- `{Binding SelectedRopeClimbingCompetenceChoice}`
- `{Binding SelectedSwimmingFacilityAccessChoice}`
- `{Binding SelectedSwimmingStyleChoice}`
- `{Binding SelectedYogaContentChoice}`
- `{Binding SessionCountAccessibilityDescription}`
- `{Binding SessionCount}`
- `{Binding ShouldAskClimbingInstruction}`
- `{Binding ShouldShowHiitUnderlyingCardioChoice}`
- `{Binding Slots}`
- `{Binding StatusMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Status}`
- `{Binding StrengthPreviewSessions}`
- `{Binding Summary}`
- `{Binding SwimmingStyleChoices}`
- `{Binding TakeOverPreviewCommand}`
- `{Binding Title}`
- `{Binding ToggleTrainingDetailsCommand}`
- `{Binding TrainingDetailsToggleText}`
- `{Binding UnavailableApplyReason}`
- `{Binding UseLongerDurationCommand}`
- `{Binding UseShorterDurationCommand}`
- `{Binding WarmupMinutes}`
- `{Binding WeekLabel}`
- `{Binding YogaContentChoices}`

### `Pages/Workout/WorkoutHistoryPage.xaml`

Class: `MuscleCuties.UI.Pages.Workout.WorkoutHistoryPage` · `x:DataType="{vm:WorkoutHistoryViewModel}"`

- `{Binding .}`
- `{Binding AvailableMonths}`
- `{Binding CloseSessionDetailCommand}`
- `{Binding DetailDurationFormatted, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DetailKindLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DetailLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DetailRpeFormatted, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DetailStartedFormatted, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DetailVolumeFormatted, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DisplayName}`
- `{Binding DurationLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding EditableSets}`
- `{Binding EditingNotes}`
- `{Binding EditingRpe}`
- `{Binding ExerciseGroups}`
- `{Binding ExerciseName}`
- `{Binding HasNoSessions}`
- `{Binding IsDetailSheetVisible}`
- `{Binding IsEndReached}`
- `{Binding IsError}`
- `{Binding IsLoadingMore}`
- `{Binding IsLoading}`
- `{Binding IsSelected}`
- `{Binding IsStatusVisible}`
- `{Binding IsWarmup, Converter={StaticResource BoolToWarmupTextConverter}}`
- `{Binding LifetimeVolumeFormatted, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding LoadHistoryCommand}`
- `{Binding LoadNextPageCommand}`
- `{Binding NavigateBackCommand}`
- `{Binding OpenSessionDetailCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutHistoryViewModel}}, x:DataType=vm:WorkoutHistoryViewModel}`
- `{Binding PrimaryExercises}`
- `{Binding RepsInput}`
- `{Binding RpeRating, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_History_RpeFormat}`
- `{Binding RpeRating, Converter={StaticResource StringToBoolConverter}}`
- `{Binding SaveSessionNotesCommand}`
- `{Binding SaveSetEditCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutHistoryViewModel}}, x:DataType=vm:WorkoutHistoryViewModel}`
- `{Binding SelectMonthCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutHistoryViewModel}}, x:DataType=vm:WorkoutHistoryViewModel}`
- `{Binding SelectedSessionDetail.WorkoutName}`
- `{Binding Sessions}`
- `{Binding SetCountLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SetNumber, StringFormat='#{0}'}`
- `{Binding StartedAt, StringFormat='{0:ddd, MMM d, yyyy}'}`
- `{Binding StatusMessage, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding TotalSessionsCount, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding VolumeLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding VolumeSummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WeightInput}`
- `{Binding WorkoutName}`

### `Pages/Workout/WorkoutPage.xaml`

Class: `MuscleCuties.UI.Pages.Workout.WorkoutPage` · `x:DataType="{vm:WorkoutViewModel}"`

- `{Binding .}`
- `{Binding AccentColor}`
- `{Binding AccentTextColor}`
- `{Binding ActivityCards}`
- `{Binding BlockedCardioNames}`
- `{Binding BlockedExerciseNames}`
- `{Binding CurrentPhaseName, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Page_PhaseFormat}`
- `{Binding Date, StringFormat='{0:dd MMM}'}`
- `{Binding DayLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DismissStrengthStyleCommand}`
- `{Binding EmptyWorkoutsActionCommand}`
- `{Binding EmptyWorkoutsButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding EmptyWorkoutsButtonVisible}`
- `{Binding EmptyWorkoutsSubtitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding EmptyWorkoutsTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ErrorText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ExploreStrengthStyleCommand}`
- `{Binding Filters}`
- `{Binding GeneratePlanCommand}`
- `{Binding HasActiveInjuries}`
- `{Binding HasActivityChoices}`
- `{Binding HasBlockedCardio}`
- `{Binding HasBlockedExercises}`
- `{Binding HasError}`
- `{Binding HasInProgressWorkout}`
- `{Binding HasKnownCyclePhase}`
- `{Binding HasNoActivities}`
- `{Binding HasNoWorkouts}`
- `{Binding HasRehabExercises}`
- `{Binding HasStrengthStyleOffer}`
- `{Binding HasStyleOfferMessage}`
- `{Binding InProgressWorkout.Title}`
- `{Binding InjuryBannerSummary, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsBusy}`
- `{Binding IsInjuryBannerExpanded}`
- `{Binding IsInjuryModalVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsRefreshing, Mode=TwoWay}`
- `{Binding IsSelected}`
- `{Binding IsTabBarVisible}`
- `{Binding Label, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding MetricText}`
- `{Binding NavigateToInjuryLogCommand}`
- `{Binding OpenInjuryModalCommand}`
- `{Binding OpenWorkoutCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutViewModel}}, x:DataType=vm:WorkoutViewModel}`
- `{Binding PlanSummaryText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RefreshCommand}`
- `{Binding RehabExerciseNames}`
- `{Binding ResumeInProgressWorkoutCommand}`
- `{Binding RetryLoadCommand}`
- `{Binding ScheduleDays}`
- `{Binding SelectFilterCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutViewModel}}, x:DataType=vm:WorkoutViewModel}`
- `{Binding SelectScheduleDayCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutViewModel}}, x:DataType=vm:WorkoutViewModel}`
- `{Binding SelectWeekCommand}`
- `{Binding SelectedWeekIndex}`
- `{Binding ShowNoPlanPrompt}`
- `{Binding StyleOfferMessage}`
- `{Binding SummaryText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Title, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ToggleEasierAlternativeCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutViewModel}}, x:DataType=vm:WorkoutViewModel}`
- `{Binding ToggleInjuryBannerCommand}`
- `{Binding ToggleSessionOptionalCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutViewModel}}, x:DataType=vm:WorkoutViewModel}`
- `{Binding WeekTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WorkoutTag, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding WorkoutTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Workouts}`

### `Pages/Workout/WorkoutSessionPage.xaml`

Class: `MuscleCuties.UI.Pages.Workout.WorkoutSessionPage` · `x:DataType="{vm:WorkoutSessionViewModel}"`

- `{Binding .}`
- `{Binding AbandonPlannedSessionCommand}`
- `{Binding AccessibilityText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ActivityBackground}`
- `{Binding ActivityCards}`
- `{Binding ActivityIntroIntensity, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Session_IntensityFormat}`
- `{Binding ActivityPrescriptionText}`
- `{Binding AddRestTimerSecondsCommand}`
- `{Binding BackCommand}`
- `{Binding CadenceLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CanAbandonPlannedSession}`
- `{Binding CanInteract}`
- `{Binding CanMarkOptional}`
- `{Binding CanStartActivity}`
- `{Binding CelebrationText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CompleteSessionCommand}`
- `{Binding CurrentExercise.CuesText}`
- `{Binding CurrentExercise.HasQuickTips}`
- `{Binding CurrentExercise.Name}`
- `{Binding CurrentExercise.PreviousText, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Session_PreviousWeightFormat}`
- `{Binding CurrentExercise.PreviousText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CurrentExercise.QuickTipsText}`
- `{Binding CurrentExercise.RecommendationText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CurrentExercise.TargetText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CurrentExercise.UsesStrengthMetrics}`
- `{Binding CurrentExercise.WhyText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding CurrentExerciseIndex}`
- `{Binding CurrentExercise}`
- `{Binding CyclePhaseNote, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DayTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DismissCelebrationCommand}`
- `{Binding DistanceLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding DoseAdjustmentNote}`
- `{Binding DurationLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding EffortDisplayText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding EffortLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding EffortSliderValue, Mode=TwoWay}`
- `{Binding ErrorReference}`
- `{Binding ErrorText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ExercisePositionText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ExercisePreviewNames}`
- `{Binding ExerciseProgress}`
- `{Binding FinishActivityCommand}`
- `{Binding FinishSessionCommand}`
- `{Binding FinishTimedActivityCommand}`
- `{Binding HasCelebration}`
- `{Binding HasDoseAdjustmentNote}`
- `{Binding HasErrorReference}`
- `{Binding HasError}`
- `{Binding HasNoActivities}`
- `{Binding HasNoActivityPrescription}`
- `{Binding HeartRateLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Index}`
- `{Binding IsActivityIntro}`
- `{Binding IsActivitySummary}`
- `{Binding IsBusy}`
- `{Binding IsCurrent}`
- `{Binding IsDayOverview, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsDayOverview}`
- `{Binding IsExerciseFlow}`
- `{Binding IsLogged}`
- `{Binding IsOptional, Mode=TwoWay}`
- `{Binding IsOptional}`
- `{Binding IsRestTimerRunning, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsRestTimerRunning}`
- `{Binding IsRestTimerVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsRestTimerVisible}`
- `{Binding IsSkipped}`
- `{Binding IsSwapPanelVisible, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsTimedActivityRunning, Converter={StaticResource InvertedBoolConverter}}`
- `{Binding IsTimedActivityRunning}`
- `{Binding IsTipsExpanded}`
- `{Binding JumpToExerciseCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutSessionViewModel}}, x:DataType=vm:WorkoutSessionViewModel}`
- `{Binding LoggedCadenceRpmText, Mode=TwoWay}`
- `{Binding LoggedDistanceKmText, Mode=TwoWay}`
- `{Binding LoggedDurationMinutesText, Mode=TwoWay}`
- `{Binding LoggedHeartRateText, Mode=TwoWay}`
- `{Binding LoggedPaceText, Mode=TwoWay}`
- `{Binding LoggedPowerWattsText, Mode=TwoWay}`
- `{Binding LoggedRepsText, Mode=TwoWay}`
- `{Binding LoggedSetsText, Mode=TwoWay}`
- `{Binding LoggedWeightText, Mode=TwoWay}`
- `{Binding MaxOptionalSets}`
- `{Binding MetricText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding NextExerciseCommand}`
- `{Binding OpenSwapPanelCommand}`
- `{Binding OptionalSets, Mode=TwoWay}`
- `{Binding OptionalSets}`
- `{Binding PaceLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PauseRestTimerCommand}`
- `{Binding PowerLabel, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding PreviousExerciseCommand}`
- `{Binding RestTimerDisplay, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding RestTimerProgress}`
- `{Binding ResumeRestTimerCommand}`
- `{Binding RetryLoadCommand}`
- `{Binding SelectActivityCommand, Source={RelativeSource AncestorType={x:Type vm:WorkoutSessionViewModel}}, x:DataType=vm:WorkoutSessionViewModel}`
- `{Binding SelectedActivity.MetricText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SelectedActivity.Title, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SessionHistory}`
- `{Binding SkipExerciseCommand}`
- `{Binding StartActivityCommand}`
- `{Binding StartRestTimerCommand}`
- `{Binding StopRestTimerCommand}`
- `{Binding SummaryDuration, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Session_TimeFormat}`
- `{Binding SummaryDuration, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SummaryPrCount, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Session_WeightPrsFormat}`
- `{Binding SummaryPrCount, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SummaryStatusText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SummaryText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SummaryTitle, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding SummaryTotalVolume, Converter={StaticResource LocalizedFormatConverter}, ConverterParameter=Workout_Session_TotalVolumeFormat}`
- `{Binding SummaryTotalVolume, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding TipsButtonText, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding Title, Converter={StaticResource LocalizedStringConverter}}`
- `{Binding ToggleTipsCommand}`
- `{Binding UsesCadenceMetric}`
- `{Binding UsesCardioMetricGrid}`
- `{Binding UsesDistanceMetric}`
- `{Binding UsesDurationMetric}`
- `{Binding UsesDurationOnlyMetric}`
- `{Binding UsesEffortMetric}`
- `{Binding UsesHeartRateMetric}`
- `{Binding UsesPaceMetric}`
- `{Binding UsesPowerMetric}`
- `{Binding UsesStrengthMetrics}`
- `{Binding UsesWeight}`

### `Platforms/Windows/App.xaml`

Class: `MuscleCuties.UI.WinUI.App`


### `Resources/Styles/Colors.xaml`

Class: ``


### `Resources/Styles/Styles.xaml`

Class: ``

- `{Binding IsSelected}`

## Code-behind binding and context wiring

| Code-behind | Wiring |
| --- | --- |
| `Controls/Cycle/CycleCalendarCard.xaml.cs` | `BindingContextChanged += OnCardBindingContextChanged;`<br>`private void OnCardBindingContextChanged(object? sender, EventArgs e)`<br>`_observedViewModel = BindingContext as CycleViewModel;` |
| `Controls/Injury/InjuryLogContent.xaml.cs` | `protected override void OnBindingContextChanged()`<br>`base.OnBindingContextChanged();`<br>`if (BindingContext is not InjuryLogViewModel vm || ReferenceEquals(_subscribedViewModel, vm))` |
| `Controls/Injury/InjuryLogModal.xaml.cs` | `protected override void OnBindingContextChanged()`<br>`base.OnBindingContextChanged();`<br>`if (BindingContext is not InjuryLogViewModel vm || ReferenceEquals(vm, _subscribedViewModel))`<br>`if (!_hasContent || BindingContext is not InjuryLogViewModel vm)` |
| `Controls/Shared/PageLoadingOverlay.xaml.cs` | `BindingContextChanged += OnBindingContextChanged;`<br>`private void OnBindingContextChanged(object? sender, EventArgs args)`<br>`_observedViewModel = BindingContext as INotifyPropertyChanged;`<br>`IsVisible = BindingContext is IPageLoadAware page && (page.IsPageLoading || page.IsLoadError);` |
| `Pages/Auth/RegisterPage.xaml.cs` | `if (BindingContext is RegisterViewModel viewModel)` |
| `Pages/Cycle/CyclePage.xaml.cs` | `// binds to IPageLoadAware on BindingContext) is active from the` |
| `Pages/Nutrition/NutritionPage.xaml.cs` | `// binds to IPageLoadAware on BindingContext) is active from the` |
| `Pages/Onboarding/ProfileSetupPage.xaml.cs` | `this.BeginPageLoad(() => ((ProfileSetupViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));`<br>`((ProfileSetupViewModel)BindingContext).MarkBirthDateSelected();`<br>`if (BindingContext is not ProfileSetupViewModel viewModel)`<br>`var viewModel = (ProfileSetupViewModel)BindingContext;` |
| `Pages/Onboarding/QuizPage.xaml.cs` | `if (sender is Border { BindingContext: SelectableQuizAnswer answer })` |
| `Pages/Profile/ProfileFeedbackPage.xaml.cs` | `this.BeginPageLoad(() => ((ProfileFeedbackViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));`<br>`var vm = (ProfileFeedbackViewModel)BindingContext;` |
| `Pages/Profile/ProfileNutritionSettingsPage.xaml.cs` | `((ProfileNutritionSettingsViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));` |
| `Pages/Profile/ProfilePersonalInfoPage.xaml.cs` | `((ProfilePersonalInfoViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));`<br>`((ProfilePersonalInfoViewModel)BindingContext).MarkBirthDateSelected();` |
| `Pages/Profile/ProfilePrivacyPage.xaml.cs` | `BindingContext = this;` |
| `Pages/Profile/ProfileUnitsDisplayPage.xaml.cs` | `((ProfileUnitsDisplayViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));` |
| `Pages/Profile/ProfileWorkoutPreferencesPage.xaml.cs` | `var vm = (ProfileWorkoutPreferencesViewModel)BindingContext;` |
| `Pages/Workout/WorkoutPage.xaml.cs` | `// binds to IPageLoadAware on BindingContext) is active from the`<br>`SessionActivitiesOverlay.BindingContext = _sessionViewModel;`<br>`if (sender is not BindableObject { BindingContext: WorkoutActivitySectionItem activity } ||`<br>`if (modal.BindingContext != _viewModel.InjuryLogVm)`<br>`modal.BindingContext = _viewModel.InjuryLogVm;` |
| `Pages/Workout/WorkoutSessionPage.xaml.cs` | `BindingContext = _viewModel = vm;`<br>`if (sender is Slider { BindingContext: WorkoutExerciseItem exercise } slider)`<br>`if (sender is not Microsoft.Maui.Controls.Switch { BindingContext: WorkoutExerciseItem exercise }) return;`<br>`if (sender is not Stepper { BindingContext: WorkoutExerciseItem exercise }) return;` |

## Navigation routes

Registered in `MauiProgram.cs`; unchanged by this implementation:

