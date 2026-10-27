# MuscleCuties Presentation Design Specification

Working migration authority for the MuscleCuties iOS application. This document establishes the canonical design system governing all layouts, typography, spacing, color, shared components, and interactions across the application. Visual appearance, VoiceOver screen reading, and dynamic text accessibility remain deferred to explicit on-device authorization.

---

## 1. Page Structure & Layout

### Safe-Area & Container Strategy
- **Page Backgrounds:** All pages declare background styling bound to `{AppThemeBinding Light={StaticResource PageBackground}, Dark={StaticResource PageBackgroundDark}}`.
- **Standard Gutters & Insets:**
  - *Standard Content Pages (Dashboard, Workout, Nutrition, Cycle, Profile):* `PresentationPageContentPadding` (`20,8,20,32` or `20,12,20,32`).
  - *Form & Settings Pages (Profile Subpages, Check-In):* `PresentationFormPagePadding` (`20,12,20,32`).
  - *Authentication Screens (Login, Register, Recovery):* `PresentationAuthPagePadding` (`24,44,24,32`).
  - *Secondary Onboarding & Auth Verification (Quiz, Profile Setup, OTP):* `PresentationAuthSecondaryPagePadding` (`24,36,24,32`).
  - *Modal Sheets & Dialogs:* `PresentationModalPagePadding` (`20,16,20,32`) and `PresentationModalHeaderPadding` (`20,18,20,12`).
  - *Pinned / Sticky Action Footers:* `PresentationActionFooterPadding` (`20,12,20,36`).
- **Content Width Limit:** Standard page content and modal dialogs enforce a centered maximum width request via `PageContentMaxWidth` or `UiDimension640` (`640pt`) to prevent unwieldy stretched layouts on iPad or wide device windows.
- **Scroll Ownership:** Every scrollable view (`ScrollView`, `CollectionView`) is the sole scroll parent in its visual hierarchy. Headers and sticky action bars are pinned outside the scroll viewport (in top or bottom `Grid` rows) to avoid scroll contention and guarantee reachable actions.
- **Bottom Navigation Clearance:** Bottom content padding incorporates safe margin clearance (`32pt` bottom inset) to ensure interactive elements are not obscured by the native iOS home indicator bar or Shell TabBar.

---

## 2. Typography & Hierarchy

### Standard Font & Type Scale
- **Font Registration:** Single unified font family registered as `"Inter"` (backed by `OpenSans-Regular.ttf` in `MauiProgram.cs`) applied via `{StaticResource AppFontFamily}` across all controls.
- **Type Scale & Role Tokens:**
  - **Page Titles (`PageTitle`):** 24pt, bold/semibold, high contrast (`TextPrimary` / `TextPrimaryDark`).
  - **Section Titles (`SectionTitle`):** 18pt–20pt, bold/semibold, clear grouping indicator.
  - **Card Titles (`CardTitle`):** 16pt, semibold, primary surface hierarchy.
  - **Body Text (`BodyLabel`):** 14pt–15pt, regular, readable line height and wrapped text.
  - **Supporting / Muted Text (`MutedLabel`):** 12pt–13pt, regular, secondary emphasis (`TextSecondary` / `TextSecondaryDark`).
  - **Field Labels & Captions (`FieldLabel`, `CaptionLabel`):** 12pt–13pt, semibold or medium, unambiguous input associations.
  - **Metric Values & Accents:** 24pt–32pt, semibold/bold numerals paired with 11pt–12pt uppercase or muted unit captions.
  - **Buttons:** 15pt, semibold, centered, with minimum touch targets.
- **Text Growth & Accessibility:** No fixed-height containers on text-bearing elements. All `Label` controls use `LineBreakMode="WordWrap"` to support accessibility Dynamic Type enlargement and bilingual localized string expansion (Russian strings average 20–35% longer than English equivalents).

---

## 3. Spacing, Dimensions & Geometry

### Semantic Spacing Scale
- `UiSpaceMicro` (`4pt`): Icon-to-label gap, hairline badge padding.
- `PresentationFieldSpacing` / `UiSpaceTight` (`6pt`–`8pt`): Gap between field label and input container; micro chips.
- `UiSpaceCompact` (`12pt`): Internal component spacing, chip grids, metric tile gaps.
- `UiSpaceControl` / `PresentationFormSpacing` (`16pt`–`18pt`): Standard vertical gap between form fields, cards, and list rows.
- `UiSpaceContent` (`24pt`): Major vertical separation between distinct screen sections.

### Sizing & Shape Geometry
- **Touch Target Minimum:** `PresentationMinimumTouchSize` (`44pt`) strictly enforced on all interactive controls (buttons, back buttons, close icons, checkbox chips, day selectors) conforming to iOS Human Interface Guidelines.
- **Form Input Heights:** `PresentationInputMinimumHeight` (`52pt`) with horizontal padding `PresentationInputHorizontalPadding` (`16,0`).
- **Corner Radii:**
  - Standard Cards (`CardPrimary`, `CardSecondary`, `SelectableCard`): `UiRadiusControl` (`16pt`).
  - Form Fields (`InputContainer`, `SearchContainer`): `PresentationInputCornerRadius` (`12pt`).
  - Modal / Bottom Sheets: `BottomSheetTopCornerRadius` (`24,24,0,0`).
  - Badges & Pills: `UiRoundRectangleRadius999` (capsule pill).
- **Decorative & Status Icons:** `PresentationDecorativeIconSize` (`40pt`) for onboarding step banners and feature badges.

---

## 4. Color, Surface & State Semantics

### Authority & Surface Palette
- `Resources/Styles/Colors.xaml` is the sole color authority, loaded prior to `Styles.xaml` in `App.xaml`.
- **Primary Brand:** Rose accent (`Primary` `#C85A87`, `PrimaryDark` `#A6446D`).
- **Backgrounds:**
  - Page: Light `#FBF7F8` / Dark `#1E171B`.
  - Surface Primary (Cards): Light `#FFFFFF` / Dark `#2B2027`.
  - Surface Secondary: Light `#F5EEF1` / Dark `#382A33`.
  - Input Container: Light `#F5EEF1` / Dark `#352931`.
  - Modal Backdrop: Scrim with 45% black opacity (`#73000000`).
- **Semantic Feedback States:**
  - Success / Recovery: `Success` / `RecoveryHigh` `#2E7D32` (Green).
  - Warning / Moderate: `Warning` / `RecoveryModerate` `#E65100` (Amber).
  - Error / Destructive: `Error` / `RecoveryLow` `#C62828` (Red).
  - Cycle Phases: Distinct thematic contrast pairs (`MenstrualPhase`, `FollicularPhase`, `OvulatoryPhase`, `LutealPhase`) preserved for user cycle recognition without relying on color alone (always accompanied by textual phase labels).

---

## 5. Shared Components & Presentation Contracts

### Standardized Controls
1. **Header Component (`PageHeader.cs`):** Uniform title placement, subtitle support, and standardized `HeaderIconButton` back/action buttons (`44x44pt` minimum touch boundary).
2. **Cards (`CardPrimary`, `CardSecondary`, `SelectableCard`):**
   - Strictly inherit `Padding="{StaticResource UiInsets18}"` and `UiRadiusControl` (`16pt`) without local inline overrides.
   - Whisper-quiet elevation shadow in light mode (`0.04` opacity), flat subtle border in dark mode (`#443540`).
3. **Action Buttons (`ButtonPrimary`, `ButtonSecondary`):**
   - Primary: High-contrast rose fill, white text, 50pt standard height, 12pt corner radius.
   - Secondary: Muted surface fill, primary text, 50pt standard height.
   - Busy State: Primary buttons disable duplicate submissions and show activity spinner overlays when `IsBusy` is active.
4. **Form Containers (`InputContainer`, `PickerContainer`, `EditorContainer`):**
   - Uniform border styling (`UiStrokeWidth1`, `BorderSubtle`), background tint, and inner padding.
   - Validation messages rendered directly adjacent to the input field using `ErrorLabel` style.
5. **Modal & Bottom Sheet System:**
   - Standardized `ModalBackdrop` (scrim), `ModalSurface` (elevated bottom or centered card), `PresentationModalHeaderPadding` (`20,18,20,12`), and `PresentationModalPagePadding` (`20,16,20,32`).
   - Visible close/dismiss action button always available in header or dedicated dismiss tap area.

---

## 6. Interaction Consistency & State Coverage

- **Predictable Action Hierarchy:** Primary positive action (e.g. Save, Apply, Check-In) pinned at the bottom in `PresentationActionFooterPadding` or prominently positioned at the bottom of the card/modal.
- **Consistent Back / Dismissal:** Back button always located at top-left of `PageHeader` using standard left chevron; modal close button always located at top-right or explicit bottom secondary button.
- **State Variations Fully Covered:**
  - *Loading:* Centered `PageLoadingOverlay` or skeleton placeholder preserving layout stability.
  - *Empty State:* Descriptive illustration or icon paired with clear title and single action button.
  - *Error State:* Inline alert banner (`CardSecondary` with warning/error border) or full-page error view with retry command.
  - *Disabled State:* Reduced opacity (`0.4`–`0.5`) with native command `CanExecute` gating.

---

## 7. Wave Migration Contracts & Superseded Token Elimination

### Canonical Presentation Tokens (Declared at Top of `Styles.xaml`)
- `PresentationPageHeaderPadding` (`20,36,20,12`)
- `PresentationPageContentPadding` (`20,8,20,32`)
- `PresentationFormPagePadding` (`20,12,20,32`)
- `PresentationAuthPagePadding` (`24,44,24,32`)
- `PresentationAuthSecondaryPagePadding` (`24,36,24,32`)
- `PresentationModalHeaderPadding` (`20,18,20,12`)
- `PresentationModalPagePadding` (`20,16,20,32`)
- `PresentationActionFooterPadding` (`20,12,20,36`)
- `PresentationInputHorizontalPadding` (`16,0`)
- `PresentationFieldSpacing` (`6`)
- `PresentationFormSpacing` (`18`)
- `PresentationDecorativeIconSize` (`40`)
- `PresentationMinimumTouchSize` (`44`)
- `PresentationInputMinimumHeight` (`52`)
- `PresentationInputCornerRadius` (`12`)

### Superseded Tokens Removed (Zero Callers Verified)
All 17 superseded, ad-hoc, value-shaped tokens have been safely deleted from `Styles.xaml` without leaving competing presentation systems:
- Phase 1 (6 tokens): `UiInsets20_8_20_32`, `UiInsets24_36_24_32`, `UiInsets24_44_24_32`, `UiInsets20_12_20_32`, `UiInsets20_36_20_12`, `RecoveryTipMaxLines`.
- Phase 2 (11 tokens): `UiInsets18_16_18_12`, `UiInsets20_12_20_36`, `UiInsets20_16_16_12`, `UiInsets20_16_20_32`, `UiInsets20_18_20_12`, `UiInsets20_18_20_14`, `UiInsets24_16_24_32`, `UiInsets24_24_24_36`, `UiInsets24_32_24_16`, `UiInsets20_24_20_28`, `UiInsets20_24_20_32`.

---

## 8. Verification & Release Gate Status

- **Static XAML Cleanliness:** 100% of 67 XAML files pass automated static checks:
  - 0 undefined StaticResource references.
  - 0 forward-reference hazards in `Styles.xaml`.
  - 0 local surface overrides (`Padding`, `BackgroundColor`, `StrokeShape`, `Stroke`) on canonical `CardPrimary` elements.
  - 0 input appearance violations.
- **Deferred Authorization Gates:**
  - `dotnet test` and iOS compilation (`dotnet build -f net10.0-ios`) deferred pending explicit user instruction.
  - Runtime VoiceOver screen reader announcements, dynamic accessibility text resizing, and light/dark mode contrast verification on physical iOS hardware remain deferred.
