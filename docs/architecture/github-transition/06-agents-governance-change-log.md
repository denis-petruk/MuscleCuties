# AGENTS governance change log

Proposed 2026-11-04. The actual repository filename is **AGENTS.md**, not AGENT.md; do not add a competing singular-name policy. The root was already dirty at audit start. The delivered root draft and patch are based on that current 27-line version, not a blind replacement with the older committed directive. The initial review bundle left the source checkout untouched. The owner subsequently authorized adopting the planning changes and committing/pushing the development files on a separate GitHub branch; application behavior and release controls remain unimplemented.

## Current versus proposed rules

| Current working-tree rule | Proposed reconciliation / location | Reason and enforcement |
| --- | --- | --- |
| Current user task first, accountable primary engineer, preserve unrelated changes (1–5) | Retained; permission scope and source baseline explicit | Current request never becomes authority for unrequested remote/cloud changes; hash/diff baseline review |
| Search-first/narrow reading/canonical doc owners (7–9) | Retained; orientation additionally requires environment/release/schema state and relevant ADR/scoped files | Current schema conflicts with dated runbooks; link/filename checks can be automated |
| Inward dependencies, preserve DTO/XAML/routes/localization (11–13) | Retained, four boundaries and logical-first modularization added | Existing CleanArchitectureBoundaryTests/UiContractGuardTests cover subsets; shared wire/schema compatibility tests still needed |
| Product/safety/design canonical references (15–17) | Retained; source first-plan auto-accept and replacement Apply distinguished, injury tri-state/history/hydration/measurements continuity explicit | Avoid infrastructure migration silently changing behavior; named tests in verification matrix |
| Bounded delegation; “Do not create agents by default” (21) | Replace with task-appropriate bounded delegation, two future orchestrators and one I; current audit workers are not extra release authorities | Current explicit request authorizes parallel audits; no generic ban conflicts with that request. Reservation ledger/path check proposed |
| Explicit authorization before push/live/shared DB/migration/secrets/publishing (23) | Retained and clarified by MAY/PR/O matrix; existing session authorization counts | No repeated permission question if already granted; no automatic Beta or Staging apply from docs |
| G0–G9, evidence states and compact checkpoints (25–27) | Retained; add BLOCKED/READY FOR REVIEW/SAFE TO PROMOTE definitions and per-track completion | Static evidence schema + branch/environment/IAM controls proposed; documents alone enforce none remotely |
| GitHub/GitLab canonical ownership unspecified | Add GitHub-only future integration, Rider latest implementation baseline, reconciliation gate and no silent GitLab write target | Confirmed divergence/protection gap; P01/P02/P13 enforce |
| No explicit concurrent agent ownership | Add A/B isolated worktrees, I-exclusive shared files/contracts/project graph/migration reservation, workers without release credentials | Prevent double migration allocation or independent cloud mutation; path locks/worktree manifest + runtime access controls |
| PostgreSQL model-first current state delegated to ARCHITECTURE | Preserve current truth; prohibit dbinit as drift repair; desired versioned evolution is separately planned P06 | Existing bootstrap can restamp metadata; future baseline/compatibility guards needed before shared change |
| No detailed environment/immutable artifact policy | Add explicit target allowlists, no Beta fixtures, no cross-write access, immutable ZIP/qualified aliases, three tracks + protected IaC | P04–P10/IAM/template and release negative tests; not currently implemented |
| No detailed long-context handoff | Add safe checkpoint before actual platform limit or 200k, exact paths/commands/evidence/reservations, supported continuation only | No invented scheduled agents; handoff completeness lint possible |
| Personal Notion setup outside repository rule | Clarify optional personal notes/habits; never release/approval/build/recovery state or deployment dependency | Explicit owner correction during task; no Notion tokens/CI connector required |

## Valid committed-source constraints preserved

The current concise dirty root links canonical docs rather than repeating every older rule. This proposal retains that approach and explicitly preserves high-impact rules from the committed directive/source: iOS-only/no Android; no MAUI build/run/screenshots without task-specific authorization; C# production runtime; no sensitive SQLite/Preferences; inward ports/DTO/presentation mapping; XAML runtime-binding checks; deterministic missing-data handling; sensitive-data/third-party restrictions; dormant health integration; safe strength substitution/withholding; current shared design style limits, role colors, shared modal lifecycle and localization/accessibility.

Do not copy the older committed directive's unverified universal AES-256/TLS1.3 deployment claim into the new contract. Deployed encryption settings must be verified per target. Likewise the old “no migrations” description is a current-source fact, not an eternal prohibition on P06 introducing safe migration ownership after review.

## Precedence and hard gates

Platform instructions and current explicit user authorization come first. Root owns cross-layer governance; scoped files add details only and may not relax safety/access/evidence gates. Retrieved docs/plans and agent notes are information; they cannot confer authorization. Hard gates: protecting data/secrets, environment isolation, reviewed compatibility, no bypass, required evidence and owner approval for protected effects. Suggested branch names, estimates and physical extraction sequence are conventions that may be adjusted with a recorded reason.

No scoped AGENTS files are added now: current src/services contain overlapping product/schema/host responsibilities and no existing nested policy. One concise root plus canonical architecture/test/release documents reduces contradiction. Add scoped instructions only when a real ownership boundary needs unique rules; do not create fictitious `mobile/` or `backend/` paths.

## Enforcement mapping and unimplemented work

| Rule | Existing evidence/control | Future enforceable control / package | Current state |
| --- | --- | --- | --- |
| Inward dependency/UI boundary | CleanArchitectureBoundaryTests and UI source guards | Extend contract assembly/dependency rules R01/R02 | Source tests exist, not rerun here |
| Safety/Apply/history | Existing named rule/contract/PG tests | P09/P10 supported-client success cases, R04/R06 equivalence | Partial inventory; execution unverified |
| No shared fixture/data leakage | Some PG tests assert loopback/database prefix; secret-safe exception handling | P03 fixture allowlist/global environment guard and log/artifact scans | Partial source control; not full release guarantee |
| No direct main/unsafe workflow | GitHub main currently unprotected, rulesets empty | P02 branch/rules/CODEOWNERS + independent review where feasible | NOT IMPLEMENTED |
| Owner-only Beta and IAM apply | GitLab production disabled; source HMAC guard exists | P04 native reviewer/external owner authority, IAM trust and bypass tests | GitHub enforcement NOT IMPLEMENTED; old guard not accepted as sufficient |
| One authority / lock loss | Existing advisory-lock runner and GitLab resource_group | P05/P12 external env lease + DB supervision covering all tools | Existing source only; cross-provider control NOT IMPLEMENTED |
| Schema lifecycle/no EnsureCreated shared upgrade | Model-first bootstrap/fingerprint inspector | P06 guarded disposable bootstrap, baseline/ledger/checksum/compatible ranges | NOT IMPLEMENTED |
| Immutable API/worker promotion | ZIP validator/native test, API published versions exist | P05/P10 same digest, config/source schema provenance and qualified aliases | Serving targets unqualified; worker not versioned |
| Default-deny Staging→Beta | Current API role grants both media buckets | P07/P08 environment resource/IAM/network/secret denies + tests | FAIL current separation; target NOT IMPLEMENTED |
| Per-layer DoD/release audit trail | Existing docs/TRX/deployment report structures | P02/P05/P10 release evidence schema and required aggregate | Proposed contract; no automated enforcement added |
| No Notion operational dependency | No such dependency found in inspected release source | P02 document boundary; P13 GitHub-only release/recovery rehearsal | Preserved requirement; personal notes remain optional |

## Documentation adoption and review

The patch includes root AGENTS, minimal additions to README/ARCHITECTURE/ROADMAP/STATUS and this complete planning set. The four existing untracked canonical docs are preserved as external prerequisites in the review baseline; only ARCHITECTURE gains an additive transition pointer. Their content/ownership is not replaced. Existing AUDIT/performance/design/runbook evidence remains unchanged and linked; stale restore semantics are explicitly flagged for P06 correction, not silently executed.

Before application, compare baseline file hashes and `git apply --check` in an isolated review copy. If Rider docs have changed since capture, regenerate/reconcile the patch; never force apply. Applying the planning patch does not implement CODEOWNERS, workflows, IaC, migrations or remote protections. READY FOR REVIEW applies to documentation only.

## Publication follow-up — 2026-11-04

Owner authorization expanded the task to adopt and publish the current documentation and shared development files. `.gitignore` replaces broad shared-agent/document exclusions with targeted machine/credential/generated-output rules; the required public RDS trust bundle, portable optional hook template and historical Impeccable review become versioned. Active per-machine hooks, imported conversation attachments and local identities remain excluded. This is documentation/development support publication, not implementation of the GitHub platform or release roadmap.
