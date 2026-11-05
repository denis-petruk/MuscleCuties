# RM00 — Candidate pin, ownership and reservations

Prepared 2026-11-05 by the integration coordinator. Scoped control record for the
[remediation plan](../UI_UX_BACKEND_REMEDIATION_PLAN_2026-11-04.md). No `CONTROL.md` exists under
`docs/architecture/github-transition/execution/`, so this is the scoped handoff the plan allows when
that record is absent. It creates no second integration authority and grants no release authority.

## Current candidate

| Field | Value |
| --- | --- |
| Rider checkout | `/Users/denispetruk/RiderProjects/MuscleCuties`, branch `docs/github-transition-governance-20261104` |
| Recorded planning baseline | `acdb131140dec58c4625132682982b7baba4a4c7` |
| Planning package commit | `a2d55adc` — plan, both prompts, audit addendum and pending-work pointers, committed so worktrees can read them (was untracked) |
| Source identity of the running rebuild | `acdb1311` tree; the two commits above it change documentation only |
| Running rebuild | Rider debug session on simulator `iPhone 17 Pro Max` (iOS 26.3), `MuscleCuties.UI.app` version `1.0` build `2`, installed 2026-11-04 23:13 |
| Resolved framework in that binary | `Microsoft.Maui.Controls 10.0.60` (from `obj/project.assets.json`) |
| Untouched by this record | Owner's running debug session; `docs/release-audit/` remains untracked and unmodified |

The screenshots and the native stack supplied by the owner cannot be attributed to this binary with
certainty; they remain correlations to this candidate, as the plan states.

## Exclusive ownership (this session)

| Area | Owner | Files |
| --- | --- | --- |
| UI presentation, modal/header lifetime, input layout | A.UI (coordinator, sequential — no A.UI delegate was available) | `Pages/Profile/PhotoJourneyVaultPage.xaml`, `Pages/Workout/WorkoutHistoryPage.xaml`, `Pages/Nutrition/FoodLogPage.xaml`, `Pages/Workout/StrengthPlanGenerationPage.xaml.cs`, `Services/Api/ApiFailurePresenter.cs` |
| Planning failure contracts and persistence seam | A.API (coordinator, sequential) | `Infrastructure/ApiClient/PlanningRequestFailedException.cs`, `Infrastructure/Cloud/CloudStrengthPlanProposalStoreV020.cs`, `ViewModels/Workout/StrengthPlanGenerationViewModel.cs` |
| Toolchain / package graph | Integration coordinator | `src/MuscleCuties.UI/MuscleCuties.UI.csproj` (`MauiVersion`) |
| Shared styles | Integration coordinator | `Resources/Styles/Styles.xaml` (dead-style removal only) |
| Canonical status | Integration coordinator | `STATUS.md`, this record |
| Platform B, unchanged | B — Platform/schema/CI | schema lifecycle, CI, deployment, secrets |

Seqential ownership means one package at a time; no file above was edited by two lanes concurrently.

## Package state

| Package | State | Evidence |
| --- | --- | --- |
| RM00 | Done | This record; planning package committed as `a2d55adc` |
| CR01 | Partial — framework and lifetime measures landed, native reproduction outstanding | `MauiVersion` 10.0.60 → 10.0.90 (upstream 10.0.90 `LayoutButton` contains the `platformButton is null` guard that 10.0.60 lacks, verified from the tagged source); generation page drops a deferred load that loses the appearance race. The rebuilt binary installed and stayed running on a spare `iPhone 17` simulator (staging endpoint, no account used, no data written); the crashing transition itself was not reproduced |
| PG01 | Partial — typed failure boundary landed, native flow unverified | `PlanningRequestFailedException` preserves status, error code and request reference; `ApiFailurePresenter` maps offline/timeout/auth/rate-limit/server to their own instruction; both generation stages render one message with the server reference |
| RM02 (PV01) | Presentation landed | Hidden photo content no longer paints a second rectangular fill inside the rounded card |
| RM02 (AH01) | Presentation landed | Archive header fills the shared width contract instead of centring to its content |
| RM04 (NM02) | Partial — layout landed, draft precision outstanding | Amount column is proportional; sole Save fills the footer as `ButtonPrimary` |
| RM03 (NM01) | Not started | Meal-editor scroll owner and disclosure state unchanged |
| RM05 (NM03) | Not started | Selected-date breakdown parity and truthful coverage unchanged |
| RM06/RM07/RM08 | Not started | Audit carry-over, feature disposition, combined review |

## Known pre-existing failure

`UiStyleGateTests.Shared_resource_and_style_keys_have_exactly_one_definition` failed on the unmodified
Rider checkout before any implementation edit: two shared styles had no consumer. Both were removed
here, which restores the gate; the register's "local automated gates are green" claim was stale.

## Implementation pass — 2026-11-05 (reservations and inventory reconciliation)

The independent review's C8 finding (four runtime/test paths missing from the declared inventory) is
reconciled here for the implementation pass. The candidate is base `9a1a357a` plus code pin
`a6f228f2951e96128fe71884b72986d38bb4ef601c527362dd2cbc9f3c2479b0` over 25 modified and 7 new
`src`/`tests` files; nothing is committed.

| Area | Owner | Files |
| --- | --- | --- |
| Generation presentation, activation, injury alert, appearance fence | A.UI (coordinator) | `ViewModels/Workout/StrengthPlanGenerationViewModel.cs`, `Pages/Workout/StrengthPlanGenerationPage.xaml` + `.xaml.cs`, `Pages/PageLoadExtensions.cs`, `Controls/Shared/PageLoadingOverlay.xaml` + `.cs` |
| Planning failure classification and reference guard | A.API (coordinator) | `Infrastructure/Cloud/CloudStrengthPlanProposalStoreV020.cs`, `Infrastructure/ApiClient/RequestReference.cs` (new), `Services/Api/ApiFailurePresenter.cs` |
| Activation/appearance rules | Coordinator | `Core/Domain/Workout/Planning/WorkoutPlanningFlowRule.cs`, `UseCases/Features/Workout/Planning/WorkoutPlanningFlow.cs` |
| Add meal entry | A.UI | `Controls/Nutrition/MealEntryModal.xaml.cs`, `Pages/Nutrition/NutritionPage.xaml` + `.xaml.cs` |
| Grouped editor precision and save | A.UI + A.API review | `UseCases/Features/Nutrition/DailyLog/IngredientPortionDraft.cs` (new), `UseCases/Features/Nutrition/DailyLog/GroupedMealEditProjection.cs` (new), `Models/UI/Nutrition/HistoryIngredientEditor.cs`, `ViewModels/Nutrition/FoodLogViewModel.cs`, `Pages/Nutrition/FoodLogPage.xaml` |
| Archive width | A.UI | `Pages/Workout/WorkoutHistoryPage.xaml` |
| Localization (EN/RU) | A.UI | `Services/Localization/UiTextCatalog*.cs`, `RussianUiText*.cs` (WorkoutGeneration + NutritionProfileAuth) |
| Composition root (injury alert only) | Integration coordinator | `MauiProgram.cs` — one added constructor argument for the native injury confirmation; no other registration changed |
| Regression tests | Coordinator | `tests/MuscleCuties.Core.Tests/{WorkoutPlanningActivationRuleTests,IngredientPortionDraftTests,RequestReferenceTests}.cs` (new) |
| Unchanged | — | `MuscleCuties.UI.csproj` / `MauiVersion` (still 10.0.90), `Styles.xaml`, shared DTOs, schema, API routes |

No file above was edited by two lanes concurrently. RM05/RM05b reservations are released, not held:
no shared breakdown or overview-contract file was touched, so the next session may reserve them.

## Next safe action

Reproduce the CR01 transition on the 10.0.90 candidate with synthetic proposal responses (preview or
failure → Edit details → edit → retry), then close RM03/RM05. Native scenario capture, device passes
and any publication stay behind their existing approvals.

Execution continues through the [mission board](MISSION.md), which carries the package states, the
exact owner actions still blocking RM05b and RM08, and the session protocol. This record stays the
authority for the candidate pin and file reservations.

## Continuation 2026-11-05

The second pass re-used the same single-editor reservations on this worktree; no file was edited by
two lanes concurrently. Candidate pin `4582e63fea80062dce59bf9bdc3520e445feb4c2505a78b64e37ef9accd59aa2`
over 45 modified + 11 new `src`/`tests` files. New scope: RM03 compact context, RM06 F01/F02/F03/S01,
RM07 heading semantics, RM05b additive coverage contract, and source verification of PV01. RM05's
presenter migration remains unreserved and unstarted. See the
[handoff](RM08-CANDIDATE-HANDOFF.md#9-continuation-2026-11-05).
