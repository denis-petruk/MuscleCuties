# Beta polish review packet — 2026-11-09

Reviewer-facing account of `integration/beta-polish-20261109-v2`, written to double as the PR body. It is structured
after the repository's [PR template](../../.github/PULL_REQUEST_TEMPLATE.md). Base: `main` `a99fd80c` (the PR #46 merge).
As of 2026-11-09 **no PR exists for this branch**; the owner opens it. Nothing here is device, simulator, macOS or AWS
evidence.

## Summary & scope

| Field | Value |
| --- | --- |
| Package | `beta-polish-20261109` (integration follow-up to PR #46) |
| Logical role | Integration Authority (I) |
| Base | `main` `a99fd80c` |
| Branch | `integration/beta-polish-20261109-v2` |
| Last code commit | `5a752d8` |
| Pre-packet tip | `6ee9d53` (docs-only) |
| This packet | the docs-only commit that adds this file |
| Diff through `6ee9d53` | 17 files, +821 / −77 |

The branch adds the E-04…E-07 fixes, the U-09/U-10 localization repairs and the A11 repository half of the alarm
subscription. It does not change backend, schema, migration, DTO or policy behavior.

**Suggested PR title:** `Beta polish: onboarding, session, Today and localization fixes`

## What changed

### UI and localization (code)

| Item | Change | Commit |
| --- | --- | --- |
| E-04 | Onboarding validation and acknowledgment copy moved into the fixed action footer above Continue, matching the quiz page | `ecf556a` |
| E-07 | Session empty/choice states require a completed load attempt; a failed load still shows the error card | `17e5be0` |
| E-06 | Strength days report localized Moderate effort instead of inferring Heavy from the activity tag | `afb459a` |
| E-05 copy half | Today's phase header, status, title and advice come from the catalog with the localized phase name | `afb459a` |
| U-09/U-10 | Featured workout card and dashboard count tile localized in EN and RU | `5a752d8` |

E-05's data half is not reproducible from source: the dashboard store already anchors the quiz phase and falls back to
the profile phase, and the view model re-notifies every phase property. A device run decides it; no code change was
invented. Full per-defect evidence is in [AUDIT.md](AUDIT.md).

### Infrastructure (repository only)

`infra/cloudformation/observability.yaml` adds `AlarmEmailSubscription` (`AWS::SNS::Subscription`) to the existing
`AlarmTopic`, using the existing `NotificationEmail` parameter. It adds no address or secret. It was **not deployed**;
the live change set and the email confirmation are owner actions ([INFRA_RULES_2026-11-09.md](../infra/INFRA_RULES_2026-11-09.md)).

### Docs and owner tooling

`scripts/beta-release-wizard.sh` (5 stages) walks the owner through the PR, branch CI, Mac simulator acceptance, the
alarm subscription and the four policy decisions. The readiness/audit/policy/infra records and the dated simulator
checklist ship here. These commits are documentation-only: `3c48236`, `1f71f83`, `54d8826`, `91886e2`, `6ee9d53`, and
this packet.

## Architectural & safety contracts

- Dependency direction is unchanged: the diff is UI/localization, one SNS resource, the owner wizard and docs. No Core,
  UseCases, Infrastructure or server dependency changes.
- No shared DTO, migration or schema change; no workout generation, acceptance, tri-state injury, hydration or history
  semantics change.
- No credentials or secrets are added. The SNS subscription references the existing `NotificationEmail` parameter.
- No cloud mutation was performed; `observability.yaml` is a repository change and its deployment remains an owner
  action.
- Branch protection readback and merge approval were not performed; they remain owner gates.

## Verification evidence

### CI at the pre-packet tip

| Evidence | Result |
| --- | --- |
| Run [42677684566](https://github.com/denis-petruk/MuscleCuties/actions/runs/42677684566) at `6ee9d53` | completed successfully |
| Commit checks | 10 successful, 0 failing, 0 pending |
| Jobs | `verify_source`, `verify_plan_generation`, `verify_ios_compile` (xcode-27), `postgres_verify` / `verify_postgres_compatibility`, aggregate `required` |
| Branch badge | `CI - passing` |

Earlier runs [42675311877](https://github.com/denis-petruk/MuscleCuties/actions/runs/42675311877) at `1f71f83` and
[42676415439](https://github.com/denis-petruk/MuscleCuties/actions/runs/42676415439) at `54d8826` were also green. The
`CI` workflow triggers on every `integration/**` push, so the docs-only packet commit is covered too; the live run list
is [here](https://github.com/denis-petruk/MuscleCuties/actions/workflows/ci.yml?query=branch%3Aintegration%2Fbeta-polish-20261109-v2).
This packet does not name its own run ID; the reviewer should read the tip's run.

### Local checks recorded at this revision

These are recorded in [RELEASE_READINESS.md](RELEASE_READINESS.md) with their commands. They were re-read while writing
this packet but not re-run during this documentation pass.

| Check | Result |
| --- | --- |
| Non-UI builds | Core.Tests / Mobile.Contracts.Tests / Registration.Tests / Server.Tests / Deployment.Tests success; Core.Tests has 4 pre-existing analyzer warnings (T-21) |
| Unit suites | Core 1325/1325; Mobile Contracts 183/183; Registration 54/54; 0 failed, 0 skipped |
| Deployment policy | 17/17 |
| Localization catalog | 8/8 |
| EN/RU parity | 2036 / 2036 keys; 0 missing, 0 extra, 0 format-argument mismatches |
| Wizard and edited assets | `bash -n` passes; edited XAML parses; `observability.yaml` parses with CloudFormation tags |

PostgreSQL compatibility was exercised only by CI (`postgres_verify`, green); no local PostgreSQL run was made here.

### G12 Simulator acceptance: NOT RUN

The dated record is
[SIMULATOR_ACCEPTANCE_2026-11-09.md](../release-audit/2026-11-09/SIMULATOR_ACCEPTANCE_2026-11-09.md). **Every checklist
row is NOT RUN** — E-04…E-07, J01–J15, CR01 and QA01. No PASS is claimed.

- Host: `Linux 6.1.102 x86_64`; `xcrun`, `simctl` and `xcodebuild` are absent; `dotnet workload list` is empty.
- The simulator build exits `1` with `NETSDK1147` (missing `maui-tizen`, `wasm-tools`; `dotnet workload restore`). Even
  installing workloads would not provide Apple's toolchain or CoreSimulator on Linux.
- CI's `verify_ios_compile` at `6ee9d53` compiled the UI. That is compile evidence, not install, launch, navigation,
  rendering, localization, contrast, Dynamic Type or journey evidence.

The Mac procedure is in the record: build `net10.0-ios`/`iossimulator-arm64`, boot the smallest supported iPhone and an
iPhone 17 Pro, install the `.app`, launch it, run light and dark in EN and RU (largest Dynamic Type at least once),
capture `simctl` screenshots, then replace every `NOT RUN` with a real result and fold the record into `G12`. Stage 3 of
`scripts/beta-release-wizard.sh` performs the same steps and writes `OWNER_SIMULATOR_QA_<date>.md`.

## Verdict

**NO-GO for external testers. NO-GO for internal TestFlight until the Mac simulator/device acceptance and the owner
actions are complete.**

The deciding reason is `G12 Simulator` = **NOT RUN**: there is no macOS/Xcode here, the recorded build attempt failed at
`NETSDK1147`, and the dated record contains no PASS rows. `QA01`, `G13`, S-03/S-04/S-05 and the live AWS items are also
open. The green CI run proves compilation and tests only.

| Gate | Status | Basis |
| --- | --- | --- |
| G01 Build | PASS (compile) | Non-UI build here; UI compiled by `verify_ios_compile` in CI |
| G02 Unit tests | PASS (local) | 1325 / 183 / 54 / 17, zero failures |
| G11 Integration | PASS (CI) | `postgres_verify` / `verify_postgres_compatibility` |
| G12 Simulator | **NOT RUN** | Dated record; all rows NOT RUN |
| G13 Persistence | NOT RUN | No device restart/offline journey |
| G17 CI | PASS | Run 42677684566 at `6ee9d53` |
| G18 GitHub | Branch pushed; no PR | Owner opens the PR |

The full gate table, including its open items, is in [RELEASE_READINESS.md](RELEASE_READINESS.md).

## How to review without a Mac

1. Review the code commits and the 17-file diff; no backend, schema, DTO or migration is touched.
2. Read the CI run and job list above; the UI compiles and the unit/integration suites pass.
3. Read the dated record as a blocker, not a result: G12 is unrun, so no runtime behavior is verified by this branch's
   evidence.
4. Confirm the owner actions below; a green CI run is not approval to release.

## Owner actions

1. Open the PR from `integration/beta-polish-20261109-v2` into `main` (base `a99fd80c`). Use this packet as the body and
   the suggested title above. No PR existed on 2026-11-09; PR #47 is unrelated.
2. Delete the stale `integration/beta-polish-20261109` branch. It holds `4ee9b4f` (a session snapshot) and has failed run
   42673683274; do not open a PR from it.
3. Run stage 3 of `scripts/beta-release-wizard.sh` on a Mac, fill the dated record with real results, and fold them into
   `G12`.
4. Re-check E-01, E-02 and E-04…E-07 on the smallest supported iPhone and iPhone 17 Pro, light and dark, EN and RU.
5. Answer S-03 (load step vs set-only), policy 5 (cycle dose), S-04 (pain surface) and S-05 (seven-day window) in
   [WORKOUT_POLICY_RESEARCH.md](../release-audit/2026-11-09/WORKOUT_POLICY_RESEARCH.md).
6. If authorized, deploy `observability.yaml`, confirm the change set adds only `AlarmEmailSubscription`, and click the
   SNS confirmation email. No live AWS change was made here.
7. Before internal TestFlight: confirm OD-5 encryption, the OD-2/3/4/6/7/8 decisions, rebuild/re-check E-01 and E-02,
   then archive, sign and upload to the internal group only.
8. Before external testers: complete AWS A1–A6 (Beta designation, backups/deletion protection/restore drill, `dbcheck`,
   aliases, alarm subscription, Secrets Manager rotation), QA01 in EN/RU with VoiceOver and Dynamic Type, and the App
   Store Connect privacy/demo/feedback items.

## Source records

- [RELEASE_READINESS.md](RELEASE_READINESS.md) — verdict, full gate table, local commands, owner actions.
- [SIMULATOR_ACCEPTANCE_2026-11-09.md](../release-audit/2026-11-09/SIMULATOR_ACCEPTANCE_2026-11-09.md) — all-NOT-RUN
  record and blocker.
- [SIMULATOR_QA.md](SIMULATOR_QA.md) — prior-head simulator journeys 1, 2, 3 and 5; not this revision.
- [AUDIT.md](AUDIT.md) — per-defect code evidence and open items.
- [STATE.md](STATE.md) — commit list, CI evidence, blockers and next actions.
- [INFRA_RULES_2026-11-09.md](../infra/INFRA_RULES_2026-11-09.md) — live AWS change rules for A11.
