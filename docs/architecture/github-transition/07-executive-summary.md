# MuscleCuties transition: decision summary

**Status — 2026-11-04:** planning ready for review, implementation unstarted. Latest code is Rider (`restore`, `fe350c4f`) plus dirty docs. GitHub main diverges 279/65 commits and is unprotected. CI requires three absent schema tests; schema is EnsureCreated/model-first without migrations. API/worker share one DB, retention is one day, deletion protection off, API role writes both media buckets. Serving targets are unqualified; alarms, tested recovery, device/old-client compatibility and authenticated performance remain open.

**Target:** C# modular monorepo; four boundaries (iOS, API/workers, PostgreSQL, IaC); three gated application releases plus protected IaC. Preserve valuable current data as Beta; create separate Staging resources, initially same-account with default-deny access. Native CloudFormation/new Staging first; no Beta replacement. Production remains design-only. Notion is personal notes/habits, never a deployment dependency.

**Ownership:** A product/mobile/backend; B platform/schema/CI; separate worktrees. One integration authority owns shared contracts/migrations/candidates; owner approves external actions. Workers have no release credentials. Expand → compatible API/worker → mobile → deferred contract. Promote identical verified ZIPs with environment-specific config/aliases; preserve current Apply behavior.

## First ten PR-sized packages, in review order

| Order | Package / dependency | Acceptance needed |
| --- | --- | --- |
| 1 | P00 baseline/data/authority ledger | Rider hashes/bundle recovery, dirty docs/data/authority classified; minimal main protection/reviewer before first merge |
| 2 | P01 history reconciliation / P00 | Ordinary ancestry-preserving PR, every remote-only commit dispositioned, Rider behavior retained |
| 3 | P02 governance/protections / P01 | Root/docs adopted, real owners, required checks/branch/review settings and bypass negatives |
| 4 | P03 shadow Actions / P02 | CI scaffold first; stacked P03/P06 merges only when replacement schema and all required tests pass |
| 5 | P04 OIDC/approval foundation / P02 | Exact issuer/repo/environment trust, separate roles and enforceable owner gate outside untrusted PR control |
| 6 | P05 immutable API/worker packaging / P03 | Same-byte digest/provenance, native ARM64 checks, provider-neutral guards/lease/rollback tests |
| 7 | P06 schema baseline/upgrade contract / P03 | Existing data preserved; real PG baseline/expand/old-new/RLS/lock/recovery tests, obsolete CI checks replaced |
| 8 | P07 IaC/isolation definitions / P02,P00 | Reviewed no-replacement change set, retained Beta, independent Staging and cost/denial plan |
| 9 | P08 isolated Staging / P04,P06,P07 | Owner-approved inert resources/synthetic DB, cross-env denies and tested restore; code/schedules activated in P10 |
| 10 | P09 endpoint/compatibility fixtures / P01,P03 | No implicit shared Debug endpoint, account/env cache isolation and supported-client golden payloads |

P04/P07 parallel P03; A prepares P09 alongside B. **P10** qualifies Staging; **P11** proves iOS/device/TestFlight readiness; **P12** is separately approved Beta cutover; **P13** proves GitLab-independent release/recovery before retirement. R01–R07 incrementally refactor contracts, state, planning, storage/safety and proven duplicates after stable Beta.

**No-go:** unclassified data, untested recovery, unsafe schema evolution, missing tests, cross-environment writes, bypassable/double authority, incompatible clients, injury/history/auth defects or failed/unmeasured successful-planner performance.

**Owner decisions:** Beta data/builds and reviewers/operators; isolation budget; RPO/RTO/retention; supported clients; performance/stability targets; concrete IAM/apply/cutover authorization after Staging/device proof. GitLab/Apple/settings access remains incomplete. Independent review of AGENTS/00–07 identified sequencing corrections now applied; see delivery verification.

**Begin only after** patch review, preserved Rider baseline, P00 reviewer/protection bootstrap and explicit implementation scope. First milestone: GitHub shadow CI on reconciled Rider code. Beta requires later approval/evidence. [Full roadmap and orchestrator prompts](03-implementation-roadmap.md), [verification](04-ci-cd-verification-matrix.md), [risks/decisions](05-risk-register-and-open-questions.md), [governance delta](06-agents-governance-change-log.md).
