# Decisions injection plan: owner decisions of 2026-11-09/10 → code, merged with MASTER_IMPLEMENTATION_PLAN

Planning gate output (read-only). Worktree `.agent-worktrees/qa-20261109`, branch `product/qa-fixes-20261109`, HEAD
`e9b2a5db`. `git status` shows only `.claude/` as untracked. Nothing was built, tested, run or committed. tests/ was not read.
Every status below comes from reading source at HEAD. None of it is runtime evidence.

Baseline delta vs MASTER §0: the `docs/architecture/github-transition/**` docs are present again, and the F01 interim mapper
edit and its RuntimeText entry are no longer in the tree, so MASTER WP0 items 1–2 are resolved. The owner's 2026-11-10 HealthKit
authorization supersedes MASTER §5 item 3 and turns it into DK3–DK7 below. S-04 is deferred, so MASTER WP6b is dropped and
exit criterion 7 is met by documenting the deferral. `docs/beta-readiness/RELEASE_READINESS.md:5` now says **GO**, which
conflicts with MASTER §6 and with this plan (see §4).

## 1. Decision-by-decision status

| Decision | Status | Evidence (file:line) | Gap / contradiction |
|---|---|---|---|
| **S-03 D4** set-only readiness reduction, no load datum, no schema | **Implemented** (live path); **dead contradicting code** | `CycleReadinessPrescriptionRule.cs:84-86` (sleep <7 h or Lighter/Easier band), `:105-114` (one optional accessory set −1); `PostgresCycleReadinessPrescription.cs:142-166` (only Sets/OptionalSets change); `PlannedPrescriptionSnapshot.cs:26-27` (no load field); `PostgresCycleReadinessReconciler.cs:160-173` (accepts exactly one set reduction) | `ComposedReadinessCycleDoseRule.cs:115-129` cuts load by about 10% and adds +1 RIR. `:136+` lowers load and adds reps or an extra set by Luteal/Menstrual phase. Nothing in src calls it (test references were not inspected). Delete it (DK1) |
| **S-03 D5** cycle estimate alone never changes sets/reps/load | **CONTRADICTED** in the live dose path | `CycleReadinessPrescriptionRule.cs:87-97`: `shouldReduce = … \|\| (phaseFeeling \|\| repeatedFeeling) && !checkInFeelingReady`. A confirmed or "reliable estimated" phase plus the onboarding phase-feeling baseline (`:91-93`), or a 3-cycle history pattern (`:94`, `:127-149`), removes a set **with no current check-in**. Reason codes `:53-54`; mapped at `PostgresWorkoutPlanAndSessionStore.cs:463-464`; reconciler accepts them at `PostgresCycleReadinessReconciler.cs:171-172` | The change only ever reduces, so it is not unsafe, but it is a second, phase-driven dose authority that the ratification forbids. Also check: `WeeklyWorkoutPlanGeneratorRule.cs:378-386` feeds the phase tier into precedence, which looks like trace only. The dormant `ReviewActivePlanPhaseV010UseCase.cs:156,256` / `PhaseAdjustmentRule` proposal path has no references, so keep it unwired. `HealthEnergyAdjustment.cs:35-40` applies a phase factor to **calories**. That is not a training dose, but nobody ratified it (owner question OQ2) |
| **S-05** 7-day window deleted, immediate restore | **Implemented**; remnants remain | `WorkoutInjuryClearanceRule.cs:14` `OptionalCalendarDaysIncludingClearance = 0`; `:48-49` makes optional only before clearance; users `PostgresWorkoutPlanReconciler.cs:616-620,674-688`, `AutomaticInjuryProvenance.cs:154,185` | The comment at `:7-10` still says "pending owner ratification … earlier behaviour can be restored". `InjuryProgressionPhase.ClearedOptionalWindow` (`InjuryProgressionPhaseRule.cs:4,16-18`) can no longer occur. `InjuryRestorationRequiredFromDate` (`AppRepoModels.cs:378`) is written only (reconciler `:619,:685`). Rows materialized while the constant was 7 (before `1a4ca7dd`) may still have `IsOptional=true`, and nobody has shown that a reconcile re-evaluates them |
| **S-04** mid-session pain button deferred | **Consistent** | Guard at `PostgresCheckInAndInjuryStore.cs:274-277` is kept. Pain → swap/stop path per MASTER F03 | Testers need a known-issue note. Drop MASTER WP6b |
| **HK-A1** contextual permission prompt (first Daily Check-In or Profile sync settings, not onboarding) | **Partial** | Check-in modal: `DashboardViewModel.cs:451-455` → `DailyCheckInViewModel.cs:119-136` → `HealthSyncService.cs:115-116` → `AppleHealthDataProvider.cs:34-66` (read-only, empty share set `:66`). No onboarding call site exists; the only matches are dead RuntimeText strings | **No Profile sync settings surface**: no Profile VM uses `IHealthSyncService`, `ProfilePrivacyPage.xaml:59-63` is static copy, and `DisconnectAsync` (`HealthSyncService.cs:68`) has no UI. There is no rationale before the system sheet. Authorization is requested again on every modal open while the cache is empty, ignoring `PromptDismissed`/`ShouldShowPromptAsync` (`HealthSyncService.cs:53-60`). A denial cannot be told apart from "no data" and gets an English message (`AppleHealthDataProvider.cs:27`) |
| **HK-A2** rolling 7-day step trend, clamp −180..+260 | **Partial** | `HealthEnergyAdjustment.cs:7-44` (clamp `:43`); used at `NutritionViewModel.cs:458-494` | (a) The window includes today's partial day and divides by 7 whatever the coverage (`AppleHealthDataProvider.cs:89-90,145`). Missing days count as zero steps, so they **produce a negative calorie adjustment**. (b) Weight defaults to 65 kg and age to 28 when unknown (`NutritionViewModel.cs:466-479`). Workout days are hard-coded to 3 (`:482`). (c) Macros are not rescaled after `TargetCalories += adjustment` (`:487`). (d) "kcal" is English (`:485`). (e) Client-only, so the Today target differs from the Nutrition target. This collides with MASTER F05/WP4. (f) Literal thresholds break SAFETY.md:24. (g) The phase factor (OQ2) |
| **HK-A3** SleepHours auto-fill + "Suggest from Apple Health" chip | **Partial** | Fill: `DailyCheckInViewModel.cs:138-147`; chip: `:150-193`, command `:207-214`, XAML `DailyCheckInContent.xaml:31-33,78-79`; keys EN/RU at `UiTextCatalog.Dashboard.cs:51,88` and `RussianUiText.Dashboard.cs:51,88` | **Overwrites a manual entry** when the modal is reopened: the VM is cached (`DashboardViewModel.cs:205`) and nothing checks `_isSleepHoursAnswered`. **Missing sleep is treated as 0** (`:152`), which suggests sleep quality 1 and energy 2 when only RHR/HRV exist. There is a hidden transport-level substitution at `CloudDailyCheckInAndCycleStores.cs:80-82`. Sleep samples from several sources are summed without merging overlaps (`AppleHealthDataProvider.cs:391-405`), so sleep can be overstated, which suppresses the conservative short-sleep reduction. The RHR/HRV thresholds are unreviewed literals |
| **HK types** Steps, ActiveEnergy, RHR, HRV SDNN, SleepAnalysis | **Implemented** | `AppleHealthDataProvider.cs:43-54,100-124` | Cache schema `StepCountStateVersion = 1` and the comment at `HealthSyncService.cs:10,199` are stale. Old step-only caches are kept |
| **HK data egress** | **FLAG – owner** | `CloudDailyCheckInAndCycleStores.cs:84-97` sends `stepsYesterday`, `steps7dAvg`, `sleep3dAvg` and the HealthKit sleep fallback to `/api/v1/check-ins`. They are persisted (`AppRepoModels.cs:243-245`) and used server-side (`PostgresDashboardOverviewStore.cs:234`, `ReadinessHistoryInputBuilder.cs:10,28`) | First-party only, which SAFETY.md:15 permits, and no external AI is involved. **But server storage is outside the ratified A/A/A scope (SAFETY.md:11)** and must match the privacy policy and App Privacy answers. OQ1 |
| **Config** entitlements / plist / privacy | **Mostly done** | `Entitlements.plist` healthkit `true`; `Info.plist:60-63`; `Resources/PrivacyInfo.xcprivacy:96,109` (Health/Fitness linked); `PlatformCompositionExtensions.cs:103-110` (iOS only, Disabled elsewhere); `MauiProgram.cs:176,203` | `NSHealthUpdateUsageDescription` (Info.plist:62-63 and both InfoPlist.strings) describes reading although the app writes nothing, so remove it. Copy is stale: `Profile_Privacy_HealthBody` "Step count is read-only" (`UiTextCatalog.NutritionProfileAuth.cs:733`), the key/value mismatch at `:590`, and RuntimeText `:84` ("Здоровье Apple"), `:502` ("Здоровые связи"), `:1406`. HealthSyncService messages are English (`:95-149`) |
| **Simulator** | **Missing** | `HKHealthStore.IsHealthDataAvailable` is true on the simulator but the store is empty, so the summary is null, the app shows "not connected" and the English empty-data message | No synthetic-data path. See DK8 |

Owner questions: **OQ1** Keep sending HealthKit aggregates (steps and sleep averages) to the backend? (default for the first TestFlight: no; send only the sleepHours the user confirmed). **OQ2** Keep the cycle-phase calorie factor (0.95/1.05)? (default: neutral 1.0, to be consistent with D5). **OQ3** Repeated-pattern evidence: delete it, or keep it only as a cue to show the check-in (default: delete it from the dose path).

## 2. Work packages (DK = decision-injection)

Conventions match MASTER: one owner per package, branch `product/<id>-<topic>`, ⚑ = integration-authority reservation.
No package needs a schema migration. Tests are described, not run. Simulator retests need task-specific owner authorization.

**DK0 Docs & handoff (integration authority).** Make the §4 amendments, revert the RELEASE_READINESS verdict, and record
DK package IDs, base SHA and reservations in STATUS.md/handoff. Acceptance: no doc claims GO while §3 exit criteria are open.

**DK1 Cycle dose = symptoms/readiness only (Core + Infrastructure.PostgreSql) ⚑ persisted vocabulary**
- `CycleReadinessPrescriptionRule.cs`: `shouldReduce = dailyReduction || RoutineSymptoms`. Keep `EvidencedPhase` only as
  provenance. Remove the `phaseFeeling`/`repeatedFeeling` branches. Bump `PolicyVersion` to `MC-CYCLE-READINESS-0.3`.
- `PlanningReleaseApproval.cs`: approval record for 0.3 citing amendments A-4/A-5 (2026-11-09).
- `PostgresCycleReadinessPrescription.cs:95-114,149-150`: stop loading baseline and pattern evidence that no rule uses.
- `PostgresCycleReadinessReconciler.cs:171-172`: new adjustments accept only `DailyReadinessAdjustment/Current`.
  Keep `PostgresWorkoutPlanAndSessionStore.cs:463-464` mapping and the `WorkoutDoseAdjustmentReason` enum members so that
  historical sessions still deserialize. Removing contract values counts as a DTO change ⚑, so do not do it.
- Delete `ComposedReadinessCycleDoseRule.cs` and its tests. The test removal belongs to the test-owning package.
- Characterize `WeeklyWorkoutPlanGeneratorRule.cs:378-386` as trace-only, and fail the test if it starts changing sessions.
- Acceptance:
  - Phase (confirmed or estimated) + low onboarding feeling + no check-in → `CycleReadinessNormalDose`.
  - 3-cycle low-readiness pattern + no check-in → no change.
  - Short sleep, Lighter/Easier band or routine symptoms → exactly one optional accessory set −1, once per session.
  - Safety concern → withheld.
  - Reps, load and RIR never change.
- Tests: Core decision table covering every branch, plus "unknown ≠ false" (null sleep is not short sleep). Disposable-PostgreSQL
  reconcile: an old-reason row stays readable, and a new phase-only day stays at its scheduled dose.
- Sim retest: synthetic user with a confirmed phase and a low phase-feeling quiz. Without a check-in the dose matches the plan.
  After a check-in with 5.5 h sleep, one optional set is gone. Check RU and EN reason copy.

**DK2 S-05 remnant cleanup (Core + Infrastructure.PostgreSql)**
- Update the `WorkoutInjuryClearanceRule.cs:7-10` comment to "ratified 2026-11-09". Collapse the `ClearedOptionalWindow` enum
  value (it has no consumers outside the rule) or document it as unreachable. Keep the column, because dropping it is a schema contract change.
- Characterize legacy rows. Seed a disposable DB with a restored original where `IsOptional=true` and
  `RequiredFromDate=clearance+7`, run the reconciler on a later date, and expect required restoration. If it stays optional,
  add idempotent re-evaluation in the reconciler restore path. Do not touch Beta data.
- Tests: clearance today restores as required, future clearance blocks, unknown clearance blocks, and an originally optional
  exercise stays optional.
- Sim retest: log a knee injury, clear it today, and see the original exercise back as required in today's preview and session.

**DK3 HealthKit permission UX + Profile sync settings (UI)**
- New `ProfileHealthSyncViewModel` plus a section on the Profile/Privacy page (shared `CardPrimary` and `ButtonPrimary`/`ButtonSecondary`)
  with status (`GetStatusAsync`), Connect/Refresh (`SyncAsync`) and Disconnect (`DisconnectAsync`).
- Check-in: a one-time rationale card, "Connect Apple Health" or "Not now", shown before the system sheet. "Not now" sets
  `DismissPromptAsync`. Request authorization only through `ShouldShowPromptAsync` or an explicit tap.
- Localized denial and empty states: "Allow access in Health > Sharing > Apps". iOS does not reveal a read denial, so
  word it as "no data or access off".
- Acceptance:
  - No HealthKit sheet before the first check-in open or a Profile tap.
  - No repeat request after "Not now".
  - Disconnect clears the per-account cache.
  - EN/RU parity.
- Tests: VM units with fake `IHealthSyncService` for prompt gating, the dismiss path and disconnect. The XAML binding guard covers the new keys.

**DK4 Check-in pre-fill correctness (UI + Infrastructure client)**
- `DailyCheckInViewModel.cs:138-147`: fill only when `!_isSleepHoursAnswered`, and mark the value as auto-filled so it can be edited.
- `:150-193`: skip the sleep-based rating when `LastNightSleepHours` is null, and move the thresholds to named constants.
- `CloudDailyCheckInAndCycleStores.cs:80-82`: remove the hidden sleep substitution. The VM is the single pre-fill owner.
- `AppleHealthDataProvider.cs:379-410`: merge overlapping asleep intervals before summing.
- Acceptance: a manual 6.0 h survives a modal reopen. RHR-only data suggests no sleep rating. The value sent equals the value shown.
- Tests: VM unit for reopen/no-overwrite and null sleep; store unit showing the sent body has no HealthKit substitution;
  interval-merge unit using pure helpers extracted from the provider.

**DK5 Calorie adjustment correctness (Core rule + UI)**
- Window: 7 completed days (today−7 … today−1). Average over the days that have samples. Require ≥4 days, or no adjustment.
- No adjustment when weight or age is unknown, so remove the 65 kg / 28-year defaults. Take workout days per week from the active plan.
- Apply OQ2: the phase factor defaults to 1.0. Use named constants. Localize "kcal".
- Decide with the WP4 owner whether Today shows the adjusted target, so Today and Nutrition agree. Keep the macro split
  consistent with 4/4/9: rescale carbs, or show the adjustment as a separate line.
- Optionally move `HealthEnergyAdjustment` to Core/Domain. The namespace is already `Core.Services.Health`, and
  the move changes no project graph.
- Acceptance: 2 days of data → 0 kcal. ±750-step dead band. Clamp at −180 and +260. Unknown weight → 0. Today total equals Nutrition total.
- Tests: Core boundary table covering coverage, dead band, clamp and rounding. NutritionViewModel unit for unknown profile.

**DK6 Health data egress (OWNER-GATED on OQ1)**
- Default: send only the `sleepHours` the user confirmed, and send `stepsYesterday/steps7dAvg/sleep3dAvg = null`. These
  fields are optional and already exist, so no DTO change is needed. The server then computes its own averages from stored check-ins.
- Alternative: ratify server storage, and update the privacy policy, App Privacy and `Profile_Privacy_*` copy to match.
- Tests: client store unit asserting the body fields.

**DK7 Config, cache and localization (UI)**
- Remove `NSHealthUpdateUsageDescription` from Info.plist and both InfoPlist.strings, since the share set is empty.
- Bump the health cache schema to 2 and rename the constant.
- Localize HealthSyncService and provider messages through codes and keys, not RuntimeText (MASTER §3 freeze).
- Fix `Profile_Privacy_HealthBody` (list the 5 types, read-only, purpose), the `:590` key/value, and RuntimeText `:84,:502,:1406`.
- Tests: catalog parity, and a plist check that the share description is absent while HealthKit stays read-only.

**DK8 Simulator HealthKit with synthetic data only (UI, Debug only)**
- Add `SyntheticHealthDataProvider` under `#if DEBUG`, registered in place of `AppleHealthDataProvider` only when
  `MC_SYNTHETIC_HEALTH=1` is forwarded by `scripts/run-ios-simulator.sh`, following the same pattern as the FDC key.
- Fixtures: 9,400 average steps, yesterday 8,100, 6.2 h last night, RHR 58, HRV 55, 420 kcal active.
- Variants: `MC_SYNTHETIC_HEALTH=sparse` (2 days of data) and `=empty`.
- A Release-build guard test asserts the type is absent from Release builds.
- Real-sheet path: on the simulator open Health → Browse → Sleep/Steps/Resting HR → Add Data with invented values only, then
  open the check-in to get the system sheet. Re-show the sheet by deleting and reinstalling the app or erasing the simulator.
  Use `simctl privacy … reset` only if the installed Xcode lists a health service; verify that first.
- Never use real Health exports, device backups or personal data (SAFETY.md:17).
- Retest matrix, RU/EN, light/dark:
  1. Fresh install: no sheet on onboarding.
  2. First check-in: rationale, then sheet, then sleep pre-filled with badge and chip shown.
  3. Change sleep, close and reopen: the manual value is kept.
  4. Chip: energy and sleep-quality sliders fill, Save works.
  5. Nutrition: +/− kcal line appears and matches Today.
  6. `sparse`: no calorie adjustment.
  7. `empty`/denied: localized "no data or access off", the check-in still works.
  8. Profile: Disconnect, then the badge and adjustment disappear.

## 3. Merged order with MASTER work packages

1. **DK0 + MASTER WP0 remainder** (handoff, reservations). Ask the owner OQ1–OQ3 at the same time.
2. **Parallel, disjoint files:** DK1 (Core cycle rule, PostgresCycleReadiness*), DK2 (WorkoutInjuryClearanceRule,
   PostgresWorkoutPlanReconciler), MASTER WP1 (F01 ⚑), WP3 (F04), WP6a (F08). DK1 goes first because it is the only
   live-path contradiction of a ratified decision. DK1 and DK2 both touch Core/Planning but no shared file. Run both
   PostgreSQL suites on one disposable DB in sequence.
3. **MASTER WP2 + WP4** (⚑ reservations), then **DK5**. DK5 depends on WP4's single nutrition-consumed source and its Today/Nutrition target decision.
4. **UI batch, one owner:** DK4 → DK3 → DK7 → DK8. Conflicts:
   - DK4 and MASTER WP8 (check-in Save hint) both edit `DailyCheckInViewModel`/`DailyCheckInContent.xaml`, so serialize them.
   - DK7 and MASTER WP7 both edit the localization catalogs and RuntimeText, so serialize them and keep the RuntimeText freeze.
   - DK3 and WP8 both touch the Profile pages, which must follow DESIGN.md.
5. **DK6** as soon as the owner answers OQ1. It is a small client-only change and can go into step 2 if the answer is "no".
6. MASTER WP5 (C01; also covers D5 copy: phase shown as context, never as a dose reason) → WP7 → WP8/WP9/WP10.

Shared contract/DTO/schema flags for the integration authority:
- DK1: persisted `DoseAdjustmentReasonCode`/`PolicyVersion` strings and the `WorkoutDoseAdjustmentReason` enum. Additive only, nothing removed.
- DK6: no DTO change, but it changes which data is collected (privacy disclosure).
- DK2: keep `InjuryRestorationRequiredFromDate`, since dropping it needs an expand/contract migration and nothing needs that now.
- No migration number is needed.

**Internal TestFlight exit criteria, added to MASTER §6:**
- DK1 merged with tests.
- DK4 merged, so there is no overwrite and no hidden substitution.
- DK3 Profile settings shipped, or the "Profile sync settings" half of decision A1 explicitly deferred by the owner.
- DK5(a)(b) fixed, so missing data never cuts calories.
- OQ1 answered, with privacy copy that matches.
- DK8 simulator evidence attached.
- S-04 deferral listed in tester notes.

## 4. Documents to amend

- `docs/release-audit/2026-11-05/WORKOUT_POLICY.md`: the decision 4/5 body text still contains the load step, +2 reps and the extra set.
  Two "Implementation acceptance" rows contradict A-4/A-5: "Poor sleep… one automatic session load reduction" and
  "Cycle adjustment… adjusts reps and load once". Change them to "one optional set removed, load unchanged" and
  "phase alone → as scheduled; symptoms/readiness → decision 4". Mark the pain row as S-04 deferred.
- `docs/PRODUCT_WORKFLOWS.md`:
  - `:10` "cycle-related conditions" becomes "symptoms reported in cycle context".
  - `:43-47` gains "no phase-only or repeated-pattern dose path".
  - The HealthKit section `:49-54` adds: the Profile connect/refresh/disconnect UI, the rationale before the sheet,
    no auto-fill over manual entry, no adjustment when coverage is under 4 days or weight/age is unknown, what is sent to the
    backend (per OQ1), the phase factor (per OQ2), and that simulator QA uses synthetic data only.
- `docs/beta-readiness/RELEASE_READINESS.md`:
  - Change `:5` to **"NO-GO for internal TestFlight: owner decisions ratified; implementation open"**. Open items: P1s F01, F04,
    F05, F06, C01; P2-high F08; DK1 contradiction; HealthKit gaps DK3–DK5; OQ1.
  - Downgrade G03/G07/G12 PASS to "PARTIAL / not re-verified on the candidate".
  - `:63` "OD-4 (HealthKit off)" and `:66` "HealthKit capability can be removed" are stale; replace them with the 2026-11-10 authorization.
  - Do not state SAFE TO PROMOTE without target checks and owner approval (CLAUDE.md states).
- `docs/release-audit/2026-11-09/OWNER_DECISIONS_AND_QA_GAP_CLOSURES.md`:
  - §4 "SAFE TO PROMOTE" becomes "BLOCKED pending DK1/DK3–DK5 and MASTER P1s".
  - Decision 2 gains a note that code still has phase-driven reduction (DK1).
  - Decision 4 gains a note that the Profile sync settings surface is not built yet.
- `docs/release-audit/2026-11-09/HEALTHKIT_AND_TESTFLIGHT_IMPLEMENTATION_PLAN.md`: mark WP-HK4/HK5 "partial", link DK3–DK8.
- `docs/SAFETY.md:11`: link the authorized scope (5 read types, on-device use, backend fields per OQ1).
- Root `CLAUDE.md` "Health synchronization stays dormant…" belongs to the integration authority. Add a pointer to the 2026-11-10
  authorization record, not a relaxation of the rule.
- Code comments: `WorkoutInjuryClearanceRule.cs:7-10`, `HealthSyncService.cs:199`, `MuscleCuties.UI.csproj:27-28`, and `STATUS.md`/handoff with DK IDs.
