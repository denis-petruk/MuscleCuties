# Simulator test report — 2026-11-09

## Executive summary

- **Tested SHA:** `f1918e138634ba39c2483bde3e1194b6d346f2a8` on `integration/beta-polish-20261109-v2`, verified by
  fetching and checking out a clean detached worktree (`git rev-parse HEAD`; `git status --short` empty).
- **Devices:** none exercised. The iOS simulator app **could not be built** in the agent sandbox: MSBuild needs Unix
  domain sockets for its worker nodes and the ILLink `TaskHostFactory` task host, and the sandbox denies `AF_UNIX`
  (`EPERM`). Simulators were available (iPhone 17 Pro iOS 26.3, iPhone SE 3rd gen).
- **Counts (21-row checklist):** PASS 0 · FAIL 0 · BLOCKED 21. Supplementary: E-01 server age gate PASS (API); G08
  registration PASS (API); T-21 confirmed open; everything else BLOCKED.
- **What did run at this tip:** local PostgreSQL migrate/seed/dbcheck (schema 7, fingerprint `25ff237d…8c982c4b`,
  compatible), the local API (loopback), a synthetic registration/login journey over the API, server age-gate
  boundaries, the Core.Tests build (T-21), EN/RU catalog parity, a static XAML styling scan, contrast computation, and
  the CI run via the GitHub API.
- **Top defects:** Q-01 build blocked (P0, environment, gate impact only); S-01 bare `Primary` caption contrast
  3.42–3.99:1 (P1); Q-02 stale age error (P2); Q-03 18th-birthday default can't be confirmed (P2, hypothesis); S-03
  error labels announced as headings (P2).
- **Ready for another simulator pass?** Yes once the build runs outside the sandbox. No code change is required to
  attempt it. The branch is unchanged, and CI is green at the tip.
- **Verdict unchanged:** NO-GO for external testers and for internal TestFlight. G12 stays NOT RUN/BLOCKED.

## Gate matrix

| Gate | Prior status | This run | Evidence | Open because |
| --- | --- | --- | --- | --- |
| G05 Scheduling | PASS tests; prior-head simulator | BLOCKED | CI `verify_plan_generation` green at tip; no plan accepted here | No simulator; plan acceptance not driven over the API |
| G08 Onboarding | PASS tests + prior-head sim | API PASS / UI BLOCKED | register 201, login 200, quiz rule 400 `InvalidQuiz` as designed | Journey 1 UI needs the app |
| G10 Logging | PASS tests + prior-head sim | BLOCKED | — | No session logged |
| G12 Simulator | NOT RUN | **BLOCKED** | OWNER_SIMULATOR_QA_2026-11-09.md | Build blocked (Q-01) |
| G13 Persistence | NOT RUN | BLOCKED | Local DB + API up; restart/offline need the app | — |
| G15 Migrations | PASS local | Re-confirmed locally | 8 scripts, head 7, 746 seed rows, dbcheck compatible | Live Beta adoption (A4) unproven |
| G17 CI | PASS | Re-confirmed | Run 42687031242: `head_sha f1918e1…`, completed/success; jobs verify_source, verify_plan_generation, verify_ios_compile, postgres_verify / verify_postgres_compatibility, required all success | CI is compile/test evidence only |
| G18 GitHub | Branch pushed, no PR | Re-confirmed | GitHub API: 0 PRs with head `integration/beta-polish-20261109-v2` | Owner opens the PR |

### E/U findings at this tip

| ID | Code at tip | Device/simulator | Status |
| --- | --- | --- | --- |
| E-01 | Untouched-picker guard present; server age gate verified (16 → 400, 18 today → 201) | BLOCKED | Open: device re-check; plus Q-02/Q-03 |
| E-02 | EN/RU copy exists for the key | BLOCKED | Open: device re-check |
| E-03 | `Keyboard="Plain"` on username | BLOCKED | Open: device re-check |
| E-04 | Messages in fixed footer | BLOCKED | Open |
| E-05 | Copy localized; API returns the quiz phase (`Follicular`) | BLOCKED | Open: data half needs the app |
| E-06 | Localized Moderate | BLOCKED | Open |
| E-07 | Load-gated empty state | BLOCKED | Open |
| U-09 | Keys used | BLOCKED | Open |
| U-10 | Keys used | BLOCKED | Open |

## Blocker verification

Legend: **R** = reproduced or exercised in this run, **E** = still open from repository evidence, **O** = blocked by
owner or live access.

| Item | Current evidence | This run | Why open | Gate impact | Kind |
| --- | --- | --- | --- | --- | --- |
| G12 | 21 NOT RUN rows | R: build blocked, 21 BLOCKED | No app binary | Blocks TestFlight | Environment → device |
| G13 | Not run | R: DB/API up, UI blocked | Needs app restart/offline | Blocks TestFlight | Device |
| QA01 | None | O: no physical iPhone, no binary | Physical device required | Blocks external | Physical device |
| E-01…E-07, U-09, U-10 | Code fixes, CI compile | R (code + API halves) | No rendered evidence | G12 | Code (done) + device |
| G05/G08/G10 | Prior-head sim | R partial (G08 API) | Not re-run in UI | G12 | Device |
| G06/G07 | Partial; S-04 missing | E | Mid-session pain surface absent (`InjuryLogViewModel.cs:379` refuses a save during a session) | External testers | Owner decision → code |
| S-03 | Set-only reduction; no load datum | E | Policy 4/5 not in production path | External testers | Owner decision |
| S-04 | No in-session pain action | E | Surface undecided | External testers | Owner decision |
| S-05 | 7-day window in code, not preview | E | No approved source | External testers | Owner decision |
| A11 | Repo adds SNS subscription | O | Deploy + email confirmation are owner actions | External | Live AWS |
| A1–A6 | Live posture unchanged | O (not touched) | `$LATEST`, Staging name, 1-day backups, dbcheck adoption, aliases, plaintext secrets | External | Live AWS |
| G14 | Repo PASS / live FAIL | O | Plaintext Lambda env secrets until A6 | External | Live AWS |
| G15 | Local PASS | R: re-confirmed locally | Live Beta dbcheck/adoption unproven | External | Live AWS |
| G18 | No PR | R: 0 PRs | Owner opens PR | TestFlight | Owner |
| Internal TestFlight | OD-5, OD-2/3/4/6/7/8, E-01/E-02 re-check, archive/sign/upload | E/O | Owner confirmations + device | TestFlight | Owner + device |
| External testers | QA01, App Store privacy/demo/feedback, A1–A6 | O | — | External | Owner/live |
| T-21 | 4 analyzer warnings | R: 4 confirmed (1030 ×1, 1031 ×2, 1025 ×1) | Test hygiene not done | Build gate not zero-warning | Code |
| PR/CI | Green | R: green at tip | Not simulator or release approval | — | — |

## Commands and logs

All logs live in [../release-audit/2026-11-09/evidence-owner-qa-2026-11-09/](../release-audit/2026-11-09/evidence-owner-qa-2026-11-09/)
(paths with `~` substituted; OTP lines and emails removed).

```
git fetch origin integration/beta-polish-20261109-v2
git worktree add --detach .agent-worktrees/qa-20261109 f1918e138634ba39c2483bde3e1194b6d346f2a8
sw_vers; xcodebuild -version; xcrun simctl list runtimes; dotnet --version; dotnet workload list
dotnet restore src/MuscleCuties.UI/MuscleCuties.UI.csproj -p:RuntimeIdentifier=iossimulator-arm64 -m:1 -nodeReuse:false   # restore.log, OK
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64 --no-restore -m:1 -nodeReuse:false   # build-ios.log, MSB4216
dotnet build … (default nodes)                                    # build-ios-2.log, FAILED 0 errors after 5 min
dotnet build tests/MuscleCuties.Core.Tests --no-restore -m:1 -nodeReuse:false   # build-coretests.log, 0 errors / 6 warnings
dotnet test tests/MuscleCuties.Core.Tests --no-build              # test-core.log, aborted (testhost socket denied)
dotnet test … --filter FullyQualifiedName~LocalizationCatalogTests --diag …   # test-loc.log, vstest-diag-excerpt.log
DbMaintenance migrate | seed | dbcheck  (ConnectionStrings__PostgreSql → 127.0.0.1:55461/mc_qa_20261109)   # db-*.log
MuscleCuties.Server.dll (Development, ASPNETCORE_URLS=http://127.0.0.1:5080, per-run generated keys)   # api-requests-redacted.log
GitHub API: actions/runs/42687031242, …/jobs, pulls?head=…        # results quoted above
```
