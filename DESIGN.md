# MuscleCuties — Design System

A warm, feminine design system built around cycle-phase awareness. The palette is rose-mauve at its core, with four distinct phase color sets that signal mood and energy rather than medical state. Typography mixes a serif display face (Fraunces) with a rounded geometric sans (Nunito) to balance softness with clarity.

---

## Typography

### Fonts

| Alias | File | Usage |
|---|---|---|
| `FrauncesDisplay` | `Nunito-Variable.ttf` | All headings, section titles, score numerals |
| `NunitoRegular` | `Fraunces-Variable.ttf` | Body copy, labels, buttons, inputs |

Both are variable fonts. Weight is controlled via `FontAttributes="Bold"` — no separate bold file is needed.

### Type Scale (Styles.xaml)

| Style key | Font | Size | Weight | Use |
|---|---|---|---|---|
| `Headline` | Fraunces | 30 | Bold | Page-level headings |
| `SubHeadline` | Fraunces | 26 | Bold | Tab page titles |
| `SectionTitle` | Fraunces | 18 | Bold | Card section titles |
| `CardTitle` | Nunito | 16 | Bold | Card primary labels |
| `EyebrowLabel` | Nunito | 10 | Bold | Uppercase tracking labels (always pair with `CharacterSpacing="1.2"`) |
| `BodyLabel` | Nunito | 14 | Regular | General body copy |
| `MutedLabel` | Nunito | 13 | Regular | Secondary descriptive text |
| `CaptionLabel` | Nunito | 12 | Regular | Metadata, timestamps, secondary values |
| `AccentLabel` | Nunito | 14 | Bold | Interactive labels, quiz answers, numeric emphasis |
| `ErrorLabel` | Nunito | 13 | Regular | Validation messages |

**Global defaults:** All `Label` elements default to `NunitoRegular / 14px / WordWrap` via the implicit style. Override with named styles where needed.

---

## Color Palette

### Brand

| Token | Light | Dark | Use |
|---|---|---|---|
| `Primary` | `#C85A87` | same | Main action color, icons, progress fills, accent text |
| `PrimaryDark` | `#A6446D` | — | Hover / pressed state for primary |
| `Secondary` | `#F8DFF1` | — | Soft blush tint, chip backgrounds |
| `Tertiary` | `#E8B4C8` | — | Subtle decorative fills |

### Surfaces

| Token | Light | Dark |
|---|---|---|
| `PageBackground` | `#FFF8FB` | `#2B1D24` |
| `CardSurface` | `#FFFFFF` | `#3A2931` |
| `AccentSurface` | `#FFF1F6` | `#3A2931` |
| `InputSurface` | `#F8EEF4` | `Gray900` |

### Text

| Token | Light | Dark |
|---|---|---|
| `TextPrimary` | `#2B1D24` | `#F8EEF4` |
| `TextSecondary` | `#8B6C79` | `#AE8D9B` |
| `TextAccent` | `#C85A87` | `#C85A87` |

### Warm Gray Scale (rose-tinted)

| Token | Value |
|---|---|
| `Gray100` | `#FFF8FB` |
| `Gray200` | `#F8EEF4` |
| `Gray300` | `#F0D8E2` |
| `Gray400` | `#D7B8C5` |
| `Gray500` | `#AE8D9B` |
| `Gray600` | `#8B6C79` |
| `Gray900` | `#4C3942` |
| `Gray950` | `#2B1D24` |

### Cycle Phases

Each phase has a background color, a text/icon color, and a brush for the `CyclePhaseToBrushConverter`.

| Phase | Light bg | Dark bg | Light text | Dark text |
|---|---|---|---|---|
| Menstrual | `#F9D6D8` | `#5A3840` | `#7A3A48` | `#F9D6D8` |
| Follicular | `#D6EED6` | `#2E5230` | `#3A6B3A` | `#D6EED6` |
| Ovulatory | `#FFF0C4` | `#5A4A00` | `#7A6000` | `#FFF0C4` |
| Luteal | `#E8D8F5` | `#3E2A58` | `#5A3B80` | `#E8D8F5` |
| Default | `#F8DFF1` | `#5A3C50` | — | — |

### Readiness & Recovery

Score-based color: ≥ 70 → green, 40–69 → amber, < 40 → red. Driven by `ReadinessScoreToColorConverter` and `RecoveryScoreToColorConverter`.

| Level | Light | Dark |
|---|---|---|
| High | `#58A873` | `#7FD197` |
| Medium | `#D9A441` | `#F0C15D` |
| Low | `#D16B6B` | `#F08B8B` |

### Nutrition Macros

| Macro | Light | Dark |
|---|---|---|
| Protein | `#A65AC8` | `#C792E6` |
| Carbs | `#E3A13B` | `#F1C46E` |
| Fats | `#6F8E4E` | `#9DB878` |
| Water | `#4F8FC8` | `#7DB5E1` |

---

## Component Library

### Cards

| Style key | Radius | Padding | Shadow opacity | Use |
|---|---|---|---|---|
| `CardPrimary` | 22 | 16 | 0.09 | Main content blocks |
| `CardSecondary` | 18 | 14 | 0.07 | Nested / supporting cards |

Shadow defaults: `Brush="#2B1D24"`, `Offset="0,4"`, `Radius="16"`. No shadow on phase-tinted or inset surfaces.

### Inputs

`InputContainer` — a `Border` wrapper around `Entry` or `Picker`.

- Background: `InputSurface` (light) / `Gray900` (dark)
- Stroke: `Transparent` (always — no border line)
- Corner radius: 14
- Padding: 16 (uniform)
- MinimumHeightRequest: 54

Entry and Picker inherit transparent background from the global implicit style; all color is supplied by the container.

### Buttons

**Default (filled):**
- Background: `Primary`
- Text: White
- Font: NunitoRegular Bold, 16
- Corner radius: 14
- Height: 52

**`ButtonLink`:**
- Background: Transparent
- Text: `Primary` / `SecondaryDarkText`
- Font: NunitoRegular, 14
- No border

### Filter Chips (Workout page)

`Border` with `StrokeShape="RoundRectangle 999"` (pill). State controlled by `DataTrigger` on `IsSelected`:
- Selected: background `Primary`, text White
- Unselected: background `Gray200` / `CardSurfaceDark`, text `TextSecondary`

### Progress Bars

- Calorie bar: `ProgressColor=Primary`, height 8
- Macro bars: `ProgressColor` bound to each macro's phase color, height 5
- Background: `Gray300`

### Readiness / Recovery Rings

Built from two overlaid `Border` elements with `StrokeShape="Ellipse"`:
1. Outer ring: `Stroke=Gray300`, `StrokeThickness="7"`, background transparent
2. Inner arc (accent): `Stroke` bound via converter, `StrokeThickness="7"`, `WidthRequest` proportional to score
Score label centered via a `Grid` overlay.

### Phase Calendar (Cycle page)

`CollectionView` with `GridItemsLayout Span="7"` (7 columns × 4 rows = 28 cells). Each cell is a `Border RoundRectangle 10` with phase background/text color. Today's cell adds a `Stroke=Primary` ring with `StrokeThickness="2"`.

### Tab Bar

Shell `<TabBar>` with 5 `ShellContent` items. Icons are SVG files in `Resources/Images/` — MAUI transcodes them to platform raster at build time.

| Tab | Icon file | Route |
|---|---|---|
| Today | `tab_today.svg` | `DashboardPage` |
| Cycle | `tab_cycle.svg` | `CyclePage` |
| Train | `tab_train.svg` | `WorkoutPage` |
| Nutrition | `tab_nutrition.svg` | `NutritionPage` |
| You | `tab_you.svg` | `ProfilePage` |

Tab bar colors: background `CardSurface`, selected `Primary`, unselected `Gray500`.

---

## Shell & Page Conventions

- `Shell.NavBarIsVisible="False"` on every content page — headers are custom-built in XAML
- Page padding: `20,56,20,32` (top 56 clears the status bar)
- Vertical stack spacing: 14 between top-level sections
- Section eyebrow labels: `EyebrowLabel` style + `CharacterSpacing="1.2"` + `Margin="4,6,4,0"`

---

## Value Converters

| Converter | Input | Output |
|---|---|---|
| `CyclePhaseToBrushConverter` | `CyclePhase` enum | `SolidColorBrush` for phase background |
| `ReadinessScoreToColorConverter` | `int` (0–100) | `Color` (green / amber / red) |
| `RecoveryScoreToColorConverter` | `int` (0–100) | `Color` (green / amber / red) |
| `StringToBoolConverter` | `string` | `bool` (non-empty = true) — from CommunityToolkit.Maui |
| `InvertedBoolConverter` | `bool` | `bool` (negated) — from CommunityToolkit.Maui |
