# REVIEW BRIEF — independent review of the remediation candidate

**This file is the prompt.** Hand a review agent this path and nothing else:

> Read `docs/design/orchestration/REVIEW-BRIEF.md` and carry out the review exactly as written.
> You are the independent reviewer; do not assume the author's claims are true.

The author of this brief implemented the candidate. **You are reviewing that implementation, so treat
every claim in it as unproven until you reproduce it yourself.** Finding nothing is a valid outcome, but
only after the checks below have actually been run.

## 1. Independence and limits — read before any judgement

- You must not be the implementing agent. A model reviewing its own diff is **not** independent review;
  if you are the same agent or model instance that produced these commits, stop and say so.
- If you are an AI reviewer, say so in the report. Even a clean AI review leaves RM08's independent
  human approval outstanding; you may produce `READY FOR REVIEW`, never `SAFE TO PROMOTE`.
- You may run builds, tests, grep, `git show`, and read framework/package sources. You may **not**
  modify tracked files, commit, push, deploy, migrate a database, create accounts, publish, or merge.
  If a check needs one of those, report it `BLOCKED` and name the owner action.
- The repository contract in [`AGENTS.md`](../../../AGENTS.md) binds you: never weaken a test, assertion,
  gate or safety rule to make the candidate look green, and never mark a gate `PASS` from a source read
  alone when it demands runtime evidence.
- Report every finding as `file:line` + the failing check + the smallest counter-example you can
  construct. "Seems fine" is not a finding.

## 2. Fixed review target

| Field | Value |
| --- | --- |
| Baseline (before any remediation work) | `acdb1311` |
| Planning package (docs only) | `a2d55adc` |
| Implementation | `e27a9a20`, `e44c31bf` |
| Final candidate under review | `d0e989d5` |
| Branch | `musclecuties-ui-ux-backend-remediation-kickoff` |

Review **the diff `acdb1311..d0e989d5`**, not the branch narrative. Confirm HEAD is `d0e989d5` (or record
what it actually is) and refuse to review an uncommitted working tree.

```bash
git log --oneline acdb1311..HEAD
git diff --stat acdb1311..HEAD
git diff acdb1311..HEAD -- tests/          # the assertions deserve their own read
git diff acdb1311..HEAD -- src/MuscleCuties.UI/Resources/Styles/Styles.xaml
```

Acceptance thresholds the candidate claims: **0 warnings / 0 errors** on the iOS simulator build and
**1055/1055** Core tests. Re-run both; do not accept the recorded numbers.

```bash
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -c Debug \
  -p:RuntimeIdentifier=iossimulator-arm64
dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj
```

Do not build or drive the MAUI app on the owner's booted simulator (`140E2656-…`); a spare-simulator
install is allowed only for the checks in §4 that need rendering, and native scenario capture remains
`BLOCKED` without a signed-in synthetic account.

## 3. Claims to adjudicate

Each row is a claim. Your job is to falsify it, then classify it.

| # | Claim |
| --- | --- |
| C1 | `Microsoft.Maui.Controls` 10.0.90 contains the `Button.LayoutButton` null guard that 10.0.60 lacks, and the csproj/comment say so truthfully. |
| C2 | The generation page cannot apply a stale appearance after the page is gone (`_appearanceVersion` guard), and the guard does not suppress a legitimate first apply. |
| C3 | A planning failure now surfaces as `PlanningRequestFailedException` carrying status/error code/request id, is still caught by the existing `HttpRequestException` handlers, and no path silently substitutes a local plan. |
| C4 | The user-visible recovery message differentiates offline / timeout / auth / rate-limit / server, in EN **and** RU, and leaks no URL, token, PII or stack. |
| C5 | `HistoryIngredientEditor` round-trips an untouched quantity exactly, parses an edited one predictably, and blocks commit on an invalid draft. |
| C6 | PV01 and AH01 layout fixes remove the reported defect without creating a new one (hit-testing, clipping, overflow). |
| C7 | The style gate was fixed by removing genuinely dead resources, not by deleting live ones or editing the test. |
| C8 | No file outside the declared scope changed; no DTO, schema, package graph, `MauiProgram`, platform target or health-sync scope was altered. |
| C9 | `STATUS.md`, `MISSION.md` and `RM00-CANDIDATE-AND-RESERVATIONS.md` describe the candidate truthfully and do not overstate an outstanding package. |

## 4. Hostile checks — the ones that actually catch something

Run these before writing a verdict. Each is designed around a plausible failure of the above.

**Tests were not weakened (C3, C5).** Read `git diff acdb1311..HEAD -- tests/` line by line. For each
changed assertion, state what it asserted before, what it asserts now, and whether the new form could
pass on a broken implementation. Confirm: no deleted test, no `[Skip]`/`[Ignore]`, no assertion count
drop, no `Assert.True(true)`, no catch-and-assert-nothing. A test that previously pinned
`HttpRequestException` and now pins a derived type plus preserved `StatusCode` is stronger — verify
that is what happened rather than assuming it.

**Dead-style deletion was safe (C7).** At HEAD, every deleted resource key must have zero references:

```bash
grep -rn "WorkoutHistorySecondaryBody" src/ tests/ || echo "no references (expected)"
git diff acdb1311..HEAD -- tests/ | grep -i "UiStyleGate" || echo "gate test untouched (expected)"
```

If any deleted key still appears in XAML, `{StaticResource}` or C#, that is a `FAIL`: it would resolve at
build time and crash at runtime. Then confirm the gate passes because the catalog is smaller, not because
the rule was relaxed.

**The quantity round-trip is actually exact (C5).** Construct the counter-examples rather than reading
the code approvingly: `0.1`, `33.333333`, `1000`, `1e-7`, the current-culture separator, a
`.`/`,` swap, `"1,234"`, `""`, `"0"`, `"-5"`, `"NaN"`, `"Infinity"`, `"1e999"`, and a value typed then
reverted. For each, answer: does an untouched row produce byte-identical persistence? Does an edited row
either round-trip or fail loudly? Is `"1,234"` an error rather than `1.234`? If `float` needs
round-trip formatting (`"R"`/`"G9"`) and the draft uses something lossier, that is a `FAIL` even though
it looks tidy. Then check every commit path — keyboard done, Save button, back-out, finder return — goes
through the same validation; one unguarded path is a `FAIL`.

**The contractual lock stayed contractual (C5, C8).** `IsMealChoiceLocked` must not have been reused to
shrink or hide UI. Diff its usages and confirm the semantics are unchanged; a UI disclosure toggle added
in a later package must not be able to unlock an existing meal.

**The failure boundary is not swallowed upstream (C3).** Trace the call chain above
`RequirePlanningData` to the view model catch blocks. Confirm: the typed exception is not re-wrapped into
a generic one before the presenter sees it, `InnerException`/status survive, and no `catch { }`,
`catch (Exception)` fallback silently calls a local generator. Then confirm the retry path cannot accept
a plan the server did not return.

**Localization parity (C4).** This app has no `.resx` files, so do not go looking for them. Localization
is one source catalog: English in `Services/Localization/UiTextCatalog*.cs`, Russian in
`Services/Localization/RussianUiText*.cs`, with `LocalizationCatalogTests` already asserting key-set and
format-argument parity. Confirm the `Ui_Common_Error_*` keys and `Ui_Common_Error_ReferenceFormat` that
the presenter uses exist in **both** catalogs, that the RU values are genuinely translated rather than
English copies, and that the reference suffix comes from a format key instead of a concatenated
translated fragment. Missing or untranslated RU text is a `FAIL`, not a nit.

**The new mapping cannot mislabel an online failure (C4, PG01).** `FromStatusCode` returns the caller's
`fallbackKey` for any status it does not classify, and the planning caller passes `ConnectionErrorKey`.
So read `FromPlanningFailure` and `FromStatusCode` together and answer: can a server that *answered* — a
400 with an unclassified code, a 409, a 422 — now tell a connected user they are offline? Can
`ApiFailureAction.None` still lead the screen to render a retry affordance? Either is a `FAIL`: it swaps
one wrong message for another, which is the exact defect PG01 exists to remove.

**The appearance guard cannot double-apply or no-op (C2).** Read the guard for both outcomes: a second
appearance change while a first apply is in flight, a change after dispose, and the normal first
application. If the version is captured but compared against a stale copy, or bumped after the apply
instead of on the lifecycle event, the crash precondition survives — that is a `FAIL`. Confirm the guard
is not a blanket `try/catch` that hides unrelated exceptions.

**Framework pin truthfulness (C1).** Confirm only `MauiVersion`-related lines changed in the csproj and
that no transitive package version drifted. Verify the resolution:

```bash
grep -rn "MauiVersion" src/MuscleCuties.UI/MuscleCuties.UI.csproj Directory.Build.props 2>/dev/null
ls ~/.nuget/packages/microsoft.maui.controls/ 2>/dev/null   # 10.0.90 must be present
```

If you can read the resolved package's `Button` source, check the null guard yourself; if you cannot,
mark the framework-source half `NOT VERIFIED` rather than inheriting the claim.

**Layout fixes did not trade one defect for another (C6).** For PV01 confirm the removed background was
not the only thing preventing a transparent surface from receiving taps or exposing content, and that the
replacement `Fill` does not stretch a control past its intended content width. For AH01 confirm a long
archive title at the narrowest supported width still truncates rather than pushing the trailing control
off-screen. Source reasoning is acceptable here; rendered evidence is `BLOCKED` without a signed-in
account, and must be reported as such.

**Scope and hygiene (C8, C9, G7).** Confirm `git diff --name-only acdb1311..HEAD` matches the declared
inventory with no stray file; that `docs/release-audit/` is still untracked; that no credential, token,
connection string or real user identifier appears in the diff; that no new dependency, Android target,
SQLite switch or health-sync activation slipped in; and that every `MauiProgram`/DTO/schema file is
untouched. Then check the docs against HEAD: a package recorded `LANDED` while its acceptance is
outstanding is acceptable **only** if the doc says so in the same row.

## 5. Verdict format

Write `docs/design/orchestration/RM08-REVIEW.md`. No other file. Structure:

1. **Reviewer and independence** — who/what, and whether this counts as independent approval.
2. **Target** — base/head SHAs, branch, working-tree cleanliness, commands run with their real output.
3. **Findings** — ordered by severity, each as `file:line`, severity, the failing check, the counter-example,
   and the minimal acceptable repair. Separate **defects** from **deviations from the plan** from **nits**;
   a deviation the author disclosed and justified is a judgement call, not automatically a defect.
4. **Claim table** — C1–C9 each as `PASS` / `FAIL` / `BLOCKED` / `NOT VERIFIED`, one line of evidence
   each.
   `BLOCKED` and `NOT VERIFIED` are never rounded up to `PASS`.
5. **Gates** — only those the diff touches (G0, G1, G2, G4, G5, G7, G8, G9), each with its evidence state
   and, where relevant, the owner action required.
6. **Overall verdict** — one of `READY FOR REVIEW` (scoped diff complete, applicable local checks pass,
   remaining external gates named) or `BLOCKED` (a required condition fails or evidence is missing; name
   the blocked package). You may not issue `SAFE TO PROMOTE`; that needs protected-target checks, recovery
   evidence and owner approval.
7. **Owner actions** — the concrete, minimal list still required, e.g. a signed-in synthetic staging
   account for native reproduction, the NM03 coverage contract decision, a physical-device and
   accessibility pass, and human sign-off.

If you find a `FAIL`, state the smallest repair. Do not propose a redesign, do not implement the repair
yourself, and do not soften a hard gate to produce a friendlier verdict.
