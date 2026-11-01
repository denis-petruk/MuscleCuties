# Handoff — Cycle/readiness gate and safety disclosure

**Date:** 2026-11-01, re-audited after the changes landed in the working tree.
**Audience:** the next agent, or the owner finishing the remaining items.
**Path prefix:** all paths are relative to the repository root.

> **Read this first.** The code work this document originally planned is **already applied** in the
> working tree: the approval record is active, and the disclosure is present in both catalogs and the
> privacy page. `STATUS.md` now records the provisional activation, and the unreferenced phase safety
> key has been removed. The staging API artifact has since been repackaged and deployed; owner UI verification and the separately authorized production release remain.
> The reconciliation worker is deployed with a real Lambda runtime handler and its EventBridge rule is
> enabled; staging's schema is current, all staging accounts have been purged, and the DLQ is empty.
> See `STATUS.md`. Every string quoted below is **CURRENT** (applied and verified with `grep -F`); this
> document contains no proposed replacement copy and no build, test or publish step.

---

## 1. Verified state (2026-11-01)

| Fact | Evidence |
|---|---|
| Gate is **active** | `PlanningReleaseApproval.cs:25` initializes `CycleReadinessPrescription` with `ProvisionalApprovalReference = "MC-PLANNING-REVIEW-TEST"` (reference declared at `:15`, date at `:17–18`) |
| Approval covers the required sections | `TrainingPrescriptions` + `AutomatedProgression`; versions come from `TodayCheckInScoreRule.RulesetVersion` and `CycleReadinessPrescriptionRule.PolicyVersion` |
| Disclosure applied — privacy page | `ProfilePrivacyPage.xaml:69–81` (elements open at 71, 74, 77), `ProfilePrivacyPage.xaml.cs:41–43` (EN), `59–61` (RU) |
| Disclosure applied — catalogs | `src/MuscleCuties.UI/Services/Localization/`: `UiTextCatalog.CycleWorkout.cs` / `RussianUiText.CycleWorkout.cs` lines 10, 36, 130; `UiTextCatalog.WorkoutPhaseAdjustment.cs` / `RussianUiText.WorkoutPhaseAdjustment.cs` line 38 |
| Phase stop-reason key was **removed** | no consumer remained; both catalog entries were deleted on 2026-11-01 |
| Purge covers injury rows | `PostgresAccountPurgeStore.cs:28` (inventory), `:160` (delete), `:381` (residual check) |
| No export surface | `grep -RIniE` outside `docs/` returns nothing |
| Artifact is **stale** | zip dated Oct 31 21:06; `PlanningReleaseApproval.cs` Nov 1 00:39, catalog Nov 1 00:40 |
| Artifact facts | 8,666,753 bytes, sha256 `de6b853ca1f65c3666c8816826a466e782233b29ca843191b82933a930487745`, 35 files, `unzip -t` clean, manifest `source_sha` `9bfc4c36…`, `worktree_dirty` `true` |
| `STATUS.md` records the provisional activation | leading paragraph names `MC-PLANNING-REVIEW-TEST` and says it is not a clinical sign-off |
| No commit for these changes | worktree dirty on `9bfc4c36` |

Rows marked stale/pending are the actual remaining work. Everything else is already true and only needs
re-verification.

---

## 2. Locked owner decisions

| Decision | Ruling |
|---|---|
| Gate | **Active**, using the fixture approval values, labelled provisional |
| Compensating control | The app states it is not clinically approved, is not a health consultant, and that a professional should be consulted — while still applying strength workouts |
| English wording | As shipped in section 4 |
| Russian wording | As shipped, owner-approved on 2026-11-01; no native or clinical review is claimed |
| Clinician "avoid" | Blocks the movement until the injury is marked `Cleared` |
| Worsening pain | Resolves through the same deterministic substitution path as contraindications |
| New injury answers | Purged with the account, excluded from any export |
| Android sources | Left inert |
| Schema | Applied in place, no migration; staging has the injury columns, metadata row and declared function owners, with account data purged on owner instruction |

**Build policy:** all building, testing and publishing is the owner's action inside Rider. This document
contains no command that compiles, tests or publishes anything; its own checks are `grep`, `sed`, `unzip`
and SQL.

---

## 3. Gate activation — applied, verify only

**File:** `src/MuscleCuties.Core/Domain/Workout/Planning/PlanningReleaseApproval.cs`

Current shape (applied):

- `ProvisionalApprovalReference = "MC-PLANNING-REVIEW-TEST"`
- `ProvisionalApprovedAtUtc = new(2026, 10, 31, 0, 0, 0, TimeSpan.Zero)`
- `CycleReadinessPrescription` initialized with
  `TodayCheckInScoreRule.RulesetVersion`, the reference, the date,
  `{ TrainingPrescriptions, AutomatedProgression }`, and `CycleReadinessPrescriptionRule.PolicyVersion`.

Why these values satisfy the rule: `CycleReadinessPrescriptionRule.IsApproved` requires a non-blank
reference, a non-default UTC date, a ruleset equal to `TodayCheckInScoreRule.RulesetVersion` (which is
`MC-PLANNING-0.2`), a reviewed policy version equal to `MC-CYCLE-READINESS-0.2`, and both covered sections.
`Directory.Build.props:4` enables implicit usings, so `HashSet<T>` and `DateTimeOffset` need no explicit
`using`.

**Verification:** V1–V3.

**Do not** describe this record as reviewed. It is a provisional activation whose justification is the
shipped disclosure, not a clinical sign-off.

---

## 4. Disclosure — applied, verify only

All strings in this section exist in the working tree today. They were checked with `grep -F` against the
named file and line. None of them is a proposal.

### 4.1 Key matrix

| Key | EN file:line | RU file:line | Render site |
|---|---|---|---|
| `Workout_Session_AdjustmentSafety` | `UiTextCatalog.CycleWorkout.cs:10` | `RussianUiText.CycleWorkout.cs:10` | `WorkoutSessionViewModel.cs:371` (`UiTextCatalog.Get`) |
| `Injury_Log_Safety_StopAndSeekCare` | `UiTextCatalog.CycleWorkout.cs:36` | `RussianUiText.CycleWorkout.cs:36` | `InjuryLogContent.xaml:185` (`loc:LocalizedText`) |
| `Cycle_Page_MedicalDisclaimer` | `UiTextCatalog.CycleWorkout.cs:130` | `RussianUiText.CycleWorkout.cs:130` | `CyclePage.xaml:74` (`loc:LocalizedText`) |

Both catalogs live in `src/MuscleCuties.UI/Services/Localization/`; there is no `Resources/Localization`
directory in this repository, so a path under `Resources/` is wrong. The repository contains exactly **one**
`ApplyLanguage` body — `ProfilePrivacyPage.xaml.cs:25`, with an English branch and a Russian branch — and it
assigns only the three privacy-card labels. The four catalog keys are resolved through the localization
catalogs and XAML/view-model lookups, **not** through any `ApplyLanguage` body;
`grep -RIn "ApplyLanguage" src --include=*.cs | grep -v ProfilePrivacyPage` returns nothing. Any instruction
that adds those keys to a code-behind method would be wrong for this codebase.

### 4.2 Privacy page (the only `ApplyLanguage` surface here)

**XAML:** `src/MuscleCuties.UI/Pages/Profile/ProfilePrivacyPage.xaml`

The grid declares exactly five rows (`RowDefinitions="Auto,Auto,Auto,Auto,Auto"`, line 18) and all five are
already occupied: row 0 the header (line 20), row 1 the summary card (26), row 2 the services card (36),
row 3 the choices card (54), row 4 the disclosure card (**69**). The disclosure card already exists and spans
lines **69–81**. There is no free row and no card to insert.

| Element | Element opens | `Text` attribute |
|---|---|---|
| `DisclaimerTitle` | 71 | 72 |
| `DisclaimerBody` | 74 | 75 |
| `DisclaimerPlanBody` | 77 | 78 |

**No string in this section is labelled NEW.** The card is applied, so this document proposes no replacement
copy and there is no NEW string that must appear nowhere: `grep -nE "\bNEW\b"` over this file returns no hit.
The three English `Text` attributes in the XAML are identical to the English code-behind assignments, and all
six assignments in both branches plus the three XAML attributes were checked with `grep -F` (V15). If a future
revision does add proposed copy, label it **NEW** and prove its absence with `grep -F` over `src/` and `tests/`
before claiming it.

**Code-behind:** `src/MuscleCuties.UI/Pages/Profile/ProfilePrivacyPage.xaml.cs`
English branch, lines 41–43:

```csharp
DisclaimerTitle.Text = "Not clinically approved";
DisclaimerBody.Text = "MuscleCuties is not a health consultant, a medical service or a substitute for professional care. Its guidance has not been reviewed or approved by a clinician. Before relying on this app, talk to a qualified professional about your training, your cycle and any injury or pain you have.";
DisclaimerPlanBody.Text = "Workouts and plans are still applied. The app will keep giving you strength training and a workout plan, adjusted to what you tell it, including when you report an injury.";
```

Russian branch, lines 59–61:

```csharp
DisclaimerTitle.Text = "Не одобрено клинически";
DisclaimerBody.Text = "MuscleCuties не является медицинским консультантом, медицинской услугой или заменой профессиональной помощи. Рекомендации приложения не проверены и не одобрены врачом. Перед тем как полагаться на приложение, обсудите тренировки, свой цикл и любые травмы или боли с квалифицированным специалистом.";
DisclaimerPlanBody.Text = "Тренировки и планы при этом применяются. Приложение продолжит давать силовую нагрузку и план тренировок с учётом того, что вы сообщили, в том числе при травме.";
```

### 4.3 Cycle page disclaimer

`UiTextCatalog.CycleWorkout.cs:130` (EN, applied):

```
Not clinically approved. MuscleCuties is not a health consultant, a medical service or a substitute for professional care. Its guidance has not been reviewed or approved by a clinician. Before relying on this app, talk to a qualified professional about your training, your cycle and any injury or pain you have. Workouts and plans are still applied, adjusted to what you tell it.
```

`RussianUiText.CycleWorkout.cs:130` (RU, applied, owner-approved 2026-11-01):

```
Не одобрено клинически. MuscleCuties не является медицинским консультантом, медицинской услугой или заменой профессиональной помощи. Рекомендации приложения не проверены и не одобрены врачом. Перед тем как полагаться на приложение, обсудите тренировки, свой цикл и любые травмы или боли с квалифицированным специалистом. Тренировки и планы при этом применяются с учётом того, что вы сообщили.
```

### 4.4 Injury safety label

`UiTextCatalog.CycleWorkout.cs:36` (EN, applied):

```
Stop if pain worsens and seek care from a qualified professional. This is not medical advice, and the app is not a health consultant; your strength plan still applies with adjusted movements.
```

`RussianUiText.CycleWorkout.cs:36` (RU, applied):

```
Остановитесь, если боль усиливается, и обратитесь к квалифицированному специалисту. Это не медицинская рекомендация, и приложение не является медицинским консультантом; силовой план при этом применяется с учётом изменённых движений.
```

### 4.5 Session stop reason

`UiTextCatalog.CycleWorkout.cs:10` (EN, applied):

```
Review today's symptoms before starting this workout. This is not medical advice and the app is not a health consultant; ask a qualified professional about pain that is worsening or not settling.
```

`RussianUiText.CycleWorkout.cs:10` (RU, applied):

```
Перед началом тренировки проверьте сегодняшние симптомы. Это не медицинская рекомендация, и приложение не является медицинским консультантом; при усиливающейся или не проходящей боли обратитесь к квалифицированному специалисту.
```

This is the string a user actually sees for `WorkoutDoseAdjustmentReason.SafetyReview`, because
`WorkoutSessionViewModel.cs:371` maps that reason to this key.

### 4.6 Phase stop reason — removed

The retired phase-adjustment review surface has no page, route or view model in the current tree, and a
repository-wide search found no consumer of `Workout_PhaseAdjustment_CurrentSafetyReviewRequired`. Both
catalog entries were deleted on 2026-11-01.

The four rendered disclosure surfaces are unchanged: the privacy card, the cycle page disclaimer, the
injury label, and the session stop reason. Do not count a phase-adjustment surface among them.

---

## 5. Export and purge

### 5.1 No export surface exists

Verified with:

```bash
grep -RIniE "account.{0,20}export|export.{0,20}account|data export|export data|/export" \
  --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git --exclude-dir=artifacts .
```

Outside `docs/` this returns nothing. The only pre-existing documentation mention is the App Store
"export-compliance" line in `docs/SUBMISSION_READINESS.md:34`. Do not build an export feature, and do not
claim the two fields are "excluded from export" as if an export existed — the accurate statement is that no
export surface exists, so there is nothing to exclude them from.

### 5.2 Purge already removes the fields

`src/MuscleCuties.Infrastructure.PostgreSql/Account/Deletion/PostgresAccountPurgeStore.cs`:

- line 28 — `typeof(WorkoutInjuryLogRecord)` in `PurgedTenantRecordTypes` (declared line 15), so the
  inventory assertion at line 353 fails if injury rows are dropped from the purge.
- line 160 — `dbContext.WorkoutInjuryLogs ... ExecuteDeleteAsync(cancellationToken)` inside the purge
  transaction, so both columns are removed with the row.
- line 381 — `WorkoutInjuryLogs.AnyAsync(...)` in the residual check, so a surviving row fails the purge.

**Verification:** V10 (code) and V11 (SQL: insert a synthetic row with non-default values, delete it the way
the purge does, confirm zero rows remain).

---

## 6. Apply the schema — owner action in Rider

No migration is involved. `DbMaintenance` creates, secures and seeds from the EF model, and `dbcheck`
detects drift.

**Precondition (owner, Rider):** the owner must have the `DbMaintenance` project built in Rider **after**
the injury columns landed in `AppRepoModels.cs` (file modified 2026-10-31 20:45). A stale binary reports
`compatible` with the old fingerprint and both columns missing.

**Owner step (owner only, in Rider):** choose the `MuscleCuties.DbMaintenance` run configuration, Release
configuration, with the single program argument `dbinit` and the connection string in
`ConnectionStrings__PostgreSql`. No agent performs this step. Repeat for every database reachable with the
owner's credentials. On a cluster that already carries the app roles, the operation aborts with a generic
`PostgresException`; such a database is **not** applied and must be named in the report.

**Agent checks — SQL only:**

```sql
select column_name, data_type, is_nullable, ordinal_position
from information_schema.columns
where table_schema = 'app' and table_name = 'workout_injury_logs'
order by ordinal_position;
```

`worsening_trigger` = `character varying`, `NO`, position 7. `clinician_advised_avoid` = `boolean`, `YES`,
position 8. `dbcheck` must report `compatible`, 69 model tables, 51 policies, zero failure codes, with
fingerprint `8f3a3f584ec386ef40e9d8c96ca0e43c653822d330370d53412c84c79da021c5`.

**Unverified in the repository:** binary currency and database reachability. Only the owner can confirm
those in Rider.

---

## 7. STATUS.md — done

`STATUS.md` now records the gate active under `MC-PLANNING-REVIEW-TEST`, marked **provisional** and not a
clinical sign-off, with the disclosure limitation stated. The unreferenced phase key has been removed from
both catalogs. Schema application and artifact freshness remain owner checks; no schema result or new
artifact hash is claimed by this document.

---

## 8. Commit — still open

One commit with the gate activation, the disclosure, `STATUS.md` and this document. Stage by path: the
worktree carries unrelated pre-existing modifications (Host/API files, the removed reconciliation project,
DbMaintenance changes). Do not stage `artifacts/` (gitignored) or any `.env`.

```
Activate the cycle/readiness approval gate and ship the safety disclosure

The gate shipped withheld because no review record existed, so every automatic
optional-set reduction was dead code. Activate it with the provisional fixture
reference and state the limitation in the product: the privacy page, cycle page,
injury label and session stop reason now say the guidance is not clinically
approved and that a professional should be consulted, while strength training
continues to be prescribed.
```

---

## 9. Repackage the artifact — completed for staging

The staging package was rebuilt from `be9589eb` on 2026-11-01, validated, and deployed to
`musclecuties-backend-api` version 5. The artifact is 8,669,964 bytes, 35 files, no PDBs,
`linux-arm64` framework-dependent, SHA-256 `d7fc3a89…`, with a manifest and zip integrity verified.
`/health/live` and `/health/ready` returned HTTP 200. Production remains separately gated; owner UI
verification remains required.

`global.json` pins SDK `10.0.403` with `rollForward: disable`, and the installed Lambda packaging tool
cannot resolve that SDK, which is why this project packages by publishing and zipping. `artifacts/` is
gitignored.

---

## 10. Verification commands (grep, sed, unzip, SQL — no builds)

`rg` is not installed on this machine. Every row below therefore uses `grep`, `grep -RIn`/`grep -RInE` or
`grep -F` in place of `rg`, and each row is runnable as written in the repository root.

| ID | Command | Expected |
|---|---|---|
| V1 | `grep -n "CycleReadinessPrescription = new(" src/MuscleCuties.Core/Domain/Workout/Planning/PlanningReleaseApproval.cs` | one hit, line 25 |
| V2 | `grep -n "MC-PLANNING-REVIEW-TEST\|TrainingPrescriptions\|AutomatedProgression" src/MuscleCuties.Core/Domain/Workout/Planning/PlanningReleaseApproval.cs` | reference plus both sections |
| V3 | `grep -RIn "PlanningReleaseApproval" src tests` | definition plus consumers, no `= null` |
| V4 | `grep -c "DisclaimerTitle" src/MuscleCuties.UI/Pages/Profile/ProfilePrivacyPage.xaml` | 1 |
| V5 | `grep -n "DisclaimerTitle.Text\|DisclaimerBody.Text\|DisclaimerPlanBody.Text" src/MuscleCuties.UI/Pages/Profile/ProfilePrivacyPage.xaml.cs` | 6 hits (3 EN, 3 RU) |
| V6 | `grep -n "Cycle_Page_MedicalDisclaimer" src/MuscleCuties.UI/Pages/Cycle/CyclePage.xaml` | line 74 |
| V7 | `grep -n "Injury_Log_Safety_StopAndSeekCare" src/MuscleCuties.UI/Controls/Injury/InjuryLogContent.xaml` | line 185 |
| V8 | `grep -n "Workout_Session_AdjustmentSafety" src/MuscleCuties.UI/ViewModels/Workout/WorkoutSessionViewModel.cs` | line 371 |
| V9 | `grep -rn "Workout_PhaseAdjustment_CurrentSafetyReviewRequired" . --exclude-dir=docs --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git` | no hits; key removed |
| V10 | `grep -RIniE "account.{0,20}export\|/export\|data export" --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git --exclude-dir=artifacts . \| grep -v "^./docs/"` | no hits |
| V11 | `sed -n '28p;160p;381p' src/MuscleCuties.Infrastructure.PostgreSql/Account/Deletion/PostgresAccountPurgeStore.cs` | inventory, delete, residual |
| V12 | SQL from section 6 on each applied database | 2 columns, exact fingerprint |
| V13 | `grep -n "TargetFrameworks>" src/MuscleCuties.UI/MuscleCuties.UI.csproj` | iOS + Mac Catalyst unconditionally; a Windows target is appended only under a Windows host; no Android |
| V14 | `unzip -l artifacts/deploy/MuscleCuties.Server.zip \| tail -1` | current 35 files |
| V15 | the fixed-string block V15-A…V15-L below | every command prints `1` |
| V16 | `grep -n "MC-PLANNING-REVIEW-TEST\|provisional" STATUS.md` | one or more hits in the leading provisional paragraph |

**V15 — fixed-string proof for section 4.** Run from the repository root; every command must print `1`.
Nothing here compiles or executes the app.

```bash
L=src/MuscleCuties.UI/Services/Localization
EB=src/MuscleCuties.UI/Pages/Profile/ProfilePrivacyPage.xaml.cs
XB=src/MuscleCuties.UI/Pages/Profile/ProfilePrivacyPage.xaml
grep -Fc 'DisclaimerTitle.Text = "Not clinically approved";' "$EB"                    # V15-A
X='MuscleCuties is not a health consultant, a medical service or a substitute for professional care.'
grep -Fc "$X" "$EB"                                                                    # V15-B code-behind
grep -Fc "$X" "$XB"                                                                    # V15-C XAML
grep -Fc 'Workouts and plans are still applied. The app will keep giving you strength training' "$EB"   # V15-D
grep -Fc 'Workouts and plans are still applied. The app will keep giving you strength training' "$XB"   # V15-E
grep -Fc 'DisclaimerTitle.Text = "Не одобрено клинически";' "$EB"                     # V15-F
grep -Fc 'Не одобрено клинически. MuscleCuties не является медицинским консультантом' "$L/RussianUiText.CycleWorkout.cs"  # V15-G
grep -Fc 'Тренировки и планы при этом применяются с учётом того, что вы сообщили.' "$L/RussianUiText.CycleWorkout.cs"       # V15-H
grep -Fc 'Stop if pain worsens and seek care from a qualified professional.' "$L/UiTextCatalog.CycleWorkout.cs"            # V15-I
grep -Fc 'Остановитесь, если боль усиливается' "$L/RussianUiText.CycleWorkout.cs"    # V15-J
grep -Fc "Review today's symptoms before starting this workout." "$L/UiTextCatalog.CycleWorkout.cs"                      # V15-K
grep -Fc 'Перед началом тренировки проверьте сегодняшние симптомы.' "$L/RussianUiText.CycleWorkout.cs"                 # V15-L
```

Section 4.3's English cycle disclaimer is longer than the fragment above; verify it whole with
`grep -Fc 'Workouts and plans are still applied, adjusted to what you tell it.' "$L/UiTextCatalog.CycleWorkout.cs"`,
which must also print `1`.

---

## 11. Traps

1. **The work is already applied.** Re-running section 4's edits duplicates the privacy card and double-appends
   the disclosure text. Verify first, edit only where V15 fails.
2. **The phase stop-reason key was removed.** Do not re-add it unless a phase-adjustment surface returns; no current page consumes it.
3. **Stale `DbMaintenance` binary.** A stale build reports `compatible` with the old fingerprint.
4. **Cluster with existing roles.** `dbinit` aborts with a generic `PostgresException`; that database is not
   applied.
5. **Fixture values are not a sign-off.** Never describe them as reviewed.
6. **Russian is a draft.** Flagged, not native-reviewed.
7. **`rg` does not exist here.** Use `grep`.
8. **The staging artifact is current; production is separate.** Do not use the staging hash as production release evidence.

---

## 12. Audit record — final state after the verification pass (2026-11-01)

Verification was performed entirely by reading files and running `grep`, `grep -F`, `sed`, `unzip -l`/`unzip -t`
and SQL inspection. **No build, test, compile, publish or app run was performed, and none may be performed by an
agent in this repository.** Anything that needs compiled or running code is listed in 12.5 as unverified in repo.

### 12.1 ApplyLanguage and catalog assignments

| # | Claim | Proof run | Evidence | Change made |
|---|---|---|---|---|
| A1 | The repo has exactly one `ApplyLanguage` body, on the privacy page | `grep -RIn "ApplyLanguage" src --include=*.cs` | `ProfilePrivacyPage.xaml.cs:22` (call), `:25` (definition); no other hit | Document's garbled "The two `ApplyLanguage` surfaces are the privacy page only" reworded to state one body with two branches |
| A2 | Privacy card EN assignments | `grep -n "DisclaimerTitle.Text\|DisclaimerBody.Text\|DisclaimerPlanBody.Text"` | lines 41, 42, 43 | none — already correct |
| A3 | Privacy card RU assignments | same grep | lines 59, 60, 61 | none — already correct |
| A4 | Catalog keys are **not** in any `ApplyLanguage` body | same grep, plus `grep -RIn "ApplyLanguage" src --include=*.cs \| grep -v ProfilePrivacyPage` | no hits outside that file | none — document already said so; now stated explicitly |
| A5 | `Workout_Session_AdjustmentSafety` (session stop reason) | `grep -F` on both catalogs | EN `UiTextCatalog.CycleWorkout.cs:10`, RU `RussianUiText.CycleWorkout.cs:10`, both verbatim | none — path/line/string correct |
| A6 | `Injury_Log_Safety_StopAndSeekCare` (injury safety label) | `grep -F` on both catalogs | EN `:36`, RU `:36`, both verbatim | none |
| A7 | `Cycle_Page_MedicalDisclaimer` | `grep -F` on both catalogs | EN `:130`, RU `:130`, both verbatim | none |
| A8 | Phase safety key before removal | `grep -F` on both phase catalogs | EN `:38`, RU `:38`, both verbatim | Removed from both catalogs on 2026-11-01; no consumer existed |
| A9 | Catalog directory | `glob **/*UiTextCatalog*.cs`, `**/RussianUiText*.cs` | all live in `src/MuscleCuties.UI/Services/Localization/` | Directory was absent from the document; added. A `Resources/Localization` path does not exist |
| A10 | Document contains no proposed replacement copy | `grep -nE "\bNEW\b"` over this file | no hit | Statement kept; absence-of-NEW is now explained rather than implied |
| A11 | Render sites | `grep -n` per site | `CyclePage.xaml:74`, `InjuryLogContent.xaml:185`, `WorkoutSessionViewModel.cs:371` | none |
| A12 | Phase key is gone | `grep -rn … --exclude-dir=docs` | no hits outside `docs/` | removed from both catalogs |

### 12.2 Privacy card

| # | Claim | Proof run | Evidence | Change made |
|---|---|---|---|---|
| B1 | The card exists and must not be inserted again | read `ProfilePrivacyPage.xaml` | `<Border Grid.Row="4" …>` opens line 69, closes line 81 | Span corrected from `69–80` to `69–81` |
| B2 | Five rows, all occupied | `grep -n "Grid.Row=\|RowDefinitions"` | `RowDefinitions="Auto,Auto,Auto,Auto,Auto"` (18); rows at 20, 26, 36, 54, 69 | none — five rows confirmed, no free row |
| B3 | Element positions | `grep -n "DisclaimerTitle\|DisclaimerBody\|DisclaimerPlanBody"` | elements open at 71, 74, 77; `Text` attributes at 72, 75, 78 | Document's 71/74/77 were right but unscoped; a table now separates element line from `Text` line |
| B4 | Every string labelled CURRENT exists verbatim | `grep -F` ×3 in the XAML, ×6 in the code-behind | all nine PRESENT | none |
| B5 | Every string labelled NEW exists nowhere | `grep -nE "\bNEW\b"` over this file | no string is labelled NEW — the card is applied and the document proposes no copy | Now stated plainly: nothing is NEW, so nothing needs to be absent; instructions added for a future revision that does add copy |

### 12.3 Build / test / publish sweep

| # | Claim | Proof run | Evidence | Change made |
|---|---|---|---|---|
| C1 | No `dotnet` build/test/publish/run/lambda/pack instruction | `grep -nEi "dotnet (build\|test\|publish\|run\|lambda\|pack)\|msbuild"` | no hit | none needed |
| C2 | No IDE or terminal command-line instruction | `grep -nEi "rider\|terminal\|command line"` | Rider mentioned only as the owner's action (§6, §9, §2 build policy) | §6 owner step reworded from "run … in Release" to "choose the run configuration" so no CLI-shaped wording survives |
| C3 | No publish instruction in a code block or numbered step | `grep -n '^```'`, `grep -nE '^[0-9]+\. '` | fences are C#, bash-grep, SQL, and the archive block; numbered steps are the trap list | §9 heading and its archive block relabelled: the owner's own action, compiles nothing, not an agent check |
| C4 | Every surviving check is grep/read/unzip/SQL | review of V1–V16 | V1–V11, V13, V15, V16 grep/sed; V12 SQL; V14 `unzip -l` | §10 preamble now states the `grep`-only rule explicitly |

### 12.4 Earlier corrections, re-confirmed after these edits

| # | Claim | Proof run | Evidence | Change made |
|---|---|---|---|---|
| D1 | Phase stop reason attribution (before removal) | read §4.1 table and both partial pairs | `WorkoutPhaseAdjustment.cs:38` / `RussianUiText.WorkoutPhaseAdjustment.cs:38`; the `CycleWorkout` partials carry only 10/36/130 | Key removed on 2026-11-01; attribution is historical |
| D2 | Purge references 28 / 160 / 381 | `sed -n '28p;160p;381p' PostgresAccountPurgeStore.cs` | 28 `typeof(WorkoutInjuryLogRecord)`; 160 `purgedCount += await dbContext.WorkoutInjuryLogs`; 381 `WorkoutInjuryLogs.AnyAsync(…)` residual check | none — all three land correctly; inventory set at 15 and assertion method at 353 also confirmed |
| D3 | `grep -RIn` substituted for `rg` everywhere | `grep -n "rg\b"` + review of V1–V16 | no verification row uses `rg`; §10 states `rg` is not installed | §10 preamble expanded to say every row is committed to `grep`/`grep -RIn`/`grep -F` and is runnable as written |
| D4 | Gate line reference | `grep -n "CycleReadinessPrescription = new("` | line **25** (reference constant 15, date 17–18) | **Corrected from 22 → 25 in §1 and V1** |
| D5 | `STATUS.md` records the provisional note | `grep -n "MC-PLANNING-REVIEW-TEST\|provisional" STATUS.md` | leading paragraph names `MC-PLANNING-REVIEW-TEST` and says provisional | §7 closed |
| D6 | Artifact is stale | `ls -l`, `unzip -l \| tail -1`, `shasum -a 256`, `unzip -t` | 8,666,753 bytes, Oct 31 21:06, 35 files, sha `de6b853ca1f6…`, integrity clean, manifest `source_sha` `9bfc4c36…`, `worktree_dirty` `true` | §1 row now quotes the full hash and the integrity/manifest results |
| D7 | UI target frameworks | `grep -n "TargetFrameworks>" MuscleCuties.UI.csproj` | `net10.0-ios;net10.0-maccatalyst`, with `net10.0-windows10.0.19041.0` appended only on a Windows host | V13's expected value corrected: the document previously implied iOS + Mac Catalyst only; no Android entry exists |

### 12.5 Unverified in repo — the owner confirms in Rider

Nothing here was built, tested, published or executed. These claims cannot be settled by reading files.

| # | Unverified item | Why it cannot be checked here | What the owner must confirm in Rider |
|---|---|---|---|
| U1 | `DbMaintenance` binary currency | A compiled binary's currency depends on when the owner's Rider build ran; a stale binary reports `compatible` with the old fingerprint | Build after the `AppRepoModels.cs` change, then apply `dbinit` and re-check the fingerprint |
| U2 | Schema actually applied per database | Requires a live connection and credentials not available to file-based checks | Confirm `worsening_trigger` varchar NOT NULL at position 7, `clinician_advised_avoid` boolean NULL at 8, and `dbcheck` fingerprint `8f3a3f58…` on every reachable database; name the unreachable ones |
| U3 | EN/RU strings render as written | Rendering needs the app running | Open the privacy page, cycle page, injury log and workout session, switch language, confirm the four surfaces |
| U4 | Gate actually admits `CycleReadinessPrescription` under `IsApproved` | That is an execution result | Run the existing test suite and the app's cycle flow |
| U5 | Artifact freshness | Packaging requires a publish, which is a build | Publish in Rider, then repackage; the size and hash will differ from `8666753` / `de6b853c…` |
| U6 | Russian wording quality | Owner approval is not a native or clinical review | Keep the owner approval recorded; a native reviewer may still improve the wording |
| U7 | Commit | Not yet done | Not a build item; owner authorization still required per §8 |
