# Styling audit — 2026-11-09

Tip `f1918e138634ba39c2483bde3e1194b6d346f2a8` (`integration/beta-polish-20261109-v2`).

**This is visual-audit evidence, not a redesign.** No token, style or XAML was changed. Authority:
[DESIGN.md](../../DESIGN.md), [UI_PRESENTATION_DESIGN_SPEC.md](../UI_PRESENTATION_DESIGN_SPEC.md),
[GATE_REGISTER.md](../design/GATE_REGISTER.md), Soft Minimal implementation evidence.

**Limitation (read first):** the rendered app could not be audited. The iOS build is blocked in the agent sandbox (see
[OWNER_SIMULATOR_QA_2026-11-09.md](../release-audit/2026-11-09/OWNER_SIMULATOR_QA_2026-11-09.md), Q-01), so there are
**no screenshots** and no observation of layout, truncation, Dynamic Type, VoiceOver order, motion or keyboard insets.
This audit is a **static** pass over the 65 XAML files, the colour dictionary and the EN/RU catalogs. Every
rendered-only criterion is marked **NOT VERIFIED**. "Looks good" is not claimed anywhere. The `/impeccable` and
`/taste-skill` lenses need rendered screens, so they were not applied. `/taste-skill` is not installed in this session.

## Screen/state inventory and coverage

Shell destinations: Today (`DashboardPage`), Cycle (`CyclePage`), Train (`WorkoutPage`), Nutrition (`NutritionPage`),
You (`ProfilePage`). Static = XAML/resources inspected; Rendered = simulator capture (none possible).

| Area | Screens / sheets | Static | Rendered (light/dark · EN/RU · default/largest DT · states) |
| --- | --- | --- | --- |
| Startup | AppStartupPage | ✔ | NOT VERIFIED |
| Auth | Login, Register, OTP, Password recovery | ✔ | NOT VERIFIED |
| Onboarding | ProfileSetup (personal details + wellness ack), Quiz, first-plan result (StrengthPlanGenerationPage) | ✔ | NOT VERIFIED |
| Today | DashboardPage; Phase, Workout, Nutrition, Targets, Trend, ReadinessRecovery cards; DailyCheckIn modal; loading overlay | ✔ | NOT VERIFIED |
| Train | WorkoutPage (featured section/card, activity cards), StrengthPlanGeneration (preview/Apply), WorkoutSession, WorkoutHistory, WorkoutSwapPanel, InjuryLog modal | ✔ | NOT VERIFIED |
| Nutrition | NutritionPage, FoodLogPage, MealEntry/Editor/Detail/Suggestion/Breakdown modals, HydrationLogView, QuickFoodItemRow | ✔ | NOT VERIFIED |
| Cycle | CyclePage, CyclePhaseDetailPage, hero/calendar/tips/explorer, PhaseSummary sheet, DatePhase modal, QuickLog bottom sheet, Warning popup | ✔ | NOT VERIFIED |
| Profile / measurements | ProfilePage, PersonalInfo (measurements), UnitsDisplay, NutritionSettings, WorkoutPreferences, Privacy, Feedback, PhotoJourneyVault, ImageCrop modal | ✔ | NOT VERIFIED |
| Archive | Workout archive is served by WorkoutHistoryPage (no separate archive page) | ✔ | NOT VERIFIED |

No navigation failure could be observed without the app, so no screen is recorded as unreachable.

## Findings

| ID | Screen / state | Sev | Expected (DESIGN.md) | Actual | Evidence | Proposed fix | Gate impact |
| --- | --- | --- | --- | --- | --- | --- | --- |
| S-01 | Train featured card (preview count); plan generation ("Required" tag) | P1 | Role tokens with light/dark pairs; declared contrast pairs ≥ 4.5:1 for text | `TextColor="{StaticResource Primary}"` (#C85A87) with no dark variant on caption-size text: **3.99:1** on CardSurface #FFFFFF, **3.42:1** on CardSurfaceDark #3A2931, 4.04:1 on PageBackgroundDark | `Controls/Workout/FeaturedWorkoutCard.xaml:243`; `Pages/Workout/StrengthPlanGenerationPage.xaml:736`; WCAG relative-luminance computation in this run | Use the existing accent role pair already used elsewhere: `AppThemeBinding Light=TextAccent (#A6446D, 5.5:1), Dark=SecondaryDarkText/ActionSurfaceDark`. Extend `UiStyleGateTests` to reject bare `StaticResource` on `TextColor` | G12 visual / a11y |
| S-02 | All sheets/pages | P2 | One shared modal lifecycle (`AnimatedModalView`) with focus capture/restore and one title announcement | 18 surfaces use `AnimatedModalView`, but 21 native `DisplayAlert`/`DisplayActionSheet`/`DisplayPromptAsync` calls remain (ProfilePage 5, PhotoJourneyVault 5, MauiProgram 3, ProfileSetup 2, Quiz/Startup/Privacy/WorkoutPreferences/FoodLog/NutritionMealEntry 1 each) | `rg -c DisplayAlert…` | Inventory which are destructive confirmations or system errors (allowed native) and which are content; record the exception list in DESIGN.md by proposal, or migrate content alerts | Design debt |
| S-03 | Onboarding Continue footer; session error | P2 | Status/errors have one stable treatment and are announced, not mis-structured | 7 `ErrorLabel`s carry `SemanticProperties.HeadingLevel="Level3"`, so VoiceOver announces validation errors as headings; no announcement on appearance | `ProfileSetupPage.xaml:281-293`, `QuizPage.xaml`, `WorkoutSessionPage.xaml` | Drop the heading level on errors; announce with `SemanticScreenReader.Announce` when the message is set | G12 VoiceOver (unverified) |
| S-04 | Featured workout card | P2 | Low variance; sanctioned inline colour only for an enumerated role | 16 inline `TextColor` bindings in one control (89 app-wide, 87 via `AppThemeBinding`) | `Controls/Workout/FeaturedWorkoutCard.xaml` | Audit each against DESIGN.md's sanctioned-exception list; converge duplicates of the label style's own colour | Design debt |
| S-05 | Cycle quick log (RU) | P3 | RU semantics equal to EN | `Cycle_QuickLog_LoadingSavedEntry` RU "Загрузка сохраненного входа…" reads as "saved login" | `RussianUiText.*` | "Загрузка сохранённой записи…" | Localization |
| S-06 | RU long action labels | P2 (unverified) | Russian long strings must fit; no clipped actions | RU action labels up to 1.9× EN (e.g. "Сохранить пользовательскую еду" 30 ch; "Изменить отметку о начале менструации" 37 ch). Fit is not observable statically | Catalog scan | Verify in J14 at the largest Dynamic Type; shorten copy if clipped | J14 |
| S-07 | Onboarding birthday | P2 | Validation reason visible and accurate | Stale age error can persist after correction (see Q-02) | `ProfileSetupViewModel.cs:170-178` | Compare against the key, or clear on any valid date | E-01 |

## Design-system compliance summary (static)

| Area | Result | Basis |
| --- | --- | --- |
| Tokens in consumers | PASS | 0 raw hex colours and 0 literal `FontSize` in 63 page/control XAML files |
| Light/dark parity | FAIL ×2 | 2 bare `StaticResource Primary` text colours (S-01); the other 87 inline colours use `AppThemeBinding` |
| Buttons | PASS | Only `ButtonPrimary` (63 uses) and `ButtonSecondary` (181) |
| Cards | PASS | Only `CardPrimary` (60) and `CardSecondary` (80); `CardTitle` is a label style |
| Touch targets | PASS (static) | No `Button`/`ImageButton` `HeightRequest` < 44 |
| Fixed-width essential labels | PASS (static) | No `Label` with `WidthRequest` |
| Localization | PASS (static) | No literal `Text`/`Placeholder`/`Title`/semantic strings; EN 2036 = RU 2036 keys, 0 missing, 0 format-argument mismatches; 4 identical EN/RU values are brand names |
| Contrast | PARTIAL | Sampled pairs pass (TextSecondary/CardSurfaceSecondary 7.33, TextAccent/PageBackground 5.50, Error 6.59/6.73, Warning 6.53, Recovery 5.14, Disabled 6.71, selected choice 4.72); S-01 fails |
| Sheets/motion | PARTIAL | `AnimatedModalView` used by 18 surfaces; `MotionSettings.ReduceMotion` (UIAccessibility) branch in `ModalTransition.cs`; native alerts remain (S-02); motion behaviour NOT VERIFIED |
| Typography / Dynamic Type / RU expansion / VoiceOver order / safe-area & keyboard insets / empty-loading-error states | NOT VERIFIED | Requires rendered screens |

The automated design gates (`UiStyleGateTests`) could not be re-run here because `dotnet test` is blocked in the
sandbox. CI run 42687031242 is green at the tip; this audit did not confirm which CI job runs `UiStyleGateTests`.
