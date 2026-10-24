# MuscleCuties UI Style Enhancement & Visual Noise Reduction Plan (Flo + MacroFactor Reference)

## 1. Design Vision & Core Principles

This specification unifies the visual design across every screen and modal in `src/MuscleCuties.UI` by combining:
- **Flo App Reference (Warmth, Calmness & Organic Hierarchy):**
  - Soft, breathable surfaces on a warm blush/plum canvas (`PageBackground` / `PageBackgroundDark`).
  - Borderless primary cards (`CardPrimary`, `CardSecondary`) elevated by whisper-quiet diffused shadows (`Opacity="0.04"`, `Radius="14"`, `Offset="0,2"`) rather than heavy nested `StrokeThickness="1"` boxes ("box-in-a-box" visual noise).
  - Soft capsule pills (`CornerRadius="999"`) for cycle phase indicators, tags, and status badges.
  - Identical top-of-page header geometry and `42x42` circular icon buttons (`HeaderIconButton`) across all tabs.
- **MacroFactor App Reference (High-Signal Data Density & Fast Scannability):**
  - **Single Unified Font (`Inter`):** Every label, number, input, picker, button, and badge across the entire application uses one font family (`AppFontFamily = "Inter"`). Zero secondary font aliases (`OpenSansSemibold` removed) and zero inline `FontFamily` overrides.
  - **Strict 7-Step Typographic Scale:** Eliminates the 16 random font sizes previously scattered across XAML in favor of a disciplined, scannable hierarchy (`HeroDisplay 28`, `SubHeadline 24`, `SectionTitle 17`, `MetricValue 18`, `BodyLabel 15`, `CaptionLabel 13`, `MicroLabel 11`).
  - **Clean Metric & Macro Presentation:** Numbers (calories, macros, sets, reps, load, streaks, readiness scores) sit inside calm, borderless surface tiles (`WorkoutMetricTile` / `SurfaceTile`) with high-contrast values and muted 12–13pt unit/category labels below—zero decorative borders around individual numbers.
  - **Visual Noise Reduction:**
    - Remove double borders (bordered cards inside bordered containers).
    - Replace harsh `1pt` dividers with `8–16pt` whitespace or whisper-soft `Gray200` / `Gray800` hairlines (`HeightRequest="1"`, `Opacity="0.6"`).
    - Unify page padding (`20,36,20,16` for headers, `20,12,20,32` for scroll content, `MaximumWidthRequest="640"`).

---

## 2. Mandatory Safety & Architecture Invariants (`AGENTS.md`)

1. **Zero XAML Binding Regressions (`AGENTS.md` Rule 4.2):**
   - Do **NOT** rename or remove any `x:Name`, `AutomationId`, `{Binding ...}`, `Command`, `CommandParameter`, `Converter`, `DataTrigger`, or `x:DataType`.
   - Do **NOT** remove policy-required or workflow-audit-required elements documented in `docs/v1/UI_WORKFLOW_AUDIT.md` (e.g., `AuthOtpCountdownTopSpacing = "0,16,0,0"`, `AuthCodeCountdownTopSpacing = "0,8,0,0"`, `MinimumHeightRequest="52"` on MealEntryModal buttons, `PersonalInfoCaptionFontSize = 12` on `ProfilePersonalInfoPage.xaml`, Dinner plate icon, etc.).
2. **Single Font Enforcement:**
   - `MauiProgram.cs` registers only `"Inter"`.
   - Individual `.xaml` files must have **zero** inline `FontFamily="..."` attributes; all typography inherits `AppFontFamily` from `Styles.xaml`.

---

## 3. Global Design Tokens (`Styles.xaml`, `Colors.xaml`, `WorkoutPlanningResources.xaml`)

### 3.1 Single Font Token
- `<x:String x:Key="AppFontFamily">Inter</x:String>`
- Applied globally via implicit `Style` setters on `Label`, `Button`, `Entry`, `Editor`, `Picker`, `DatePicker`, `TimePicker`, `SearchBar`, `Span`, `RadioButton`, and `CheckBox`.

### 3.2 Typographic Scale
| Style Key | Size | Weight | Line Height / Spacing | Usage |
| :--- | :--- | :--- | :--- | :--- |
| `Headline` | `28` | `Bold` | `CharacterSpacing="-0.3"` | Hero numbers, onboarding splash titles |
| `SubHeadline` | `24` | `Bold` | `CharacterSpacing="-0.2"` | Top-of-page titles (`Today`, `Cycle`, `Train`, `Nutrition`, `You`) |
| `ModalTitle` | `20` | `Bold` | `CharacterSpacing="-0.1"` | Bottom-sheet and modal headers |
| `SectionTitle` / `CardTitle` | `17` | `Bold` | `CharacterSpacing="0"` | Card headers, section titles |
| `WorkoutMetricValue` | `18` | `Bold` | `CharacterSpacing="0"` | MacroFactor-style numeric readouts (macros, streaks, sets/reps) |
| `BodyLabel` (default `Label`) | `15` | `None` | `CharacterSpacing="0"` | Standard body copy, list rows, descriptions |
| `FormFieldLabel` / `AccentLabel` | `13` | `Bold` | `CharacterSpacing="0"` | Input labels, inline action links, active pills |
| `MutedLabel` / `CaptionLabel` | `13` | `None` | `CharacterSpacing="0"` | Subtitles, secondary metadata, units |
| `EyebrowLabel` | `11` | `Bold` | `CharacterSpacing="0.6"` | Uppercase/compact category eyebrows, phase tags |

### 3.3 Surface, Card & Control Tokens
- **`CardPrimary`:** `BackgroundColor="{AppThemeBinding Light={StaticResource CardSurface}, Dark={StaticResource CardSurfaceDark}}"`, `StrokeThickness="0"`, `Padding="16"`, `CornerRadius="18"`, `Shadow="Offset 0,2 | Radius 14 | Opacity 0.04"`.
- **`CardSecondary` / `SurfaceTile`:** `BackgroundColor="{AppThemeBinding Light={StaticResource AccentSurface}, Dark={StaticResource Gray900}}"`, `StrokeThickness="0"`, `Padding="12"`, `CornerRadius="14"`, no shadow.
- **`HeaderIconButton` / `CircleIconButton`:** `WidthRequest="42"`, `HeightRequest="42"`, `CornerRadius="21"`, `StrokeThickness="0"`, `BackgroundColor="{AppThemeBinding Light={StaticResource Gray200}, Dark={StaticResource CardSurfaceDark}}"`.
- **`PhasePillBadge` (`WorkoutPill`):** `CornerRadius="999"`, `Padding="10,5"`, `StrokeThickness="0"`, `BackgroundColor="{AppThemeBinding Light={StaticResource AccentSurface}, Dark={StaticResource Gray900}}"`.
- **`InputContainer`:** `CornerRadius="14"`, `MinimumHeightRequest="52"`, `Padding="14,2"`, `StrokeThickness="1"`, `Stroke="{AppThemeBinding Light={StaticResource InputStroke}, Dark={StaticResource InputStrokeDark}}"`.

---

## 4. Workstream Partitioning (6 Parallel Flash Subagents)

| Workstream | Owner | Target Pages & Controls | Key Visual Noise Reductions | Status |
| :--- | :--- | :--- | :--- | :--- |
| **WS0** | Principal Architect | `MauiProgram.cs`, `Colors.xaml`, `Styles.xaml`, `WorkoutPlanningResources.xaml`, `Controls/Shared/*` | Register single font `Inter`, establish Flo + MacroFactor tokens, calm shadows, borderless cards, unified buttons & inputs | Completed |
| **WS1** | Flash Subagent 1 | `Pages/Startup/AppStartupPage.xaml`, `Pages/Auth/*` (`LoginPage`, `RegisterPage`, `OtpVerificationPage`, `PasswordRecoveryPage`), `Pages/Onboarding/*` (`ProfileSetupPage`, `QuizPage`) | Unify auth card elevation, remove inline `FontFamily`, clean input spacing, preserve `AuthOtpCountdownTopSpacing` (16pt) & `AuthCodeCountdownTopSpacing` (8pt), calm quiz & setup cards | Completed |
| **WS2** | Flash Subagent 2 | `Pages/Dashboard/*` (`DashboardPage`, `DailyCheckInPage`), `Controls/Dashboard/*`, `Controls/Injury/*` | Fix `DashboardPage` double-40pt top padding (`16,40,16,12` + `16,40,16,32` -> `20,36,20,12` + `20,8,20,32`), unify `HeaderIconButton`, remove inline `FontFamily="Inter"`, de-clutter streak & readiness tiles | Completed |
| **WS3** | Flash Subagent 3 | `Pages/Cycle/*` (`CyclePage`, `CyclePhaseDetailPage`), `Controls/Cycle/*` | Flo-inspired soft phase cards, borderless calendar & phase explorer cards, clean bottom sheets (`CycleQuickLogBottomSheet`, `CycleDatePhaseModal`), consistent 20pt horizontal rhythm | Completed |
| **WS4** | Flash Subagent 4 | `Pages/Workout/*` (`WorkoutPage`, `WorkoutSessionPage`, `StrengthPlanGenerationPage`, `PhaseAdjustmentReviewPageV010`, `WorkoutHistoryPage`), `Controls/Workout/*` | MacroFactor-style set/rep/load tables, remove inline `FontFamily="Inter"`, unify `HeaderIconButton` (42x42), clean week selector pills & session cards | Completed |
| **WS5** | Flash Subagent 5 | `Pages/Nutrition/*` (`NutritionPage`, `FoodLogPage`), `Controls/Nutrition/*` | MacroFactor-inspired calorie/macro balance bar, borderless meal cards, clean `FoodLogPage` search/history rows, preserve 52pt `MealEntryModal` centered buttons & Dinner plate icon | Completed |
| **WS6** | Flash Subagent 6 | `Pages/Profile/*` (all 9 Profile pages), `Controls/Profile/*` | Remove heavy borders on Profile streak tiles (`StrokeThickness="0"`, soft surface fill), unify settings list rows & subpage headers, preserve `PersonalInfoCaptionFontSize = 12` on `ProfilePersonalInfoPage.xaml` | Completed |

