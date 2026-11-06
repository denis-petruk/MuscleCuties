# Risks, decisions and unresolved access

As of 2026-11-04. Evidence IDs refer to [the audit](00-current-state-audit.md). Severity: Critical (data loss, unauthorized exposure/deploy or safety bypass), High (release/compatibility failure), Medium (cost/maintainability/operational gap). Likelihood is a planning assessment, not a measured probability. No risk below was remediated in this docs-only task.

| ID | Severity / likelihood | State and evidence | Mitigation / owner / closure proof |
| --- | --- | --- | --- |
| K01 | Critical / high | Confirmed 279/65 Rider/GitHub divergence, E01–E03; latest source is local | P00/P01 I preserves dirty overlay/history, complete commit disposition, ordinary PR and behavior comparison. Close only with reviewed canonical SHA and backups. |
| K02 | Critical / medium | Tester data classification unknown; current API/worker share one DB, E11 | Preserve existing resources, classify without resetting, new isolated Staging P08. O approves Beta identity/data retention; closure isolation/deny proof and retained tester continuity. |
| K03 | Critical / medium | Backup retention 1 day, deletion protection false, no proven restore, E13/E21 | P06/P08/P12 approved protection and isolated protected restore, real RPO/RTO and integrity. Snapshot available ≠ recoverability. No overwrite of Beta as a drill. |
| K04 | Critical / high | Model-first EnsureCreated and metadata restamping, strict fingerprint/no migration chain, E08 | P06 explicit existing-baseline adoption/version/checksum/compatibility contract, fresh+upgrade+old-new tests; prohibit dbinit as drift repair. Close with exact target schema proof, not historical 59 migration count. |
| K05 | High / certain if current CI runs | Three required schema tests absent, E05 | P03/P06 implement actual equivalent baseline/upgrade/drift tests and nonempty TRX assertions before parity; do not remove assertions without coverage. |
| K06 | Critical / medium | Unprotected GitHub main/no rulesets and no Actions, E03/E04 | P02/P04 protected branch/reviews/workflows/owner-controlled deploy identity; safe negative bypass tests and settings readback. |
| K07 | Critical / medium | Cross-write to staging/prod media buckets from API role, E14 | Map actual object ownership without reading photos; restrict runtime/deploy roles and bucket policies in approved P07/P08/P12 changes; prove Staging denies Beta access. Names alone do not classify data. |
| K08 | High / high | Unqualified API/worker targets/no aliases; version 13 now observed versus docs version 11, E12 | P05 publish/route versioned aliases with revision checks, immutable ZIP/config/source linkage and single authority; P10 rollback drill. Revalidate just before execution because live state changed independently. |
| K09 | Critical / medium | GitLab/manual authority and settings unknown; two pipelines could race, E09/E22 | P00 authority register, cross-tool lease; disable/drain/revoke legacy before enabling GitHub Beta P12. No automatic GitLab fallback. |
| K10 | High / high | Historic planner p99 failure; local tests short-circuit 409/400, E20 | P10 authenticated success workload with route attribution, approved SLO/window and actual old/current clients; no health-probe substitution. |
| K11 | High / medium | No CloudWatch alarms in us-east-2; EventBridge delivery target no DLQ, Lambda async DLQ present/empty, E16 | P08 delivery-vs-execution failure handling, lag/NeedsReview/DLQ/latency/error alarms and incident drill; empty queue does not close correctness. |
| K12 | Critical / medium | Debug defaults to shared endpoint; unknown installed-client support and auth/cache partition, E18/E19 | P09 endpoint allowlist/per-env cache and token isolation, wire golden fixtures, actual installed build inventory; P11 physical acceptance. |
| K13 | High / medium | API username mc_admin observed; actual privileges and full security posture unknown, E11/E14 | Protected DB role/ownership/RLS/grants read review in P06; least-privilege runtime/migrator/discovery separation, approved rotation only if needed. Do not infer superuser from username or declare least privilege from source. |
| K14 | High / medium | Public RDS reachable from app SG/self and operator /32, not world-open; API config holds secret material in environment, E11/E13 | P07/P12 private access plan with operational access/recovery, managed secret references/resolution, scoped IAM and secret-leak review. Network/secrets changes separately approved; no panic rotation from this audit. |
| K15 | High / medium | iOS signing/TestFlight/device/Apple revocation/media acceptance unverified, E20/E22 | P11 actual endpoint/signed IPA/device/Apple/App Privacy evidence and supported-version policy; no broad claim from compile/startup. |
| K16 | Medium / high | No current IaC project/stack found; account/runner/billing capabilities unknown, E06/E17/E22 | P07 native CloudFormation/new Staging first, no duplicate management; pin toolchain/native ARM64/macOS and estimate cost using current owner region/account evidence; budget approval. |
| K17 | High / medium | Local/cloud planner registrations coexist; equivalence not established, E18 | R03–R07 characterize callers and outputs; no removal or new generator until proof; preserve safe first-plan exception/replacement semantics. |
| K18 | Medium / high | Parallel auditors hit account limits before final reports; independent final review initially unavailable | Coordinator verified material source/live claims directly. A later independent reviewer identified three sequencing issues in 00–03 and AGENTS, corrected in this proposal; final review scope/results are recorded in delivery verification. Runtime safety/release evidence remains open. |

## Decisions recorded from this request

| Decision | Basis / reason | Effect |
| --- | --- | --- |
| GitHub is sole future source/PR/automation/release home | Explicit master brief | GitLab only temporary read-only archive after proof; never permanent deployment fallback |
| Rider checkout is the latest code | Explicit owner correction during this task | Remote history reconciliation preserves Rider behavior and uncommitted docs |
| Current task is audit/docs/governance/implementation planning only | Explicit brief | No application refactor, remote writes, deployment or migrations now |
| Two development orchestrators, one integration authority | Explicit brief | Separate worktrees/paths/reservations; workers have no release access |
| Stable Beta and separate Staging; protect tester data | Explicit brief | Prefer retain existing environment as Beta if valuable data/installed clients depend on it; provision independent Staging |
| Four technical boundaries; three release tracks; protected IaC workflow | Explicit brief | No speculative microservices or rewrite; independent gates and compatible promotion |
| Notion is personal notes/habits, outside deployment | Explicit owner correction during this task | No CI, release, approval, orchestration-state or recovery dependency; all operational artifacts in repo/GitHub/secure release storage |
| Preserve existing valid project rules | Current working-tree AGENTS/canonical docs plus brief | iOS-only, gated UI runs, privacy/synthetic fixtures, dormant health, current design/product rules retained |

## Architecture recommendations awaiting implementation review

Native CloudFormation for new resources; same-account but separate physical Staging resources initially; retain existing data plane if valuable; separate future Production account design only; versioned EF migration lifecycle with verified existing baseline and supplemental/security SQL; immutable API/worker ZIP promotion, separately configured versions/aliases; additive wire/schema compatibility and characterized legacy-path retirement. These recommendations are documented choices for review, not owner authorization to apply them.

## Owner decisions strictly necessary before dependent implementation

These are deferred concrete decisions, not a questionnaire blocking this planning task. Evidence collection continues without asking the owner to restate source facts.

| ID / needed by | Decision | Investigation first / safe default until resolved |
| --- | --- | --- |
| D01 / P00,P12 | Which current data/installed builds are valuable and which resources constitute Beta? Who owns release/recovery? | Inventory existing clients/resource metadata with protected access. Treat all present shared data/media as valuable; do not reset or copy into ordinary Staging. |
| D02 / P00 bootstrap,P02,P04 | Named reviewer/integration/release operators and enforceable approval method, including solo-owner constraints | Read GitHub environment/plan/access settings, verify native reviewer controls on current public repo. If independent human is unavailable, no false peer approval; owner-operated protected release plus recorded external review for security changes. |
| D03 / P07,P08 | Separate-account versus same-account physical isolation and approved monthly budget/capacity differences | Prepare resource-specific current-price estimate and actual maintenance needs. Recommended same-account separate resources, new private RDS; logical shared-instance exception only explicitly accepted. No spend until approved. |
| D04 / P06,P08,P12 | Acceptable RPO/RTO, backup retention, recovery/evidence retention and protected clone access | Produce drill result/restore timing and data-classification constraints; no guessed target or claim of completed restore. |
| D05 / P09,P11,P12 | Actual supported TestFlight/client range, minimum-version/retirement policy and endpoint/bundle/signing scheme | Inventory Apple builds/installed usage and golden payloads; keep old supported clients working until explicit retirement decision. |
| D06 / P10,P12 | Success-path performance/error targets, test load/window and Beta stability window | Measure isolated representative successful flows and historic tail; show workload/sample data before approval. Existing 1000ms diagnostic is not an automatic product SLO. |
| D07 / P12,P13 | Concrete cutover/freeze/maintenance window if needed, candidate approval and archival/revocation retention | Complete Staging proof first; no migration/cutover scheduled automatically, one active authority per target. |
| D08 / later R05/R06 only | Offline nutrition conflict semantics or catalog and safety-rule changes if the refactor needs them | Preserve characterized behavior and use the [approved Beta workout policy](../../release-audit/2026-11-05/WORKOUT_POLICY.md) for Beta. Record gaps and keep unrelated features outside the platform migration. |

## Access and verification limits

AWS MCP read access worked, STS identity confirmed and relevant Lambda/RDS plus selected runtime role/storage/network/event/backup configs were inspected. No database sessions/user rows/photo objects/secret values were retrieved; schema contents, real grants and restore reliability remain unverified. No CloudWatch health-data logs were read. All 17 enabled regions were scanned for Lambda/RDS; API/event/alarms/CloudFormation detail is bounded to us-east-2. Other accounts, disabled regions, organization SCPs and all IAM principals were not audited.

GitHub connector read public repository/branch/ruleset/release/PR metadata; settings/secrets/Apple/plan and full webhook/agent permissions need owner-authorized metadata access. Local gh is not installed; this did not prevent supported connector reads. GitLab SSH ref reads worked; unauthenticated project/MR/issue/release API returned 404, so private/unauthorized metadata remains UNKNOWN. No claim that there are no GitLab issues/runners/variables/hooks is justified.

Follow-up acceptance requires source and target revalidation at execution time; live version 13 already superseded dated version-11 docs. Never overwrite historical reports to pretend they described the current runtime.
