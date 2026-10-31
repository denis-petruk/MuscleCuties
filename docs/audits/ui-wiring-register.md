# UI wiring register — surfaces with no client caller

For every server route family that has no caller in `src/`, this register states the cheapest wiring
path and the deletion path, so the product decision is a choice rather than an investigation.

Read with [`DEAD_CODE_AUDIT_20261030.md`](DEAD_CODE_AUDIT_20261030.md) §5 and
[`dead-code-manifest.csv`](dead-code-manifest.csv).

Effort scale: **XS** (under a day), **S** (1–2 days), **M** (3–5 days), **L** (1–2 weeks including
tests and localization).

---

## 1. Workout history — cheapest win, already built

| Item | State |
| --- | --- |
| Routes | `GET /api/v1/workouts/history`, `/months`, `/month/{year}/{month}`, `/sessions/{id}`, `/personal-records`; `PUT /sessions/{id}/notes`, `/sets/{id}` |
| Server | `WorkoutArchiveEndpointMappings.cs`, `ServerWorkoutArchiveUseCase`, `PostgresWorkoutArchiveStore` — all implemented |
| Client | `WorkoutHistoryPage.xaml`, `WorkoutHistoryViewModel`, `WorkoutArchiveUseCase`, `CloudWorkoutArchiveStore` — all implemented |
| Missing edge | **One navigation call.** `AppShell.xaml.cs:65` already registers the route; nothing navigates to it |

**Wire (S).** In `WorkoutViewModel` (which already receives `Func<string, Task> navigateAsync` at
`WorkoutViewModel.cs:27` and uses it at line 112):

1. Add `OpenHistoryCommand = new AsyncRelayCommand(() => _navigateAsync(nameof(WorkoutHistoryPage)));`
2. Add a header action in `WorkoutPage.xaml` (the `CollectionView.Header` block starting at line 46)
   bound to `OpenHistoryCommand`, with a `loc:LocalizedText` label that already exists or one new key.
3. No new DI, no new route, no new client store. `WorkoutHistoryViewModel` construction at
   `MauiProgram.cs:598` already injects `IWorkoutArchiveUseCase`.

**Delete (M).** Remove the page, VM, client store, UI models, client archive service, and the list
routes; keep the two `/count` routes and their server stack (live dashboard progress).

**Recommendation:** wire. The feature is complete and the only defect is a missing button. The server
list routes stay; the `ServerWorkoutArchiveUseCase` list methods stay.

---

## 2. Fasting timer (P06)

| Item | State |
| --- | --- |
| Routes | `/api/v1/fasting` group at `HydrationAndFastingEndpointMappings.cs:201` (10 routes: session, start, pause, resume, end, correct, planned end) |
| Server | `FastingSessionUseCase`, `PostgresFastingStore`, `Core/Domain/Fasting/FastingSession` |
| Client | 4 orphan properties in `ProfilePersonalInfoViewModel.cs:341-344`; **no XAML section**; no client API methods; no client store |
| Localization | `Fasting_*` keys exist in Russian and English catalogs but are not used in XAML |

**Wire (L).**

1. Add a "Fasting" section to `ProfilePersonalInfoPage.xaml`, bound to `FastingEligibility`,
   `ActiveFastingSession`, `PlannedFastingEndUtc`, `EnablePlannedFastingEndReminder`.
2. Add commands to `ProfilePersonalInfoViewModel` for start/pause/resume/end and a timer tick; the
   policy requires an eligibility attestation before start (`FastingSession.cs:120`).
3. Add client methods to the store that owns the profile screen (`CloudAccountAndProfileStores.cs`
   family) for the 10 routes; the client has none today.
4. Add `FastingSessionUseCase` to the client DI graph with an HTTP store, or keep it server-side only
   and add the client store.
5. Add tests for eligibility gating, timezone offset bounds (`FastingSession.cs:317`) and planned-end
   reminder behavior.
6. Use the existing `Fasting_*` catalog keys; no new localization work beyond bindings.

**Delete (S).** Remove the 4 properties, the use-case file, the store, the domain type, the 10 routes
and the unused catalog keys.

**Recommendation:** decide by product intent. The server and domain are complete; only the client half
was removed. If the timer is still planned, wiring is mostly UI and client transport, not new logic.

---

## 3. Hydration: presets, intake, reminders

| Item | State |
| --- | --- |
| Routes | `/api/v1/hydration` group at `HydrationAndFastingEndpointMappings.cs:34` (13 routes: presets CRUD/initialize/reorder, intake get/change, reminders get/save) |
| Server | `InitializeWaterPresetsUseCase`, `Get/Create/Update/Delete/ReorderWaterPresetsUseCase`, `Get/ChangeWaterIntakeUseCase`, `Get/SaveWaterReminderConfigurationUseCase`, `PostgresHydrationStore` |
| Client | No API call. 3 orphan reminder properties in `FoodLogViewModel.cs:603-605`. `Core.Domain.Hydration.DefaultWaterPresets` is used by `FoodLogViewModel.cs:609-611` for quick-add values only |
| UI today | Dashboard shows a hydration string from the overview DTO (`DashboardTargetsCard.xaml`); there is no water logging screen |

**Wire (L).**

1. Add water intake controls to the Nutrition tab or the dashboard targets card: preset chips from
   `GetWaterPresetsUseCase`, an intake progress value from `GetWaterIntakeUseCase`, and a +/− action.
2. Add a reminder settings section to `ProfilePersonalInfoPage` or the notifications settings area,
   bound to the 3 existing properties.
3. Add a client hydration store and register it; no client transport exists today.
4. Add tests for preset initialization idempotency (the store contract explicitly says "never merge or
   resurrect defaults") and for reminder save round-trips.

**Delete (M).** Remove the 13 routes, the 9 use cases, `PostgresHydrationStore`, the 3 properties and
the water quick-add path in `FoodLogViewModel` (which currently logs water through the local diary).

**Recommendation:** wire intake only (small, visible value); reminder configuration can wait. Deleting
intake would remove a feature users can already see on the dashboard as a read-only number.

---

## 4. Body circumference measurements

| Item | State |
| --- | --- |
| Routes | `/api/v1/progress/measurements` group at `HydrationAndFastingEndpointMappings.cs:148` (4 routes) |
| Server | `Create/Correct/Delete/ReadBodyMeasurementHistoryUseCase`, `PostgresBodyMeasurementStore`, `HttpContextBodyMeasurementSession` |
| Client | 4 orphan properties in `ProfilePersonalInfoViewModel.cs:334-337`; **no XAML section**; no client transport |
| Domain | `Core/Domain/Progress/BodyMeasurement.cs` and code constants are complete |

**Wire (M).**

1. Add a "Measurements" section to `ProfilePersonalInfoPage.xaml`: site picker, side picker, unit
   toggle, value entry, history list.
2. Bind the 4 existing properties; add commands for add/correct/delete and history load.
3. Add client transport for the 4 routes, with the same unit conversion the domain already defines.
4. Add tests for bounds (`1..500 cm` per the view-model comment) and for unit conversion.

**Delete (S).** Remove the 4 properties, the 4 use cases, the store and the 4 routes.

**Recommendation:** wire. The view-model properties and the domain were left in place, which is strong
evidence the screen was planned and the wiring was never finished.

---

## 5. Workout planning catalog and plan generation

| Item | State |
| --- | --- |
| Routes | `GET /api/v1/workouts/catalog`, `POST /api/v1/workouts/plan` (`WorkoutPlanningEndpointMappings.cs:10-11`) |
| Server | `GetWorkoutPlanningCatalogUseCase`, `GenerateWorkoutPlanUseCase`, PostgreSQL catalog and reference-data sources |
| Client | Generates plans locally through `WorkoutPlanGenerator` and `FourWeekPlanRule`; no call to either route |

**Decision required (L).**

- **Wire server generation:** make `WorkoutPlanGenerator` call `POST /workouts/plan`, remove the local
  duplicate rules, and make the server catalog authoritative. This is a product architecture change,
  not a UI wiring task.
- **Delete routes (S):** remove the two routes and `GenerateWorkoutPlanUseCase`; keep the catalog store
  only if another server path uses it (it does not today).

**Recommendation:** delete unless the roadmap explicitly moves plan generation to the server. Two
implementations of the same rule already exist in this repository; keeping the unused one is how they
diverge.

---

## 6. Profile history, quiz responses and nutrition settings

| Route | Server | Client state | Decision |
| --- | --- | --- | --- |
| `GET /api/v1/profile/history` | `ServerProfileUseCase.LoadHistoryAsync` | Local profile history only | **Wire (S)** if users should see server-side profile versions; otherwise delete |
| `GET /api/v1/profile/quiz-responses`, `PUT /{questionId}` | `ServerProfileUseCase.LoadQuizResponsesAsync`, `CorrectQuizResponseAsync` | No correction UI | **Wire (S)** only if quiz answers are editable after onboarding; otherwise delete |
| `PUT /api/v1/profile/nutrition-settings` | **Duplicate** of `/nutrition-preferences`; both call `UpdateNutritionPreferencesAsync` with the same body | Client uses `/nutrition-preferences` | **Delete (XS)**, no product loss |

The nutrition-settings duplicate at `ApiApplication.cs:515` is the safest deletion in this document:
it is byte-for-byte the same handler as line 508 under a second path.

---

## 7. Account migration claims

| Item | State |
| --- | --- |
| Routes | `POST /api/v1/accounts/migration-claims`, `/redeem` (`ApiApplication.cs:656,671`) |
| Server | `IAccountMigrationClaimUseCase`, PostgreSQL claim store, nonce table |
| Client | No call, no UI, no mention anywhere in `src/` |

**Delete (S)** unless there is an unreleased migration tool. The feature was presumably for moving a
guest account onto an authenticated account; the local flow now uses `IPendingRegistrationService`
(`PendingRegistrationService`, `IAccountSignInStore` cloud path). Keeping two migration mechanisms is
a security-review surface with no user.

If it must be kept, the wiring is: a post-sign-in prompt in `LoginViewModel`, a client method for both
routes, and tests for claim expiry, nonce reuse and cross-account redemption.

---

## 8. Session probe

| Item | State |
| --- | --- |
| Route | `GET /api/v1/session` (`ApiApplication.cs:97,423`) |
| Returns | `{ isAuthenticated, userId, apiVersion }` |
| Client | No call |

**Wire (XS):** call it once during `AppStartupPage` to validate the stored token and show a version
mismatch error. This would also give the client a cheap, explicit auth health check.

**Delete (XS):** remove the route and `SessionResponse`.

**Recommendation:** wire. It is a diagnostics endpoint with a real use during startup, and it is
already covered by the parity test. If it is wired, add a client test for the signed-out response.

---

## 9. Singular and legacy aliases

| Alias | Duplicate of | Called by |
| --- | --- | --- |
| `/api/v1/workout/history` | `/api/v1/workouts/history` | `ServerRouteParityTests` only |
| `/api/v1/workout/injuries` | `/api/v1/workouts/injuries` | tests only |
| `/api/v1/workout/exercises` | `/api/v1/workouts/exercises` | `Registration.Integration.Tests` (`/swap`, `/contraindicated`) |
| `/api/v1/cycle/phases` | `/api/v1/cycle/history/phases` | tests only |

**Delete (XS).** The plural groups are registered first and are the ones the client calls. Update the
two test call sites in the same commit. No UI wiring is possible or desirable; these are accidental
duplicates.

---

## 10. Priority order

| Order | Item | Effort | Why first |
| --- | --- | --- | --- |
| 1 | Fix the DI graph (F13 in the audit) | M | Any wiring below can trip over it; `ProfileHealthSyncPage` is reachable today |
| 2 | Wire the workout history button | S | Complete feature, one missing navigation call |
| 3 | Delete the nutrition-settings duplicate | XS | Zero product risk, removes a divergent contract |
| 4 | Delete singular and legacy aliases | XS | Removes duplicate API surface and test ambiguity |
| 5 | Decide fasting and measurements | M/L each | Both are half-removed features; pick wire or delete and finish |
| 6 | Decide hydration intake vs reminders | L | Largest UI build; split intake from reminders |
| 7 | Profile history and quiz corrections | S each | Only if the product wants server-side profile versioning |
| 8 | Migration claims and session probe | XS/S | Delete claims; wire the probe only if startup needs it |
| 9 | Workout plan generation | L | Architecture decision, not wiring |
