# Closed-beta audit and implementation plan — 2026-11-08

Senior review of style/UX, beta readiness, release infrastructure and workout quality, with the ordered work needed
to invite closed-beta testers. This is evidence at its recorded revision, not live deployment truth, and it authorizes
nothing: every AWS, database, signing and distribution step below still needs explicit owner authorization.

| Field | Value |
| --- | --- |
| Baseline | `main` `4ee9b4f2` (PR #39) **plus an uncommitted 54-path working-tree delta** (see `BETA_READINESS_VERIFICATION.md` appendix) |
| Lenses | `impeccable audit` (native iOS), `aws-deployment` (release path), repository G0–G9 |
| Live target inspected | AWS account `…7003`, `us-east-2`, describe/list calls only (no data reads, no secret values, no mutation) |
| Public URLs inspected | `musclecuties.app/privacy`, `/support`, `/.well-known/apple-app-site-association`, Apple AASA CDN |
| Not done | No MAUI build/run/screenshots (owner rule), no device, no deploy, no DB connection, no commit |

## 1. Verdict

**Not ready to invite external testers. Ready for an internal-only TestFlight build after Phase 0–2 below.**

| Severity | Count | Theme |
| --- | --- | --- |
| P0 — blocks beta | 9 | Unreviewed candidate, live serving `$LATEST` with no aliases, environment identity, 1-day backups/no deletion protection, unknown schema adoption path, silent alarms, native crash pin, bodyweight plans missing whole movement patterns, test-fixture approval reference in production |
| P1 — fix before external testers | 16 | Secrets in env vars, HealthKit entitlement without feature, privacy manifest, AASA, export compliance, beginner style dead-end, RU users seeing server English, exercise guidance, device/EN-RU acceptance never run |
| P2/P3 | 10 | Hardening, architecture debt, canary deploys |

What is genuinely strong and should be preserved: the domain-safety design (unknown ≠ no, withhold over unsafe, single
acceptance path), the B1 empty-DB `migrate`/`seed`/`dbcheck` chain, the digest-verified deployer with rollback, the
shared style system with executable guards (role-only colours, two button/two card budget, contrast pairs, catalog
parity 1,990/1,990), and honest in-app disclaimers ("not clinically reviewed").

## 2. Evidence collected in this pass

| Check | Result |
| --- | --- |
| `dotnet build` Release: Server, WorkoutReconciliation, DbMaintenance (current tree incl. uncommitted delta) | **0 warnings / 0 errors** each |
| `dotnet test` Core.Tests (build step) | Core, Contracts, UseCases, Infrastructure and Core.Tests **compiled** |
| `dotnet test` execution | **BLOCKED** — sandbox denies vstest's local TCP bind (`SocketException 13`). Last recorded green: Core 1,247 / Contracts 147 / Registration 46 / Server 113 (same tree, `BETA_READINESS_VERIFICATION.md`) |
| Catalog coverage simulation over `ReviewedExerciseCatalogV020.json` | See D1 |
| AWS describe pass | See section 3A |
| Public site | Privacy and Support pages live (EN/RU). AASA returns the HTML site shell; Apple CDN returns 404 |

## 3. Findings

### 3A. Release and infrastructure (live state, read-only)

| ID | Sev | Finding | Evidence | Fix |
| --- | --- | --- | --- | --- |
| A1 | P0 | **No Lambda aliases exist; API Gateway serves the unqualified function (`$LATEST`).** The B2 deployer requires `BETA_SERVER_ALIAS`/`BETA_WORKER_ALIAS` and fails before mutation without them, so the first real deploy cannot run; serving `$LATEST` violates the "no `$LATEST` serving target" contract. Worker has 1 version, last updated 2026-11-01 (older than current source). | `ListAliases` → 0 on both functions; integration URI `…:function:musclecuties-backend-api` | Publish version, create `beta` alias on both, point the HTTP API integration and Lambda permission at the alias ARN, retarget the EventBridge rule to the worker alias, set the four GitHub `vars`. |
| A2 | P0 | **The only live environment is labelled Staging everywhere, but the Beta app points at it.** RDS `musclecuties-staging-db`, `ASPNETCORE_ENVIRONMENT=Staging`, alarms/SNS/stack named staging; `AppEndpointConfiguration.BetaBaseUrl` = that API. Docs say "Beta only, no Staging provisioned". Tester data will land in a database named staging with staging config. | `GetFunctionConfiguration` (non-secret keys only), `DescribeDBInstances` | Owner decision: **designate this stack as Beta** (set environment to `Beta`, rename alarms/SNS/topic via IaC, record it in `CONTROL.md`) or provision a separate Beta. Do not run both meanings at once. |
| A3 | P0 | **Recovery posture cannot protect tester data.** Backup retention **1 day**, deletion protection **off**, single-AZ `db.t4g.micro`, no restore drill on the current schema. | `DescribeDBInstances` | Retention 14 days, deletion protection on, manual snapshot before every migration, restore drill to a temporary instance + `dbcheck` (runbook `POSTGRES_BACKUP_RESTORE_DRILL_RUNBOOK.md`), record RPO/RTO. |
| A4 | P0 | **Schema adoption of the existing database is unproven.** B1 is proven only from empty databases. The live DB was built by the older model-first/`dbinit` history; its ledger state is unknown. `db-release.yml` has no snapshot step and no pre-check; `deploy_release.py` flips code without checking schema compatibility. | Workflow + deployer source | Read-only `dbcheck` against Beta → restore a snapshot to a scratch instance → rehearse `migrate`+`seed`+`dbcheck` there → only then `db-release`. Add a snapshot step to `db-release.yml` and a schema-version gate to `deploy_release.py deploy`. |
| A5 | P0 | **Alarms notify nobody.** SNS `musclecuties-staging-operational-alarms` has **0 subscriptions**; only 2 alarms live (API error rate, worker DLQ), both `INSUFFICIENT_DATA`; `observability.yaml`'s p95-latency and EventBridge-DLQ alarms are absent. | `ListSubscriptionsByTopic`, `DescribeAlarms` | Deploy `observability.yaml` for the Beta name, confirm an email subscription, add Lambda throttles/errors, RDS CPU/free-storage/connections alarms. |
| A6 | P1 | Secrets in plaintext Lambda environment variables (DB connection string, JWT signing key, Apple and receipt private keys, HMAC keys, Resend key), AWS-managed key only. Anyone with `lambda:GetFunctionConfiguration` reads them. Rotation is still an open AUDIT §3 item. | Env **key names** only | Move to Secrets Manager (or SSM SecureString) with cached reads, rotate everything once, then remove the env values. |
| A7 | P1 | Log groups never expire. | `DescribeLogGroups` → no retention | 30-day retention on both groups. |
| A8 | P1 | API `$default` stage has no access log and no throttling. | `GetStages` | Stage throttle (e.g. 20 rps / burst 40) and access logs without query strings or bodies. |
| A9 | P1 | No reserved concurrency; `MaxPoolSize=5` × unbounded concurrency can exhaust `db.t4g.micro` connections. | `GetFunctionConcurrency`, safe env keys | Reserved concurrency ≈10 (API), 1–2 (worker). |
| A10 | P1 | Smoke check is optional (`HEALTH_URL` unset → no probe). | `backend-release.yml` | Make `BETA_API_ENDPOINT` required; probe `/health/live`, `/health/ready`, anonymous `/api/v1/session` = 401. |
| A11 | P1 | `webcredentials:musclecuties.app` is entitled, but AASA serves the HTML shell and Apple's CDN returns 404, so shared-credential AutoFill cannot work. | curl | Serve JSON (`{"webcredentials":{"apps":["<TEAMID>.com.musclecuties.app"]}}`) with `application/json`, or drop the entitlement for Beta. |
| A12 | P2 | RDS `PubliclyAccessible=true` (security group limited to one developer /32 plus two SG references). | `DescribeSecurityGroups` | Private + SSM port-forward for operator access. |
| A13 | P2 | Actions pinned by tag, not SHA; unused `MuscleCuties-GitHubActions-StagingDeploy` role; three media buckets (`media-prod`, `media-staging`, `staging-media`, one unversioned). | `ListRoles`, `ListBuckets` | Pin SHAs, delete or scope the unused role, confirm the one bucket the API uses. |
| A14 | P3 | Alias cut-over is all-at-once. Fine for ≤50 testers. | — | Later: weighted alias routing or CodeDeploy Lambda canary with alarm rollback. |

Good live signals: Lambdas in VPC, `RunMigrationsOnColdStart=false`, SSL required, DLQs empty, EventBridge rule
enabled, buckets private/encrypted/versioned, Performance Insights on.

### 3B. iOS app and TestFlight readiness

| ID | Sev | Finding | Fix |
| --- | --- | --- | --- |
| B1 | P0 | **CR01 native crash risk unresolved on `main`.** `MuscleCuties.UI.csproj` pins MAUI **10.0.60** and claims it contains the `Button.LayoutButton` fix; the repository's own primary-source evidence (remediation plan, CR01) shows the null guard first ships in 10.0.90. The RM08 candidate resolved 10.0.90 but that bump never landed (`git log -S MauiVersion` shows only the original commit). | Bump to a verified ≥10.0.90 with a compatible CommunityToolkit.Maui; correct the comment; replay the plan → Edit details → retry → rapid-back sequence on device. |
| B2 | P0 | **The candidate is not a commit.** 54 paths are uncommitted, including safety-relevant changes (removal of the per-exercise acknowledgment gate and native injury alert, default style change to General strength, catalog review import). A TestFlight build must come from a reviewed SHA. | Split into reviewed PRs (Phase 0). |
| B3 | P1 | HealthKit is half-present: `com.apple.developer.healthkit` entitlement on, `EnableAppleHealthEntitlements=true`, the real `AppleHealthDataProvider` is registered on iOS, but there is no `NSHealthShareUsageDescription` and no UI. Beta App Review rejects a HealthKit capability with no visible feature; any stray authorization call crashes for the missing purpose string. | For Beta: register `DisabledHealthSyncService` on iOS, remove the entitlement/capability (owner decision G03). |
| B4 | P1 | No `ITSAppUsesNonExemptEncryption` key → manual export-compliance answer on every upload. | Add `false` if only standard HTTPS/OS crypto is used (owner confirms). |
| B5 | P1 | `PrivacyInfo.xcprivacy` declares no collected data types while the app collects email, user ID, health & fitness, body measurements, photos and diagnostics. | Declare them (linked, not tracking, app functionality) and mirror them in App Store Connect App Privacy. |
| B6 | P1 | iPhone landscape orientations enabled; no landscape layouts or evidence. | Portrait only for Beta. |
| B7 | P2 | `UIBackgroundModes: fetch` with no handler. | Remove. |
| B8 | P2 | `ApplicationVersion` is hand-set (2); every upload needs a higher build number. | Derive from the workflow run number at archive time. |
| B9 | P2 | In-app feedback opens Mail to a personal mailbox; fails when Mail is not configured. | Rely on TestFlight feedback + a domain support address; keep Mail as fallback. |

### 3C. Style and UX — `impeccable audit` (native, source-only)

| # | Dimension | Score | Key finding |
| --- | --- | --- | --- |
| 1 | Accessibility | 2 | 390 descriptions for 130 tap targets and F01/S01 fixed in source, but device automation recorded tab items not exposed individually and accessibility frames offset by ~21–100 pt inside `WorkoutActivityPreferencePicker`; no VoiceOver pass ever run |
| 2 | Performance | 3 | Virtualized lists, SourceGen, strict XAML; 1.8k-line view models; deployed latency gate still open |
| 3 | Appearance & theming | 3 | Role-only colours, dark pairs and 22 contrast pairs are guarded; dark/large-text rendering never captured |
| 4 | Platform conformance | 3 | Native Shell tabs, stack and sheets; many tappable `Border`s instead of buttons; Fluent icon font instead of SF Symbols (owner-approved) |
| 5 | Adaptivity | 2 | Landscape enabled without layouts; largest Dynamic Type and keyboard/safe-area unverified |
| **Total** | | **13/20** | **Acceptable — provisional until rendered/device evidence exists** |

**Conformance verdict:** reads as a native app, not a web port. The residual risk is behavioural (crash, dead ends,
VoiceOver), not visual.

Status of the 2026-11-04 findings, re-checked in source today: F01 injury tri-state, F02 hydration pending/retry, F03
stale cycle cache, S01 icon action names, NM02 grouped editor, NM03 breakdown presenter, AH01 archive width, PV01
privacy surface, PG01 typed failure contract — **addressed in source, unverified on device**. S02 shared header: 22/24
pages (startup and session intentionally excepted). CR01 — **open** (B1).

New UX findings:

| ID | Sev | Finding | Fix |
| --- | --- | --- | --- |
| C1 | P1 | **Beginner style dead end.** A beginner can pick High-volume (or High-intensity) strength, which the reviewed templates restrict to established history; the planner returns zero exercises, Apply stays disabled and the user reads "No safe strength exercises remain". Observed in the recorded simulator sittings (`BETA_READINESS_VERIFICATION.md`). The uncommitted default change to General strength only fixes new accounts. | Show eligibility in the activity picker: disable or label styles that need training history, with one-line reason and the alternative; keep the server rule authoritative. `impeccable clarify` + `harden`. |
| C2 | P1 | **Russian users see server-authored English.** Rest-day titles ("Living happy life", "Pure rest day"), "Scheduled for today", meal-suggestion names/descriptions and "Nutrition focus" are composed in PostgreSQL stores and shipped as display text. | Return codes/keys and localize on the client; additive DTO fields under an integration reservation. |
| C3 | P1 | Rest-day title copy ("Living happy life") does not meet the "respectful, adult language" direction in DESIGN.md. | Replace in the same change as C2. |
| C4 | P1 | Never observed end-to-end: a feasible preview with exercises → Apply → strength session with per-set rows → summary → next prescription. All evidence stops at the preview. | QA01 scenario 1 (Phase 4). |
| C5 | P2 | Tab bar and picker accessibility (see score 1). | VoiceOver pass; if confirmed, expose per-row semantics in the picker. |

### 3D. Workout quality and safety

| ID | Sev | Finding | Fix |
| --- | --- | --- | --- |
| D1 | P0 | **Equipment profiles produce plans with whole movement patterns missing.** Simulated over the 59 reviewed exercises and 52 template slots (beginner, no injuries): **Bodyweight only** — 0 candidates for every knee-dominant slot (no squat, lunge or split squat exists), 0 for every upper-pull slot, 0 vertical push; **Dumbbells and bands** — 0 vertical pull and a single candidate for squat and horizontal pull. The planner safely omits unfillable required slots (`MovementOmitted`), so a bodyweight tester never receives a squat, lunge or pull. Only 10 bodyweight exercises exist and no band-only pull. | Either add reviewed bodyweight/band movements (bodyweight squat, split squat, reverse lunge, step-up, glute bridge, band row, band pulldown, pike push-up, inverted row where a sturdy anchor is declared) through the owner's catalog review, **or** remove "Bodyweight only" from Beta onboarding and disclose the home-gym pull gap. Add a guard test: every offered equipment profile × experience has ≥2 candidates per required slot. |
| D2 | P0 | **Production dose authority still cites the test fixture.** `PlanningReleaseApproval.ProvisionalApprovalReference = "MC-PLANNING-REVIEW-TEST"` (dated 2026-10-31) gates training prescriptions and automated progression. Policy `MC-BETA-WORKOUT-1` lists replacing it as an implementation-acceptance item. | Record `MC-BETA-WORKOUT-1` (owner decision 2026-11-05) as the active approval with its date and sections; keep the fixture in tests only. |
| D3 | P1 | **Policy and code disagree on injury adaptations.** The approved policy promises "native alerts for any injury adaptations"; the uncommitted change deletes the alert and the per-exercise gate because no screen could satisfy it (every catalog exercise carried review conditions). The removal is reasoned, but it is a safety-relevant behaviour change. | Owner amends the policy text (inline disclosure replaces the alert) and an independent reviewer approves the PR; keep `ExerciseEligibilityRuleTests` proving contraindication/restriction blocking still works. |
| D4 | P1 | **No usable technique support for beginners.** Exercise media is deferred; reviewed conditions say beginners should "demonstrate controlled setup", which the app cannot do. | Every beginner-eligible exercise gets localized EN/RU setup, 2–4 cues and one common mistake; guard test for presence. |
| D5 | P2 | One under-target session triggers a load decrease (`StrengthProgressionRule.ResolveSingleSetLoad`), and the rule's default target is 2 RIR while the reviewed templates prescribe 3 RIR. A single bad day regressing load can feel punishing to beginners. | Product decision: decrease only after two consecutive deficits; align the RIR source with the template. |
| D6 | P2 | Duplicate dose authorities: `WeeklyWorkoutPlanGeneratorRule` and `ComposedReadinessCycleDoseRule` have no production caller; `IWorkoutPlanner` is still DI-registered. The policy requires one final dose authority. | Characterize, prove equivalence or irrelevance, then retire (not in the Beta critical path). |
| D7 | P2 | The catalog review is owner self-review of an AI-assisted workbook (59 exercises "approved with conditions"). Acceptable under the 2026-11-05 decision; disclosed in-app as "not clinically reviewed". | Keep the disclosure; schedule an independent qualified review before public launch. |
| D8 | P1 | The policy acceptance table must run through production entry points with disposable PostgreSQL. Core tests cover most rows (short sleep, pain withhold/substitute, cycle extra-set gating, missing-RIR weeks), but no end-to-end table run is attached to a candidate. | Integration test per table row (Phase 3). |

### 3E. Architecture note

A dedicated architecture pass (`/improve-codebase-architecture`) was not part of this audit; it is owner-invoked. In Beta scope only
D6 and C2 matter. Other hotspots (1.8k-line `StrengthPlanGenerationViewModel`, `WorkoutSessionViewModel`,
`PostgresWorkoutPlanAndSessionStore`; presentation text inside infrastructure) are post-beta refactors.

## 4. Implementation plan

Lanes follow `AGENTS.md`: **A** product, **B** platform, **I** integration authority, **O** owner. Each step names its
gate. Durations are rough single-developer estimates, not commitments.

### Phase 0 — Land a reviewable candidate (I, ~1 day)

1. **0.1 Split the uncommitted delta into PRs** from `main` `4ee9b4f2` (done locally 2026-11-08; branch contents and
   review focus are listed in [DECISIONS.md](DECISIONS.md#phase-0-branches-awaiting-review)):
   - `product/beta-p0a-planning-flow` — acknowledgment-gate removal, default General strength, preview rationale,
     server-saved workout preferences, related tests and docs (D3 review required).
   - `platform/beta-p0b-catalog-review-seed` — `seed` applies the reviewed catalog.
   - `product/beta-p0c-auth-local-dev` — `LocalDevelopmentEmailChallengeDelivery`, OTP localized errors.
   - `product/beta-p0d-session-set-capture` — per-set session rows and EN/RU copy.
   - `platform/beta-p0e-beta-only-endpoint` — Staging removal in csproj/endpoint/workflow + contract test.
   - `integration/beta-p0f-record` — STATUS/ROADMAP/CONTROL/BETA_READINESS, this audit and the decision record.
   Gate: CI `required` green on each; code-owner review; nothing pushed without owner authorization.
2. **0.2 Owner decision record** (section 6) committed to `docs/release-audit/2026-11-08/DECISIONS.md`.

### Phase 1 — Beta environment foundation (B, owner-authorized AWS changes, ~3 days)

Serialize every mutation under the `mutation-beta` lock; record before/after identities in the evidence file.

1. **1.1 Environment identity (A2).** Set `ASPNETCORE_ENVIRONMENT=Beta` (confirm `appsettings.Beta.json` exists or add
   it), rename alarm/SNS/topic names through `observability.yaml` parameters, record "this stack is Beta" in `CONTROL.md`.
2. **1.2 Recovery (A3).** Retention 14 days, deletion protection on, manual snapshot, restore drill to a scratch
   instance, `dbcheck` on the restore, record RPO/RTO. Exit: restore log with timestamps and matching fingerprint.
3. **1.3 Schema adoption (A4).** Read-only `dbcheck` against Beta → rehearse `migrate`+`seed`+`dbcheck` on the restored
   scratch instance → add to `db-release.yml` a pre-step that creates `musclecuties-pre-migrate-<sha>` and waits for it →
   run `db-release` on Beta. Add to `deploy_release.py deploy` a check that the serving DB reports
   `schema_version ≥ required` (read via a tiny `dbcheck`-style endpoint or the release manifest) before alias flip.
   Exit: Beta `dbcheck` `compatible`, `schema_version=5`.
4. **1.4 Aliases (A1).** `publish-version` both functions; `create-alias beta`; HTTP API integration → alias ARN;
   `add-permission` for the alias; EventBridge target → worker alias; set `BETA_SERVER_FUNCTION/ALIAS`,
   `BETA_WORKER_FUNCTION/ALIAS`, `BETA_API_ENDPOINT`, `AWS_ACCOUNT_ID` vars. Make `BETA_API_ENDPOINT` required (A10).
5. **1.5 Secrets (A6).** Create one Secrets Manager secret per concern, grant the function role `GetSecretValue` on
   those ARNs only, load via a configuration provider at cold start, rotate all values, delete env values.
6. **1.6 Observability (A5, A7–A9).** Deploy `observability.yaml` for Beta; confirm SNS email subscription; add
   throttles, RDS CPU/storage/connection alarms; 30-day log retention; stage access logs and throttling; reserved
   concurrency. Exit: a synthetic alarm reaches the inbox.
7. **1.7 First real release.** `backend-release.yml deploy` → verified report → negative path (deliberate digest
   mismatch on a dry target) → `rollback` mode → redeploy. Exit: three reports attached to the evidence file.
8. **1.8 AASA (A11)** and **A12–A13** hardening.

### Phase 2 — App blockers (A + I, ~4 days)

1. **2.1 MAUI upgrade (B1).** I reserves `MuscleCuties.UI.csproj`; bump `MauiVersion` to a verified ≥10.0.90 and
   CommunityToolkit.Maui to its matching release; fix the comment; simulator build + CR01 reproduction sequence.
   Gate: owner-authorized UI build, crash sequence passes 20× rapid transitions.
2. **2.2 HealthKit off (B3).** `PlatformCompositionExtensions`: iOS → `DisabledHealthSyncService`; remove
   `com.apple.developer.healthkit` and `EnableAppleHealthEntitlements`; contract test asserting no HealthKit
   entitlement while dormant.
3. **2.3 Info.plist (B4, B6, B7).** Portrait only, drop `fetch`, add `ITSAppUsesNonExemptEncryption=false` after owner
   confirmation; build number from `github.run_number` in `mobile-release.yml` (B8).
4. **2.4 Privacy (B5).** Fill `NSPrivacyCollectedDataTypes`; draft App Privacy answers in the evidence file.
5. **2.5 Beginner style eligibility (C1).** Expose per-style eligibility from the existing planning rules to the
   preferences VM; picker rows show a localized reason and are disabled when ineligible; Core test for the eligibility
   projection, Contracts test for EN/RU keys. Reuse `CardSecondary` selection; no new style.
6. **2.6 Localize server text (C2, C3).** Add additive code fields (`TitleKey`, `SummaryKey`) to the affected DTOs,
   keep old strings for older clients, map keys on the client, rewrite the rest-day copy. Integration reservation for
   the DTO change.

### Phase 3 — Workout quality (A + O review, ~4 days)

1. **3.1 Coverage (D1).** Owner chooses: add reviewed movements (catalog `V021` entries with pattern, equipment,
   contraindications, timing and EN/RU guidance → owner review → seed) or restrict onboarding equipment. Add
   `CatalogCoverageGuardTests`: for each offered equipment profile × {Beginner, Returning, Established} every required
   template slot has ≥2 eligible candidates of its preferred pattern.
2. **3.2 Provenance (D2).** Replace the active approval with `MC-BETA-WORKOUT-1` / 2026-11-05 / both sections; tests
   assert production never cites a fixture reference.
3. **3.3 Policy reconciliation (D3).** Amend `WORKOUT_POLICY.md` decision text after owner approval; link the PR.
4. **3.4 Technique guidance (D4).** Guidance completeness test over beginner-eligible exercises; fill gaps EN/RU.
5. **3.5 Progression decision (D5).** Implement the owner's choice with boundary tests.
6. **3.6 Acceptance table (D8).** One integration test per row of the Beta policy table through the production
   proposal/accept/session/progression entry points on disposable PostgreSQL (`MC_*_POSTGRES` fixtures).

### Phase 4 — Device acceptance QA01 (O + A, ~2 days, on the exact archive)

Record device, iOS, build number, SHA, scenario and outcome.

1. Register → OTP → onboarding → quiz → first plan **with exercises** auto-applied (Full gym, Dumbbells, and the D1
   outcome for Bodyweight).
2. Replacement: Regenerate disclosure → Apply; decline keeps the active plan.
3. Strength session: per-set load/reps/RIR/technique, swap, optional set, finish; summary; next prescription changes
   only after two qualifying sessions (B3 device log).
4. Timed session with activity actuals.
5. Injury: Unanswered/Yes/No round trip; pain stop → substitute or withhold.
6. Nutrition: add meal, edit grouped meal (decimal comma in RU), breakdown for a past date, water ±, copy yesterday.
7. Cycle live vs offline (airplane mode) shows cached/stale state.
8. Account deletion and Sign in with Apple revocation with a real Apple ID.
9. Both appearances, largest Dynamic Type, RU, VoiceOver on Today/Train/session/plan preview, Reduce Motion.
10. Network loss mid-generation and mid-save: recovery copy, no duplicate plan.

### Phase 5 — TestFlight and operation (O)

1. Archive from the reviewed SHA with provenance (SDK, Xcode, MAUI, signing identity) recorded.
2. Internal group first (no Beta App Review), 3–5 days of use.
3. External group: Beta App Review needs a demo account, beta description, feedback email, privacy URL and the App
   Privacy answers from 2.4.
4. Tester note: what to test, known limits (no media, no fasting, health sync off, not clinically reviewed), how to
   send feedback/screenshots via TestFlight.
5. Operate: daily alarm/DLQ/`NeedsReview` check for two weeks; backend rollback = workflow `rollback` mode; app
   rollback = expire the build in App Store Connect.

## 5. Go / no-go checklist for external testers

- [ ] Candidate is a reviewed, merged SHA; CI `required` green (Phase 0).
- [ ] Beta served through `beta` aliases; deploy, negative path and rollback reports attached (1.4, 1.7).
- [ ] 14-day backups, deletion protection, restore drill with fingerprint match (1.2).
- [ ] Beta `dbcheck` compatible at `schema_version=5` (1.3).
- [ ] Alarms deliver to a confirmed subscriber (1.6); secrets rotated and out of env vars (1.5).
- [ ] MAUI ≥10.0.90; CR01 sequence passes on device (2.1).
- [ ] No HealthKit entitlement; privacy manifest and App Privacy answers match (2.2, 2.4).
- [ ] Every offered equipment profile yields squat/hinge/push/pull coverage, or the profile is not offered (3.1).
- [ ] Production approval reference is `MC-BETA-WORKOUT-1` (3.2).
- [ ] QA01 scenarios 1–10 pass in EN and RU on a physical iPhone (Phase 4).

## 6. Owner decisions needed

1. Is the existing `musclecuties-staging-*` stack the Beta environment (A2)?
2. Bodyweight profile: add reviewed movements, or drop it for Beta (D1)?
3. Amend the policy for inline injury disclosure instead of a native alert (D3)?
4. HealthKit out of the Beta binary (B3)?
5. Export-compliance answer: standard encryption only (B4)?
6. Load decrease after one or two under-target sessions (D5)?
7. Beginner access to High-volume/High-intensity: disabled with reason, or auto-downgrade with disclosure (C1)?

## 7. Gate status from this pass

| Gate | Status |
| --- | --- |
| G0 | PASS — HEAD `4ee9b4f2`, uncommitted delta preserved untouched, this file is the only write |
| G1 | PASS (source) — dependency direction unchanged; duplicate dose authorities noted (D6) |
| G2 | FAIL — D1 coverage gap, D2 fixture approval reference |
| G3 | BLOCKED — live adoption path unproven (A3, A4) |
| G4 | NOT RE-RUN — no endpoint calls beyond public site checks |
| G5 | NOT VERIFIED — source audit only; device acceptance never run |
| G6 | NOT RE-RUN |
| G7 | FAIL (target) — plaintext secrets in env, no log retention, public RDS endpoint |
| G8 | PARTIAL — Release builds 0/0; test execution blocked by sandbox |
| G9 | PASS — this record |
