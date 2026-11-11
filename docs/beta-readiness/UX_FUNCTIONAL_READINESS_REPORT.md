# UX / functional readiness report — regenerated 2026-11-10

Source of truth: current worktree code plus `.claude/qa-evidence/ORCHESTRATOR_CHECKPOINT.md`. Verdict: **NO-GO** for internal TestFlight.

## Closed and simulator-verified earlier
- F01 bodyweight logging: Dead Bug shows a real 0 and logs 0 kg; weighted lifts without history still require an explicit weight.
- F05 consumed totals: Today and Nutrition agree (130 kcal / 3-28-1 g); hydration rows no longer mask food.
- F05/F09 target: Today, Nutrition and the Food Diary agree at 2 320 kcal disconnected and 2 370 connected; the diary uses the server target and does not yet add the on-device Health adjustment.
- F06 action labels: completed -> "Открыть тренировку", in progress -> "Продолжить тренировку".
- Ring fill: the progress-ring sweep direction is fixed on Today; owner confirmed all rings.
- Check-in: rationale card before any Apple prompt; Connect pre-fills 6,2 h; manual value is not overwritten; privacy Health card connect/disconnect works.
- F08: a `NeedsReview` reconcile is a successful reconcile; the injury screen no longer claims the plan was not updated (fix in `CloudDailyCheckInAndCycleStores.RefreshPlanAfterInjuryChangeAsync`).

## Changed in this pass (compiled, not yet simulator-verified)
- F07r: a stored authenticated session now wins over a cached onboarding-incomplete flag. Both `App.InitializeAndRouteAsync` and `AppShell.VerifyAuthenticatedNavigationAsync` require `IsLoggedInAsync()` to be false before routing to ProfileSetup; otherwise a returning user goes to the dashboard. Failure paths still surface `AppStartupPage.ShowStartupError`.
- P2 localization: injury pain levels, injury empty states/form title, delete/clear failures and the meal log/save buttons now resolve through catalog keys. RU meal button shortened to "Приём пищи" / "Сохранить" to stop the truncated primary action.
- Catalog parity: 2 085 EN keys / 2 085 RU keys, zero one-sided keys.

## Open P1
- F04 clean finish: the spinner binds to `IsBusy` only, but a full in-app finish remains unverified because the reopened session returns 409 on duration mismatch.
- C01 ovulation: `DashboardSupportRule.BuildCycleInsightText` still states an ovulation date without an estimate marker; the fix needs an estimate flag on the cycle metrics/DTO.
- S-04: in-session pain/injury reporting is owner-deferred.

## Open P2 / risks
- WP10: `WorkoutViewModel.PlanSummaryText` counts scheduled non-rest days correctly; the requested-vs-scheduled difference originates in plan generation. `WorkoutOverviewResponse` carries no withhold reason, so a truthful disclosure needs a new field (integration-authority DTO change). No display workaround applied.
- RU 12-hour times: format strings are culture-aware (`NutritionViewModel.cs:342`, `FoodLogUiModels.cs:85`); the 12-hour output means the process culture is not Russian while copy is translated. Fix belongs to the app-wide language/culture switch.
- Duplicate "Назад": only one binding exists in `WorkoutSessionPage.xaml:22`; the second control seen in QA comes from another surface that was not reproduced.
- Check-in save hint and duplicate "Питание" header were not reproduced from the current XAML; both remain unverified.

## Evidence
`.claude/qa-evidence/screenshots/` (today-badge-state2.png, health-card-disconnected.png, checkin-prefill.png, diary-target-2320.png, rings-fixed-today.png) and `ORCHESTRATOR_CHECKPOINT.md`.

## Addendum 2026-11-10 (culture / chrome / generation trace)
- RU 24-hour time: `LocalizationService.SetLanguage` now assigns the process and thread cultures (`ru-RU`/`en-US`) alongside the copy language, so `ToString("t")` renders 24-hour in Russian. Format strings were not rewritten.
- Duplicate chrome: the redundant Nutrition page header was deleted from `NutritionPage.xaml` (the `PageHeader` block); the shell tab remains the single "Питание" label. For the exercise screen, every source was inspected: `WorkoutSessionPage.xaml:14` (`Shell.NavBarIsVisible="False"`), `:22` (the only back button), no `ToolbarItem`, no `ContentPage` base style, and `AppShell.xaml` (`ShellContent` for Workout has `Shell.NavBarIsVisible="False"`). No second in-repo source exists.
- WP10 FIXED: `WeekScheduleRule.MapArchetypesToDays` used to schedule exactly `strengthSlots.Length` days. It now builds `scheduledSlots` by cycling the template rotation to `distinctAvailableDays.Length`, so every requested weekday is filled. Harness against the HEAD rule and the patched rule: BEFORE requested=3 scheduled=2 (Monday, Wednesday); AFTER requested=3 scheduled=3 (Monday, Wednesday, Thursday).

- F07r SEAM: `MuscleCuties.Core/Domain/StartupRoutingRule.cs` (`StartupRoutingRule.Resolve`) is now called by both `App.InitializeAndRouteAsync` and `AppShell.VerifyAuthenticatedNavigationAsync`. Diagnostic output: no session + incomplete -> ProfileSetup (unchanged first run); no session + complete -> Login; token + incomplete -> Dashboard (pre-fix ProfileSetup); token + complete -> Dashboard; failed state read -> StartupError.

## End-to-end harness 2026-11-10 (in-memory, deleted afterwards)
- WP10 through the real `WeekPlanRule.BuildWeek`: a Mon/Wed/Thu request (3 days) with a two-archetype template produced **3 scheduled non-rest days** (Monday, Wednesday, Thursday). The HEAD mapping produced 2 (Monday, Wednesday). No downstream cap truncated the sessions.
- F07r measured against the HEAD branch (`git show HEAD:App.xaml.cs`, lines 195-199: incomplete -> `ActivateRootAsync(ProfileSetupPage)` unconditionally): token + onboarding-incomplete routed to **ProfileSetup** before, **Dashboard** through `StartupRoutingRule.Resolve` now. Failed token (`stateReadFailed`) resolves to **StartupError**; `App.InitializeAndRouteAsync` calls `ShowStartupError`, and `AppShell` redirects to `//AppStartupPage`. A genuine first run (no session, incomplete) still resolves to ProfileSetup.
- Chrome inventory: no `ContentPage` style, no `ControlTemplate`, no `ToolbarItem`, no `BackButtonBehavior`, no `PushModalAsync`/`PresentationMode`, no custom `ViewHandler`/`Renderer`; `NavigationPage` appears only as a style (`Resources/Styles/Styles.xaml:1166`); `Shell.Title` appears only as `Shell.TitleColor` (`Styles.xaml:1142`). `NutritionPage.xaml` now contains **0** `PageHeader` controls; the single "Питание" label is the shell tab (`AppShell.xaml:77`).

## Overview-formatter proof 2026-11-10 (in-memory harness, deleted)
- Extracted the Workouts summary into production `PlanSummaryFormatter.Format(WorkoutOverviewResponse, weekIndex)` (`src/MuscleCuties.Infrastructure/Workout/Overview/PlanSummaryFormatter.cs`); `WorkoutViewModel.PlanSummaryText` now delegates to it, so the user-facing text is produced from the real overview response.
- Harness drove a Mon/Wed/Thu request through `WeekPlanRule.BuildWeek` into a `WorkoutOverviewResponse` and the formatter: HEAD mapping -> `"2 training days - 5 full rest days"`; patched mapping -> `"3 training days - 4 full rest days"`. No downstream cap remained.
- Nutrition header evidence: `NutritionPage.xaml` contains **0** `PageHeader` controls. The single visible "Питание" label is the tab title set at `AppShell.xaml.cs:195` (`NutritionTab.Title = UiTextCatalog.Get("Tab_Nutrition")`), tab declared at `AppShell.xaml:70` (`x:Name="NutritionTab"`).

## Production overview-chain proof 2026-11-10 (harness deleted)
- Extracted the pure assembly from `PostgresWorkoutPlanAndSessionStore.LoadOverviewAsync` into nested `PostgresWorkoutPlanAndSessionStore.WorkoutOverviewBuilder.Build` (same file, ~line 200); `LoadOverviewAsync` still authorizes and queries, then delegates to it. No behavior change intended.
- Harness chain, verbatim: request Mon/Wed/Thu; HEAD mapping 2 vs patched generation 3; production builder produced 3 scheduled non-rest days; `PlanSummaryFormatter` -> `"3 training days - 4 full rest days"`; per-day text came from `BuildDetailsText`.

## External harness re-verification 2026-11-11
- Fresh harness at `/tmp/mc-wp10-final2` (deleted, absence confirmed) drove Mon/Wed/Thu through `WeekPlanRule.BuildWeek` -> `PostgresWorkoutPlanAndSessionStore.WorkoutOverviewBuilder.Build` -> `PlanSummaryFormatter.Format`. It printed the serialized `WorkoutOverviewResponse` with MON 2026-11-09, WED 2026-11-11 and THU 2026-11-12 as non-rest entries, each `DetailsText` "Planned Full body session.", and summary "3 training days - 4 full rest days". No source change this pass.
