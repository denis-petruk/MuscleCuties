# Implementation roadmap: platform first, incremental refactoring second

Proposed 2026-11-04. **Do not execute this plan as part of the audit assignment.** Rider is the latest implementation baseline; preserve its dirty docs. Notion has no project deployment role. References: [audit](00-current-state-audit.md), [target](01-target-architecture.md), [cutover](02-gitlab-to-github-migration.md), [test commands/scenarios](04-ci-cd-verification-matrix.md), [risk decisions](05-risk-register-and-open-questions.md).

Estimates below are focused engineering days for one experienced contributor, excluding owner waits, provisioning lead time, Apple review and incident repair. Ranges are planning estimates, not commitments. Roles: **A** product/mobile/backend orchestrator; **B** infrastructure/schema/CI orchestrator; **I** integration authority; **O** owner. A/B use isolated worktrees; one exclusive reservation owns each shared path. All external writes require concrete owner authorization under AGENTS. Tests and resources marked **anticipated** do not exist yet.

## Dependencies, milestones and safe parallelism

| Package | Depends on | Blocks / milestone | Owner |
| --- | --- | --- | --- |
| P00 | Planning review | All implementation work | I/O |
| P01 | P00 | P02–P13 source baseline | I, A/B read-only reviewers |
| P02 | P01 | Enforceable integration/governance | I/O |
| P03 scaffold / parity | P02; full parity also P06 | Scaffold enables P05/P06/P09 development; P03+P06 combined green gates before merge/promotion | B |
| P04 | P02 | P08/P10/P12 protected cloud identity | B/O |
| P05 | P03 | P10 immutable backend/worker promotion | B |
| P06 | P03 scaffold available in combined candidate | P03 full parity/P08/P10/P12 schema safety | B, I reserves migrations |
| P07 | P02/P00 inventory | P08 reproducible isolation | B |
| P08 | P04/P06/P07 | P10 actual isolated Staging | B/O |
| P09 | P03/P01 | P10/P11 client and contract compatibility | A, shared DTO reservation by I |
| P10 | P05/P06/P08/P09 | First GitHub-to-Staging qualification | I, A/B supply evidence |
| P11 | P09/P10 | P12 iOS/TestFlight readiness | A/O |
| P12 | P04–P11, O approval | First GitHub-to-Beta cutover and stability | I/O |
| P13 | P12 stability/retirement proof | GitLab retirement | B/I/O |
| R01 | P12 stable, P09 fixtures | R02/R03 clean transport boundary | A |
| R02 | R01 | R04/R05 client state seams | A |
| R03 | R01 | R04 domain/application seam | A |
| R04 | R02/R03 | R07 generator retirement | A |
| R05 | R02 | R07 storage/safety continuity | A |
| R06 | R03/P06 | R07 worker/progression equivalence | A, B schema reservation |
| R07 | R04/R05/R06 | Incremental refactor completion | A/I |

Critical path: baseline/minimal protected integration → history → full governance → CI scaffold plus migration tests jointly green → artifact/isolation prerequisites → Staging qualification → mobile acceptance → Beta approval/cutover → measured stable window. P04/P07 run alongside P03; P05/P06 are logically parallel but B must delegate disjoint paths or work sequentially. A runs P09 while B prepares platform work. P03 scaffold is a review/development checkpoint, not a passing or mergeable CI package: P03 and P06 use a stacked integration candidate and neither is merged with a failing required safety gate. Full P03 parity closes only after P06 equivalents execute successfully. This removes a dependency cycle without making a required test optional. R01+ may be designed in parallel but application refactors begin after stable Beta, except a separately reviewed release-blocking fix. Estimates must not imply a third autonomous production orchestrator.

**Before first GitHub-to-Staging deployment:** P00–P09 complete; approved Staging provisioning; real isolation/deny proofs; schema baseline and compatibility tests; enforced identity and approval; exact package/source provenance; rollback target/lease; synthetic end-to-end fixture ready. P10 produces post-deploy qualification and must pass before any Beta promotion. CloudFormation apply during P08 is separately approved infrastructure work, not implied by a build.

**Before first GitHub-to-Beta deployment:** P10/P11 pass; actual Beta data and installed-client inventory; tested restore/RPO/RTO; old/new client and worker/schema compatibility; authenticated success-path performance; media/privacy/auth/device acceptance; no critical/high unmitigated safety/data/security risks; one authority transfer; O approval binds exact artifacts/config/schema. Native approval or enforceable owner-controlled alternative must exist.

**Can wait until Beta is stable:** physical project extraction, broad client/state cleanup, legacy-generator retirement, general performance optimization beyond agreed release gates, cross-account migration, future Production, optional dashboards/cost tuning. Do not postpone known release-blocking loss-of-data, injury, authorization or isolation defects under “refactoring later.”

## Plan I — GitHub platform and safe release foundation

### P00 — Freeze evidence, preserve current work and classify data

- **Why/evidence/risk:** E01/E02/E11/E21: dirty owner docs, divergent history and unclassified shared data make automated resets or reseeds unsafe.
- **Depends on / blocks:** planning review / all packages.
- **Exact scope:** known Rider checkout, Git remotes/linked worktrees, current AGENTS/STATUS/ROADMAP, GitHub main protection/reviewer bootstrap, DB/media metadata and backup runbooks; anticipated `docs/architecture/github-transition/evidence/baseline/` and authority register.
- **Ownership/collisions:** I alone snapshots/records shared docs; A/B inventory read-only. O identifies data owner/installed builds; no app paths edited.
- **Steps:** refresh refs and hashes; snapshot owner-selected dirty files separately from bundles; verify bundle restore; inventory GitLab settings/variables by name, hooks, runners, registry, MR/issues/releases; record current deploy authority, valuable data classification, read-only config and access gaps. Before P01 can merge, O establishes minimal PR-only main protection/restricted direct writes and a real reviewer, with signed local candidate-check evidence as bootstrap until Actions exists. P02 extends this to complete automated protections; it does not defer initial protection. Reconcile stale restore runbook expectations before any drill.
- **Tests/manual:** C0; bundle verify/test clone; compare exact source hashes; O validates data classification and release owner; verify minimal main protection/reviewer settings before the first history PR merge.
- **Staging/Beta gate:** no deploy or DB write; future recovery clone needs target-specific O authorization.
- **Recovery:** retain Rider untouched; regenerate evidence from the captured baseline if incomplete.
- **Done/artifact:** signed baseline/ref/data/authority ledger with no secrets and explicit unknowns; archive recovery test output and minimal main protection/reviewer readback.
- **Estimate/confidence:** 1–3 days, medium; remote metadata access is unresolved.

### P01 — Reconcile history through a Rider-preserving integration PR

- **Why/evidence/risk:** E02/E03; using GitHub main as latest could remove current behavior.
- **Depends on / blocks:** P00 including minimal main protection/reviewer bootstrap / P02 and all source-based work.
- **Exact scope:** known GitHub main, Rider restore, two GitLab tips and `.gitignore`/AGENTS/STATUS conflicts; isolated anticipated `integration/github-history` branch and commit-disposition ledger.
- **Ownership/collisions:** I owns all merge resolution; A/B review assigned modules read-only. No workers independently merge or rewrite refs.
- **Steps:** follow cutover history procedure; preserve source refs, compare main-only/GitLab-only changes, resolve by latest Rider behavior and explicit intent, record each omitted/reimplemented change; include selected dirty docs only after review; prepare ordinary PR, no force push.
- **Tests/manual:** C0–C5 applicable source/contract suites plus representative history/hydration/measurement/planning fixtures; ancestry checks and full application-tree diff to Rider. Review known deletions manually.
- **Staging/Beta gate:** no cloud effects; pushing/merging requires session authorization. Missing history disposition blocks merge.
- **Recovery:** discard isolated candidate, retain original branches/bundles; do not reset source checkout.
- **Done/artifact:** reviewed ancestry/disposition report and canonical candidate SHA merged only after P00 protection/reviewer checks and signed local behavior evidence; no unprotected bootstrap merge.
- **Estimate/confidence:** 2–6 days, low/medium due 279/65 divergence.

### P02 — Adopt governance and enforce protected integration

- **Why/evidence/risk:** E03/E04; main is unprotected and no CODEOWNERS/workflow policy exists.
- **Depends on / blocks:** P01 / deploy-capable implementation.
- **Exact scope:** proposed root AGENTS plus canonical doc links; anticipated `.github/CODEOWNERS`, PR template, governance/path checks; GitHub main/release rules and environments.
- **Ownership/collisions:** I owns policy files/shared docs; O alone approves access/protection changes. A/B receive path reservations.
- **Steps:** reconcile the preserved current AGENTS with proposal; adopt ownership/approval matrix; resolve real reviewer identities and solo-owner limitations; configure protections and required checks; protect workflow/schema/IAM changes and release refs; record two worktrees/reservations.
- **Tests/manual:** C0/C10; intentional direct-push/check-bypass/self-review attempts using safe test refs; settings readback. Test protection remains enforceable if a workflow is modified.
- **Staging/Beta gate:** O authorizes GitHub settings changes; no deploy authority granted merely by policy adoption.
- **Recovery:** retain a reviewed settings export; correct rules under O control, never disable gates to unblock work.
- **Done/artifact:** governance diff, real owners, effective settings/readback and negative-test evidence; independent architecture review recorded or explicitly still open.
- **Estimate/confidence:** 1–3 days, medium.

### P03 — Port meaningful CI in shadow mode

- **Why/evidence/risk:** E04–E06: no Actions and obsolete missing tests prevent real parity.
- **Depends on / blocks:** P02 / P05/P06/P09.
- **Exact scope:** `.gitlab-ci.yml` as reference only; anticipated `.github/workflows/ci.yml`, `db-verify.yml`, reviewed composite/TRX helper; `global.json` and build pins exclusively reserved to B for this PR.
- **Ownership/collisions:** B owns CI/toolchain; A supplies test inventory without editing shared build files. P03 scaffold can be prepared/reviewed before P06, but final P03/P06 candidate merges only after both gate sets pass.
- **Steps:** port source/backend/contract/disposable fixtures from parity table; verify SDK/native dependencies; always-run required aggregate and dependency-aware routing; reject empty tests/skips, forks get no credentials; replace obsolete test-name assertions only in coordination with P06 equivalents. Keep cloud roles unavailable.
- **Tests/manual:** C1–C5 and C7 in clean Linux CI; inject missing test/no-executed-test/wrong-SDK failures; verify PR/main/manual source matrix and path-routing negatives. If P06 equivalents are pending, PG compatibility stays failing, not green.
- **Staging/Beta gate:** shadow only; no AWS or Apple release access.
- **Recovery:** revert workflow PR; GitLab remains only the explicitly recorded legacy authority, not an assumed working fallback.
- **Done/artifact:** scaffold checkpoint with explicit blocked schema gate; full package completion only when P06 replacement tests and all required CI pass on the combined candidate. Attach parity ledger/run links/TRX; never merge a red required gate.
- **Estimate/confidence:** 2–4 days, medium.

### P04 — Establish OIDC and an owner-controlled approval boundary

- **Why/evidence/risk:** E14/E22; only GitLab OIDC observed; dispatch/candidate-controlled approval is bypassable.
- **Depends on / blocks:** P02 / P08/P10/P12.
- **Exact scope:** anticipated `infra/cloudformation/identity.yaml`, protected environment settings/role trust, release-approval contract and tests; existing production_authorization.py retained until retirement decision.
- **Ownership/collisions:** B drafts policy; I security-reviews; O applies approved access changes. A/workers never receive roles/secret values.
- **Steps:** capture actual issuer/audience/subject semantics (including ID subject opt-in); separate artifact/Staging/Beta/migrator/IaC roles; restrict refs/environment; configure required reviewers or owner-operated short-lived alternative outside untrusted workflow control. Bind approval to candidate/schema/config/expiry/nonce; no long-lived admin keys.
- **Tests/manual:** static IAM review, trust simulation/negative invalid repo/ref/env/audience/fork/expired-approval/replay tests; verify approved path on an isolated harmless target. Do not use Beta writes as a test.
- **Staging/Beta gate:** explicit O IAM/settings authorization; Beta role unavailable until gate proven.
- **Recovery:** O revokes new role trust/session permissions; keep prior single authority until cutover.
- **Done/artifact:** reviewed trust/policy templates, observed claims without token, approval negative-test evidence and role/account matrix.
- **Estimate/confidence:** 2–5 days, medium; plan/reviewer/access constraints unresolved.

### P05 — Build immutable API/worker artifacts and port release guards

- **Why/evidence/risk:** E09/E12; source release is GitLab-coupled, worker unversioned and serving API unqualified.
- **Depends on / blocks:** P03 / P10.
- **Exact scope:** `tools/deployment/{release.sh,release-engine.py,validate-package.sh,upload-artifact.sh,verify-arm64-package.sh}`, deployment tests; anticipated backend-build/release reusable workflows and worker manifest.
- **Ownership/collisions:** B exclusively release tooling; I reserves manifest/policy contracts; A supplies app smoke expectations.
- **Steps:** use explicit provider-neutral build/release evidence; remove GitLab-only authorization coupling without spoofed CI vars; build API+worker once; validate ZIP/Skia/native startup; versioned artifact storage/digest; configure publish-version/alias revision logic, rollback and lock-loss supervision. Store environment config separately; never rebuild on Beta promotion.
- **Tests/manual:** C7/C8; tampered ZIP/object/tree/digest, stale alias revision, wrong target, no approval/lease, lost lock, worker mismatch; native Linux ARM64 liveness and DB-ready proof separately. Compare Staging/Beta input bytes.
- **Staging/Beta gate:** local tests only here; upload/deploy is P10/P12 and separately authorized.
- **Recovery:** retain old scripts disabled for new authority; revert tooling PR before use. Never rollback schema by restoring an arbitrary ZIP.
- **Done/artifact:** verified package/manifests/digests, reproducible test output and promotion contract; no deployment claimed.
- **Estimate/confidence:** 3–6 days, medium.

### P06 — Introduce a non-destructive schema baseline and upgrade contract

- **Why/evidence/risk:** E05/E08/E21; EnsureCreated and exact fingerprints are not a shared migration lifecycle; old runbook has 59-chain assumptions.
- **Depends on / blocks:** P03 scaffold reviewed/available in the same integration candidate, not full parity / P03 full parity, P08/P10/P12 and later schema refactors. P03 and P06 are jointly verified before either is merged; no mutual completion prerequisite.
- **Exact scope:** known PostgresSchemaBootstrap/DriftInspector/PostgresDbContext, Schema SQL, DbMaintenance Program/session lock, ModelSchemaContractTests; anticipated `Persistence/Migrations/`, baseline/adoption checks, migration ledger/checksum/tests and db-release.yml.
- **Ownership/collisions:** B alone allocates migration IDs; I reserves model/security/contracts; A proposes required structures without editing migration files.
- **Steps:** inventory actual schema in an approved protected read/recovery context; reconcile fresh model versus historical chain; create canonical EF baseline plus SQL security/catalog revisions; explicit adopt-existing operation verifies shape/security before recording history; guard EnsureCreated to disposable targets; safe diagnostic instead of “run dbinit”; support old/new compatibility intervals and drift; exclusive migration lease/session lock. Reconcile restore runbook and add additive upgrade/re-run/partial-failure fixtures.
- **Tests/manual:** C5 with real PG17; fresh-create and existing-model baseline adoption; old API on expanded DB; new API minimum schema; unsupported/unknown drift fails; all constraints/RLS/grants/catalog IDs/history unchanged; lock loss/concurrent migration fails safely; restore rehearsal in approved isolated protected clone. Never drop/recreate Beta.
- **Staging/Beta gate:** shared read/clone/write explicitly authorized; baseline adoption on Beta is a DB release with O approval, not a bookkeeping bypass.
- **Recovery:** transactional migration rollback where proven, otherwise reviewed additive forward repair; retain failed ledger/state for diagnosis, no automatic Down against tester data.
- **Done/artifact:** ordered/checksummed migration artifact, compatible-schema matrix and integrity/restore evidence; replacement safety tests exist and execute.
- **Estimate/confidence:** 4–10 days, low until exact deployed schema compared.

### P07 — Define reproducible environment infrastructure without replacement

- **Why/evidence/risk:** E10–E17; shared data, cross-media permissions, no deployed CloudFormation stack found.
- **Depends on / blocks:** P00/P02 / P08.
- **Exact scope:** anticipated `infra/cloudformation/{staging,beta,identity,observability}.yaml`, `infra/parameters/*.example.json`, infra.yml; known existing API/worker/RDS/media/queue identities.
- **Ownership/collisions:** B owns templates, I reviews contracts/resource ownership, O approves account/cost/security choices. No apply in this package.
- **Steps:** ADR native CloudFormation; current-resource inventory/import ownership; separate Staging functions/API/private RDS/roles/buckets/queues/logs; retain stateful Beta resources; deletion protection/backup policy/SG review; tags, budget alarms, drift, explicit secret references. Quantify actual current price/capacity deltas before O cost approval; no speculative dollars as facts.
- **Tests/manual:** template validation/lint/security checks, IAM least-privilege/denies, reviewed change set with no Beta DB replacement/deletion and no resource dual-management; check account-level blast radius and rollback limits.
- **Staging/Beta gate:** template PR review only; apply belongs to P08 or separately approved Beta hardening.
- **Recovery:** revert templates/change set before apply; import failure plan preserves existing resources unmanaged, never deletes/recreates them.
- **Done/artifact:** reviewed templates/change sets, resource ownership map, cost estimate assumptions and explicit state-retention policy.
- **Estimate/confidence:** 2–5 days, medium.

### P08 — Provision and prove isolated Staging

- **Why/evidence/risk:** E11/E14/E18; local/dev and present Staging share the prospective tester target.
- **Depends on / blocks:** P04/P06/P07 / P10.
- **Exact scope:** newly approved Staging AWS resources from P07, synthetic DB/catalog fixtures, runtime/migrator roles and observability; current Beta resources preserved in place.
- **Ownership/collisions:** B prepares apply and synthetic seed; I owns environment mutation lease; O authorizes concrete change set/spend. No A cloud credentials.
- **Steps:** apply reviewed new-resource change set; initialize through P06 migrations; seed synthetic users/history only; create network/storage/DB/roles/queues/log groups and alarm definitions; keep API routes unbound, function code/aliases undeployed and schedules disabled/untargeted until P10 has verified P05 artifacts. IaC must not sneak in placeholder or unreviewed application code; verify secrets/config identity and default-deny cross-access. Preserve existing endpoint/data; do not rename/move Beta merely to clean labels.
- **Tests/manual:** network/IAM/DB negative tests from Staging principal to Beta; least-privilege RLS/tenant checks; protected restore drill with measured RPO/RTO; media synthetic upload/expiry/version-delete tests only within new Staging; verify logs omit identifiers/health values.
- **Staging/Beta gate:** O-approved infrastructure apply/seed only on allowlisted Staging; any Beta hardening is its own approved change set.
- **Recovery:** stop new scheduler/traffic; preserve evidence; O approves teardown of only newly created synthetic resources with explicit IDs; never target current shared DB.
- **Done/artifact:** inert environment manifest, physical isolation/deny proof, storage/DB/restore evidence and synthetic fixture version. Function/alias/scheduler runtime readback and alarm firing are P10 gates after verified code deployment.
- **Estimate/confidence:** 2–5 days plus provisioning/recovery duration, medium.

### P09 — Make endpoint selection and compatibility fixtures explicit

- **Why/evidence/risk:** E18/E19; Debug defaults to shared API; JSON/Apply/injury behavior must not drift.
- **Depends on / blocks:** P01/P03 / P10/P11.
- **Exact scope:** `MauiProgram.cs:109,328–336`, client configuration adapter and csproj build properties, existing mobile/registration/server contracts, WorkoutPlanningFlowRuleTests; anticipated endpoint-identity/config tests and frozen older-client JSON fixtures. No physical contract extraction yet.
- **Ownership/collisions:** A owns mobile/client tests/config; I reserves shared DTO changes. B provides signed environment identifiers, not secrets.
- **Steps:** remove implicit shared Debug fallback; explicit Local/Staging/Beta selections with safe defaults/allowlist and cache/account partitioning; record current first-plan auto-Apply/replacement semantics; freeze serialization/nullable safety/exclusions/date/enum/error contracts; inventory installed TestFlight clients and propose support range. Fix any release-blocking behavior in separate small reviewed PRs.
- **Tests/manual:** C2–C4/C6 scenarios S01–S10; Release config cannot be overridden by stale debug Preferences; wrong-env target rejected; no cached Beta token reuse on Staging; owner/device checks after task-specific authorization.
- **Staging/Beta gate:** no live app writes here; synthetic Staging only when P08 and O authorization exist. Do not change first-plan acceptance policy silently.
- **Recovery:** revert config adapter while preserving persisted formats; invalidate environment-mismatched cache safely, never delete server records.
- **Done/artifact:** endpoint manifest per binary, wire fixture matrix and supported-version proposal, regression results.
- **Estimate/confidence:** 2–5 days, medium.

### P10 — Qualify first GitHub-to-Staging release

- **Why/evidence/risk:** E12/E16/E20; package checks and health probes alone miss planner/worker/media failures.
- **Depends on / blocks:** P05/P06/P08/P09 / P11/P12.
- **Exact scope:** anticipated backend-release.yml/db-release.yml and release-evidence storage; new Staging API/worker aliases, DB/schema, media, scheduler/DLQs/alarms; current latency scripts and synthetic E2E harness.
- **Ownership/collisions:** I owns candidate/lease/promotion; B executes only delegated Staging action, A runs approved product fixtures. O authorizes target release/load/recovery actions.
- **Steps:** preflight identity/schema/approval/lease; expand only if required, create/deploy the P08-reserved API/worker targets using exact verified ZIPs/config, publish/route qualified aliases, then bind API and enable the reviewed scheduler; verify alias targets and live alarm behavior; run authenticated lifecycle, older-client matrix, worker deferral/retry and media; measure successful planner latency with workload/sample/window, errors and cost; rehearse prior compatible alias rollback and failed migration recovery. Legacy environment remains untouched.
- **Tests/manual:** all Staging matrix rows; 2xx generated/accepted/logged flows plus negative auth/ownership; duplicate Apply/session finish; pending-age observability; no safety/history loss; no skipped fixture. Agree quantitative SLOs before qualification; historic failing p99 cannot be waived by health latency.
- **Staging/Beta gate:** O Staging authorization; failed/unmeasured required gates block Beta automatically.
- **Recovery:** halt shift, restore prior compatible alias/config under lease; schema forward fix if rollback unsafe; retain partial report.
- **Done/artifact:** signed source/package/config/schema release record, scenario output, attributed performance window, tested recovery and no cross-env writes.
- **Estimate/confidence:** 2–5 days plus agreed observation window, medium/low.

### P11 — Establish environment-specific iOS/TestFlight release workflow

- **Why/evidence/risk:** E18/E20/E22; mobile contract tests do not establish an installable accepted build.
- **Depends on / blocks:** P09/P10 / P12.
- **Exact scope:** `MuscleCuties.UI.csproj`, iOS entitlements/signing/config; anticipated mobile-release.yml; `scripts/verify-ui-flows.sh`, design gate register; Apple App Store Connect/testing metadata.
- **Ownership/collisions:** A owns app workflow/acceptance, B reviews secret/runner isolation, I reserves csproj/source/build number, O owns Apple identity and publication approval.
- **Steps:** pin macOS/Xcode/.NET/workloads; separate endpoint-specific binaries and approved bundle/signing identifiers; ephemeral keychain/profile handling; stamp source/API/schema compatibility and artifact digest; build/export/install; verify both appearances/accessibility/localization and all core flows; record actual previous installed versions. Build number changes only with accepted release candidate.
- **Tests/manual:** C6 and owner-authorized UI build/run; physical iPhone S01–S12, VoiceOver/large type/EN-RU/offline/cache/account deletion/Apple revocation; signatures/entitlements/endpoint check; support/privacy/AASA/App Privacy validation and capability review. Health integration remains disabled.
- **Staging/Beta gate:** explicit task-specific build/run permission; separate signing/upload/TestFlight publication authorization. Apple review/propagation may delay rollout.
- **Recovery:** stop distribution of candidate; backend retains old-client support; return to previous supported build where Apple permits, no assumption of instant installed-client downgrade.
- **Done/artifact:** signed IPA/digest/provenance and physical-device acceptance, Apple distribution record when authorized; failed/unrun checks explicit.
- **Estimate/confidence:** 3–7 days plus Apple wait, low/medium.

### P12 — Preserve existing data, transfer Beta authority and promote

- **Why/evidence/risk:** E11–E16/E20/E21; Beta data, rollback, approval and single authority cannot be inferred.
- **Depends on / blocks:** P04–P11 / P13 and post-stability refactors.
- **Exact scope:** classified current API/RDS/media/worker retained as Beta if valuable data exists; approved protection/IAM/config changes; Beta release manifest/aliases, existing legacy deploy role/job authority.
- **Ownership/collisions:** I alone owns cutover and environment lease; O approves candidate and authority change; B prepares exact reviewed changes; A verifies supported clients, no concurrent edits/deploy.
- **Steps:** record current schema/data/ownership/installed clients without exporting records; tested protected restore plus O-agreed RPO/RTO; review Beta protection/least-privilege and security status; freeze deployments, drain/revoke old deploy authority before enabling GitHub; approve exact ZIP/config/schema; additive schema only if needed; compatible API/worker aliases; synthetic authorized canaries separated from real testers; monitor defined thresholds/window and preserve history/media. Keep tester DB in place, no routine copy or reseed.
- **Tests/manual:** all Beta matrix gates, no loss/restriction changes in permitted canaries, old/current iOS coexistence; auth/media/reconciliation, fresh successful-planner latency/error metrics; effective approval/lease/denies and rollback proof. No unapproved load test on Beta.
- **Staging/Beta gate:** explicit O go/no-go bound to all identities; deployment freeze and recovery operator recorded; no critical/high unmitigated risk. Passing Staging is necessary but not approval.
- **Recovery:** freeze new deployments, previous compatible version/config under lease; data forward-fix or protected incident restore with O-approved loss/reconciliation. Never revive GitLab authority automatically.
- **Done/artifact:** measured cutover/approval/recovery record and owner-agreed stability window passed; actual Beta/TestFlight release status recorded without invented completion.
- **Estimate/confidence:** 2–5 days plus stability window, low until data/restore/device inventory resolved.

### P13 — Prove GitLab independence and retire its deployment access

- **Why/evidence/risk:** E09/E22; hidden scripts/registries/hooks could keep GitLab operationally required.
- **Depends on / blocks:** P12 stable / transition completion.
- **Exact scope:** GitLab pipelines/runners/hooks/variables/registries and retained MR/issues/releases archive; IAM gitlab.com deploy trust; README/badges/agent integrations; GitHub release/recovery workflows.
- **Ownership/collisions:** B inventories/removes approved dependencies; I validates evidence; O authorizes revocation/archival and retention. No repository deletion.
- **Steps:** run retirement checklist; release same proven artifact to approved validation target and rehearse GitHub-only recovery with GitLab access disabled; verify package restore, source refs and Apple/export paths; rotate/revoke only approved credentials; preserve audit links/exports; make GitLab read-only archive.
- **Tests/manual:** cold clone/build/test/release/recovery rehearsal without GitLab; runtime has no GitLab dependency; confirm no queued/manual deploy remains and revoked trust cannot assume target roles.
- **Staging/Beta gate:** O authorization for revocation/archival and any release; production remains unprovisioned.
- **Recovery:** retain GitHub last-good artifacts and owner-operated short-lived recovery; archive remains readable. Any exceptional reactivation is explicit incident approval, never automatic.
- **Done/artifact:** signed independence/retirement checklist, verified revocations, export inventory/retention and GitHub-only operation record.
- **Estimate/confidence:** 1–3 days, medium after metadata access resolved.

## Plan II — Architecture refactoring and quality improvements

Keep existing project paths initially. The following map is a target change sequence, not a claim that the extraction exists.

| Current directories/classes | Target module/seam | Slice |
| --- | --- | --- |
| UseCases feature DTOs, Infrastructure cloud request/response types, Server endpoint serialization | Anticipated `src/MuscleCuties.Contracts` wire-only project with golden JSON and no Core/ORM/MAUI dependencies | R01 |
| MauiProgram, UI ViewModels/navigation, Infrastructure ApiClient/Cloud and auth services | Explicit mobile composition/config, typed transport and UI state adapters; UI maps wire contracts | R02 |
| Core/Domain/Workout/Planning; UseCases Features/Workout/Planning; PostgreSql planning stores | Single server planning application boundary over pure Core policies and persistence ports | R03 |
| StrengthPlanGenerationViewModel, WorkoutPlanningFlowRule/Flow, proposal/lease/accept endpoints | Characterized preview/accept/navigation state transitions with explicit retry/replacement semantics | R04 |
| EncryptedLocalSnapshotCacheService, AccountSessionGate, cloud authority/session token adapters; hydration/measurements clients | Account+environment scoped storage/auth/offline adapter contracts; no silent cloud-to-local authority fallback | R05 |
| Injury contracts/rules, Postgres injury/reconciliation stores, WorkoutPendingRevisionJobHandler/worker | Shared domain safety and idempotent background application service; owned observed-set progression fixtures | R06 |
| Infrastructure Services/Workout/Planning/WorkoutPlanner versus CloudStrengthPlanProposalStoreV020 | Retire only unreachable/equivalent competing generator paths after caller and behavior proof | R07 |

### R01 — Extract wire contracts without changing payloads

- **Why/evidence/risk:** E07/E19; UI currently references application/domain assemblies and transport shapes are distributed.
- **Depends on / blocks:** P12 stable/P09 / R02/R03.
- **Exact scope:** known `src/MuscleCuties.UseCases/Features/*/*Contracts.cs`, Infrastructure cloud stores, Server mappings, test fixtures; anticipated Contracts project and solution references.
- **Ownership/collisions:** A implementation; I exclusively reserves solution/project graph/shared DTO files; B reviews schema impact, none intended.
- **Steps:** freeze old/current JSON; move one feature family per PR (registration/preferences, then workout/injury, then supporting domains); retain serializers/wire names/nullability/error meanings; add adapter mappers; update consumers and references together. Do not extract all families in one unreviewable PR.
- **Tests/manual:** C1–C4; golden round trips for supported old clients, HashSet/exclusion/null safety answers, dates/enums/unknown fields; architecture no outer dependencies in Contracts; smoke feature in synthetic Staging.
- **Staging/Beta gate:** normal reviewed compatible backend/mobile track gates; no Beta release permission from extraction.
- **Recovery:** revert feature PR or keep backward-compatible mapper; no stored-data rewrite.
- **Done/artifact:** one-family payload-equivalence report and dependency graph; repeat until approved scope complete.
- **Estimate/confidence:** 3–7 days total across several PRs, medium.

### R02 — Isolate mobile configuration, transport and presentation adapters

- **Why/evidence/risk:** E18/E07; composition/state coupling makes config failures indistinguishable from product behavior.
- **Depends on / blocks:** R01 / R04/R05.
- **Exact scope:** MauiProgram, Infrastructure backend/cloud clients, UI presentation mappers/navigation, ApiFailurePresenter; anticipate feature composition modules, not new business engines.
- **Ownership/collisions:** A owns one feature per PR; I reserves shared navigation/config/DI; no B schema edits.
- **Steps:** split composition registrations from UI startup; explicit cancellation/error/ownership contracts; map wire→UI models; extend status/request-reference taxonomy without leaking identifiers; retain route/binding/localization keys and encrypted-cache semantics.
- **Tests/manual:** C1/C2/C4 architecture/route/binding guards; auth expiry/429/409/schema fault/timeouts/offline scenarios; owner-authorized per-screen iOS check in EN/RU/large text.
- **Staging/Beta gate:** synthetic Staging and mobile/backend gates; owner publication approval if released.
- **Recovery:** revert feature adapter PR; keep source-compatible mapper until callers migrate, no cache format break without version reader.
- **Done/artifact:** affected screen parity/state matrix and dependency guard evidence.
- **Estimate/confidence:** 3–6 days across small PRs, medium.

### R03 — Separate planning application orchestration from pure domain policy

- **Why/evidence/risk:** E07/E19; large versioned planning/use-case/store surfaces make persistence and rules hard to reason about.
- **Depends on / blocks:** R01 / R04/R06.
- **Exact scope:** Core `Domain/Workout/Planning`, UseCases `Features/Workout/Planning`, Server WorkoutPlanningEndpointMappings, PostgreSql planning ports/stores.
- **Ownership/collisions:** A owns characterized rule/use-case slice; I reserves shared DTO/ports; B approves any requested schema change separately, no migrations hidden here.
- **Steps:** introduce explicit inputs/results and clock/catalog/safety ports; move only orchestration to application services and deterministic rules inward; preserve feasibility/frequency/duration/strength/injury/readiness behavior and source review requirements; remove no rule on the basis of naming alone.
- **Tests/manual:** C1/C3/C5; policy boundary matrix, empty/missing/stale/invalid inputs, deterministic results, golden proposal/accept outputs and transaction invariants; synthetic success-path performance comparison.
- **Staging/Beta gate:** full domain/API/DB gates for any behavior impact; separate clinical/product review for changed rules.
- **Recovery:** revert one slice, preserve old contract adapter; no wholesale second generator or data conversion.
- **Done/artifact:** equivalent outputs/invariants and inward dependency evidence for each extracted slice.
- **Estimate/confidence:** 4–8 days across several PRs, medium/low.

### R04 — Refactor preview/Apply/navigation as explicit state transitions

- **Why/evidence/risk:** E19; first-plan auto-accept, replacements, leases and uncertain retries are different valid paths.
- **Depends on / blocks:** R02/R03 / R07.
- **Exact scope:** StrengthPlanGenerationViewModel, WorkoutPlanningFlowRule/Flow, CloudStrengthPlanProposalStoreV020, planning acceptance/lease endpoints and persistence tests.
- **Ownership/collisions:** A owns UI+application feature; I reserves shared rule/DTO state; B has no migration reservation unless separately requested.
- **Steps:** characterize state transitions before decomposition; preserve first-plan conditions, active accepted plan during replacement, acknowledgment construction, lease fencing, cancellation/navigation and uncertain-accept retry; route failed API/schema/auth explicitly, no local generation fallback.
- **Tests/manual:** named WorkoutPlanningFlowRuleTests plus PlanningAcceptanceTransactionTests; duplicate/concurrent Apply, timeout-after-commit, stale lease/catalog, replaced preferences, back/reopen/sign-out; physical-device flow after authorization.
- **Staging/Beta gate:** G2/G3/G4/G5 all applicable, supported clients and owner mobile release approval.
- **Recovery:** revert transition slice; preserve proposal IDs/revision history and idempotent recovery, do not delete active plans.
- **Done/artifact:** transition table→tests/device evidence with no semantic diff or explicitly separately approved change.
- **Estimate/confidence:** 3–6 days, medium.

### R05 — Harden account/environment storage and auth/offline continuity

- **Why/evidence/risk:** E18/E19; cached state, token refresh and account/environment switching can leak or overwrite state.
- **Depends on / blocks:** R02 / R07.
- **Exact scope:** `EncryptedLocalSnapshotCacheService`, `AccountSessionGate`, SecureStorage token/key stores, cloud authority, hydration/measurement/nutrition adapters and associated tests.
- **Ownership/collisions:** A one storage/auth seam per PR; I reserves cache wire format and account lifecycle; B supplies environment identity only.
- **Steps:** model offline read-only/pending states and cancellation; version account+env cache envelopes; prevent refresh/sign-out races; preserve history/hydration/measurements and privacy-safe deletion; characterize nutrition fallback separately and retain reviewed offline behavior until product policy is decided. No new nutrition semantics inferred from infrastructure work.
- **Tests/manual:** cache cross-account/env negative cases, interrupted writes/corruption/key loss, expired token/refresh cancellation, sign-out/delete races, offline replay idempotency and stale safety data; authorized device encrypted-storage/offline checks.
- **Staging/Beta gate:** privacy/security/API/mobile gates; no tester record cleanup; unsupported conflict semantics require separate owner product decision.
- **Recovery:** keep backward-compatible cache reader or clear only explicitly disposable local cache safely; never use server-data deletion as rollback.
- **Done/artifact:** concurrency/privacy fixtures and retained user-data continuity evidence.
- **Estimate/confidence:** 3–7 days, low/medium.

### R06 — Consolidate injury/reconciliation/progression boundaries

- **Why/evidence/risk:** E11/E16/E19/E20; worker deferrals and safety answers need consistent persistence/observability, progression must use observed completion.
- **Depends on / blocks:** R03/P06 / R07.
- **Exact scope:** injury Core/UseCases contracts, PostgreSql injury/reconciliation stores, `WorkoutPendingRevisionJobHandler`, `services/MuscleCuties.WorkoutReconciliation`, injury/acceptance/progression integration tests.
- **Ownership/collisions:** A owns application/domain behavior; B exclusively owns any migration ID/security SQL or worker infrastructure change; I coordinates event/schema contract reservation.
- **Steps:** characterize tri-state/stale answers, episode lineage/substitution/omission/restoration and tenant isolation; shared idempotent reconciliation service; retries/cursor/failure/NeedsReview explicit; observed-set progression fixtures preserve completed/in-progress sessions; no clinical rules invented. Add queue/pending-age metrics through a separate B-owned PR if required.
- **Tests/manual:** C1/C3/C5; injury UI→HTTP→PG round trip, episode/RLS transaction tests, duplicate worker invocation/crash replay/technical failure/deferral, completed-set versus planned-set progression; approved Staging synthetic scheduled canary.
- **Staging/Beta gate:** compatible worker/API/schema pair and all injury safety gates; O authorizes worker/DB releases.
- **Recovery:** revert compatible worker/application slice or pause new work under O incident plan; retain pending cursor/lineage/history, never clear pending flags to suppress alarms.
- **Done/artifact:** safety/progression equivalence report, worker lag/NeedsReview operational evidence and unchanged history checks.
- **Estimate/confidence:** 4–9 days, low/medium pending reviewed catalog evidence.

### R07 — Retire proven duplicate paths and complete quality evidence

- **Why/evidence/risk:** E18; local WorkoutPlanner and cloud proposal registrations are not enough proof of duplicate behavior or dead code.
- **Depends on / blocks:** R04/R05/R06 / refactor completion.
- **Exact scope:** Infrastructure `Services/Workout/Planning/WorkoutPlanner`/IWorkoutPlanner, MauiProgram registrations, real callers and source guards; no speculative module deletion.
- **Ownership/collisions:** A investigates/removes one proven path; I reviews removal and project references; B confirms no runtime/deploy contract dependency.
- **Steps:** build caller/XAML/DI inventory; compare synthetic outputs/state changes and offline semantics; choose authoritative path based on reviewed product contract; remove unreachable/equivalent adapters only, retain intentional calculators; optimize measured hot paths with bounded work/cancellation; refresh docs/checkpoints and retire obsolete plans by links/reason, not deletion of evidence.
- **Tests/manual:** full affected regression/contract/PG suites, positive planner E2E, source guard for no competing authority/fallback, measured p95/p99 under fixed workload and owner device parity. Deleted paths must have no runtime binding/DI/route callers.
- **Staging/Beta gate:** ordinary protected releases; no microservice extraction or unapproved rule change.
- **Recovery:** revert one removal PR while persisted contracts remain compatible; no resurrection of unsupported old data formats.
- **Done/artifact:** caller/equivalence/removal ledger, current quality measurements and no unresolved issue falsely marked closed.
- **Estimate/confidence:** 2–5 days, low until reachability/equivalence established.

## Ready-to-use orchestrator handoffs (future sessions only)

These prompts define bounded work after the owner starts implementation. They do not launch agents, schedule continuations or grant cloud access.

### A — Product/mobile/backend orchestrator

```text
Work on one explicitly assigned P09/P11/R01–R07 package in an isolated product/<ID>-<topic> worktree from the integration authority's pinned SHA. Read AGENTS, architecture/product/safety/design/verification docs and the full assigned roadmap package. Verify git status and the reservation ledger first. Treat Rider-derived characterized behavior as baseline; preserve first-plan auto-accept and replacement Apply, tri-state injury answers, history/hydration/measurements and dormant health integration.
Own only the package's reserved product files and tests. Do not edit workflows/IaC/migrations/shared project graph or DTOs without exclusive integration reservation. Request schema additions through B's versioned contract; never create a second generator. No cloud/shared DB/remote write or mobile build/run/publication without applicable owner authorization. Implement one reviewable slice, run meaningful permitted checks, attach actual evidence and open gates, update scoped handoff, then hand to I. Do not merge/promote yourself or claim device/clinical acceptance from source tests.
```

### B — Platform/schema/CI orchestrator

```text
Work on one assigned P03–P08/P13 platform/<ID>-<topic> worktree from I's pinned SHA. Read AGENTS, audit, target, migration plan, package, verification matrix and exact current schema/environment runbooks. Verify baseline and exclusive file/migration reservations. Preserve the existing environment and all tester data; new Staging must be independently isolated. Notion is not an operational dependency.
Own only reserved CI/release/IaC/schema tooling and tests. Do not change domain/mobile behavior. Port fail-closed guarantees, prove obsolete migration assertions replaced by meaningful tests, keep immutable API/worker artifacts and independent DB/mobile/backend gates. Never spoof GitLab variables as a security boundary, rely on workflow_dispatch alone, or run dbinit to clear drift. Prepare reviewable templates/migrations/change sets and negative tests. No apply/deploy/shared DB/secrets change without concrete owner authorization and I's mutation lease. Hand evidence/rollback/open gates to I; do not cut over authority yourself.
```

### I — Integration/release authority

```text
Start by reading AGENTS, roadmap dependency table, risk decisions, both orchestrator handoffs and current source/target evidence. Own shared contracts/project graph/AGENTS/status/roadmap, migration reservations and candidate manifests. Preserve Rider and both remote histories; integrate through reviewed PRs, never force-push. Assign A and B disjoint paths/worktrees from a pinned candidate, serialize shared edits and all environment mutations with a real cross-tool lease.
Validate exact source/tree/package/config/schema/client compatibility and current test results. Resolve conflicts from user decisions and actual code; retain uncertainty. Prepare one concrete Staging/Beta/IaC action with recovery/evidence for owner approval as applicable; do not infer approval from a plan or green CI. Keep only one deploy authority per environment and workers without release access. Stop the planning assignment before implementation. During an authorized implementation session, advance only packages whose prerequisites pass and preserve unmet gates rather than marking them complete.
```

## Checkpoint contract

Each scoped handoff records: task/package/status; authorization scope; base/head/tree; owned/excluded/shared reserved paths and migration IDs; decisions/reasons/source links; edits/provenance; exact commands/outcomes/artifact hashes; target environment/config/schema if relevant; blockers; next safe step; reservation release/transfer; rollback limits. Checkpoint before the platform limit or 200k tokens, whichever is lower, leaving space to verify the handoff. Use only supported session continuation. Repository/GitHub artifacts remain sufficient if Notion or any personal tracker is offline.
