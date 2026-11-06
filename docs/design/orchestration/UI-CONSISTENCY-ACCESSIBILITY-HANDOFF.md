# UI consistency, accessibility and units — implementation handoff

Dated 2026-11-06. This is a scoped implementation record, not a design-authority change and not a
device acceptance claim. It resolves F01, S01, S02 and F06 from the
[app-wide audit](../APP_WIDE_CONSISTENCY_AUDIT_2026-11-04.md) without changing the approved
Soft Minimal values.

## Package record

| Field | Value |
| --- | --- |
| Package | UI-CONSISTENCY-ACCESSIBILITY-20261106 |
| Accountable role | Product A — mobile UI and accessibility engineer |
| Base revision | `210f41fd7834b5daf98bf4e2f77cd3025a2e1981` (`main` merge #33) |
| Working branch | `feature/ui-consistency-accessibility` |
| Owned paths | `Controls/`, `Resources/Styles/`, page header composition, `InjuryLogViewModel`, `InjuryLogContent.xaml`, `DashboardViewModel.Trends`, `tests/MuscleCuties.Core.Tests/` |
| Excluded paths | Server, DbMaintenance, `.github/workflows`, food-log and workout-generation logic |
| Shared-file reservations | None. `PageHeader`, `UnitsFormatter` and the two catalog families are additive and consumed only by product UI. |

## What changed

### F01 — injury answers are three visible states

`InjuryLogContent.xaml` no longer renders a boolean switch. The clinician-advice answer is a
three-cell segmented control bound to `ClinicianAdviceOptions` (Unanswered / Yes / No). Each cell
uses `ChoiceCellCompact` and `ChoiceCellLabel`, so the shared style owns the selected surface and
exposes `Common_Selected` / `Common_NotSelected` as the accessibility hint. `InjuryAdviceOption`
carries a `bool?` value; `SelectedClinicianAdvice` remains nullable and an unanswered answer is
never presented as an explicit No. A single `OnSelectedClinicianAdviceChanged` handler keeps the
three cells in step with an edit or a reset.

### S01 — accessible names and selection state

- Food Log's Previous Day and Next Day borders now set `AutomationProperties.Name` as well as the
  localized description.
- `PageHeader` sets both the automation name and the description on its back button, so the four
  migrated detail headers inherit a named control.
- `HeaderIconButton` documents the per-instance contract: the style owns geometry and surface; each
  instance supplies the catalog-backed action name.
- `QuickFoodItemUiModel` exposes `SelectionStateText`; the selection target publishes it through
  `SemanticProperties.Hint`, so selection is a state trait rather than a change buried in the name.

### S02 — one shared page header

`PageHeader` now owns the whole header contract:

- `ShowBack` — a root page omits the back column instead of faking a disabled control.
- `Title` / `Subtitle` — updated in place, so a bound title does not rebuild the tree.
- `ActionContent` — the page's single contextual action, including an existing accessible
  `HeaderIconButton` Border, instead of a second hand-built header column.
- `StatusContent` — a compact status view on the subtitle line (the Workout phase `PillBadge`);
  when present it owns that line and the text subtitle steps back.
- `ActionText` / `ActionCommand` — a text action rendered with `ButtonSecondary`.
- Level-one heading semantics and the localized back name stay inside the control.

Eight root/detail pages moved onto it: Dashboard, Profile, Nutrition, Food Log, Workout,
Workout History, Photo Journey Vault and Cycle. Titles, subtitles, back commands and contextual actions are
preserved; the previously explicit `SemanticProperties.HeadingLevel="Level1"` labels are now
provided by the shared control.

### Page-header audit (24 pages)

| Page | State |
| --- | --- |
| DashboardPage | Migrated — root (`ShowBack=False`), heading subtitle, injury action |
| ProfilePage | Migrated — root |
| NutritionPage | Migrated — root |
| CyclePage | Migrated — root, month title, settings action |
| Nutrition/FoodLogPage | Migrated — back, Today action and date-picker action |
| Workout/WorkoutHistoryPage | Migrated — back, refresh action |
| Profile/PhotoJourneyVaultPage | Migrated — back, privacy action |
| Cycle/CyclePhaseDetailPage | Already shared |
| Workout/StrengthPlanGenerationPage | Already shared |
| Profile/ProfilePersonalInfoPage | Already shared |
| Profile/ProfileNutritionSettingsPage | Already shared |
| Profile/ProfileWorkoutPreferencesPage | Already shared |
| Profile/ProfileUnitsDisplayPage | Already shared |
| Profile/ProfileFeedbackPage | Already shared |
| Profile/ProfilePrivacyPage | Already shared |
| Auth/LoginPage, Auth/OtpVerificationPage, Auth/PasswordRecoveryPage, Auth/RegisterPage | Not applicable — auth root frame without a back action or level-one page title |
| Onboarding/ProfileSetupPage, Onboarding/QuizPage | Not applicable — progressive onboarding frame; the step indicator and progress belong to the flow, not the header |
| Startup/AppStartupPage | Not applicable — bootstrap state, no title or action |
| Workout/WorkoutPage | Migrated — root, phase PillBadge in the status slot, injury action |
| Workout/WorkoutSessionPage | Not applicable — session frame owns timing, exit and save actions |

The seven pages the audit named for heading semantics (Dashboard, Vault, Profile, Food Log,
Nutrition, Workout, History) all expose a Level1 heading through `PageHeader`.

### F06 — units preference

`MeasurementUnits` (Core) holds the exact conversion factors and canonical round trips.
`UnitsFormatter` (UI) is the display seam: it converts, formats locale-aware values and supplies
the catalog-backed unit label, so no view model imports `Core.Domain` or hardcodes a symbol.

- Dashboard trends show weight in kg/lb, distance in km/mi and pace per the selected distance,
  using the saved `IProfileUnitsUseCase` preference. A preference read failure keeps the last known
  system and logs only the exception type.
- The dashboard's rolling weight summary is formatted the same way and its literal copy moved to
  the catalog.
- Personal info loads height and weight in the saved system (cm/in, kg/lb), shows the matching unit
  as the field hint, converts back to cm/kg on save, and keeps canonical storage unchanged.
- New catalog keys: `Common_Unit_Km`, `Common_Unit_Lb`, `Common_Unit_Mi` and
  `Dashboard_Weight_TrendMedianFormat` in EN and RU; `Dashboard_Trend_LoggedWeeksFormat` takes the
  unit label as a third argument in both languages.

## Verification

| Gate | Command | Result |
| --- | --- | --- |
| Core and UI source guards | `dotnet test tests/MuscleCuties.Core.Tests/` (SDK 10.0.200) | **1202 passed, 0 failed** |
| Workout header | `Workout_header_uses_the_shared_status_slot` | PageHeader + status slot + PillBadge asserted |
| Unit formatting | `MeasurementUnitsTests` | Round trips, metric pass-through, distance/pace and label cases pass |
| XAML well-formedness | `xml.etree.ElementTree` over all 67 UI XAML files | 0 parse errors |
| Catalog parity | `LocalizationCatalogTests` | EN/RU key sets and format arguments match |
| iOS compilation | `dotnet build src/MuscleCuties.UI -f net10.0-ios` | **Blocked** — `NETSDK1147: maui-tizen, wasm-tools` workloads are unavailable in this Linux environment; no iOS workload can be installed here |
| Rendered UI / VoiceOver | Owner device run | **Unverified** — requires a macOS build and the steps in `scripts/verify-ui-consistency-accessibility.sh` |

The source guards prove the structure; they do not prove VoiceOver traversal, announcements,
focus order, dynamic type or the visual result. Those remain owner device gates under AGENTS.md.

## Next action

Build on the owner's Mac, run the device-verification wizard, and record the outcome. No S02
header item remains; any further UI package is outside this scope.
