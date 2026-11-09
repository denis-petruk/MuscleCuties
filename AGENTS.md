# MuscleCuties — agent operating contract

Proposed 2026-11-04. Hard gates below are mandatory within the current user's authorization. This file does not authorize implementation, remote writes, releases, or infrastructure changes by itself.

## Orientation and precedence

1. Follow platform instructions and the current user request first. Preserve unrelated changes; never reset, stash, overwrite, or commit them without authorization. Read `git status --short --branch`, record HEAD, and establish the task's allowed files before editing.
2. Read this file, applicable scoped instructions, [architecture](docs/ARCHITECTURE.md), [product workflows](docs/PRODUCT_WORKFLOWS.md), [safety](docs/SAFETY.md), [verification gates](docs/VERIFICATION_GATES.md), and relevant ADRs. For platform/schema work also read the [environment and release design](docs/architecture/github-transition/01-target-architecture.md), [migration plan](docs/architecture/github-transition/02-gitlab-to-github-migration.md), and current migration ledger. A missing ledger is a blocker for a shared schema change, not permission to invent one.
3. Search with `rg --files`, then `rg` and narrow excerpts. Exclude generated output, bin/obj, secrets, logs and binaries unless required by the task. Keep one canonical rule owner and link to it. Dated reports are evidence at their recorded revision, not live deployment truth.
4. Root owns cross-layer rules; a scoped file may add local details but cannot relax hard gates. If instructions conflict with the current request, apply the request and record the reconciliation. Suggested branch naming and estimates are conventions; authorization, isolation, safety, privacy and evidence gates are hard requirements.

## Source of truth and integration

- GitHub `denis-petruk/MuscleCuties` is the sole target for reviewed integration, PRs and releases. Until history reconciliation is complete, the owner-designated Rider checkout is the latest implementation baseline; GitHub `main` must not overwrite it. Preserve all relevant remote history through the reviewed reconciliation plan.
- Work through PRs into protected `main`; never push directly, force-push, bypass required checks, or write to GitLab without explicit owner authorization. After cutover, GitLab is read-only historical evidence and must not remain a hidden deployment dependency or bidirectional mirror.
- Suggested branches: `product/<package-id>-<topic>`, `platform/<package-id>-<topic>`, `integration/<candidate>`. Never invent an owner/reviewer identity or count a bot's self-review as independent approval.
- **No agent attribution in Git or GitHub.** Commit messages, author/committer/co-author fields, PR titles and descriptions, review comments and code comments must not name an AI agent, assistant, tool or model (e.g. Claude, Codex, Codebuff, Gemini, Copilot) or link to an agent session. Commit as the owner's Git identity, never an agent default identity; omit `Co-Authored-By`, `Generated with` and session trailers or footers, even when a tool adds them by default. Files that configure agents (`AGENTS.md`, `CLAUDE.md`, `.claude/`, `.codex/`) may exist and be named.

## Two orchestrators and one integration authority

- **A — Product:** mobile, client adapters, API/use-case/domain refactoring and behavior tests. **B — Platform:** GitHub workflows, release tooling, schema lifecycle, IaC and operational verification. Use separate branches/worktrees.
- Before work, record package ID, base SHA, owned paths, excluded paths, dependencies, shared-file reservations and next checkpoint in the repository handoff. One package has one accountable owner. Workers inherit only that bounded scope and receive no release credentials.
- The **integration authority** exclusively owns root AGENTS, solution/project graph changes, shared DTO changes, canonical architecture/status/roadmap, release manifests and migration numbering. It can delegate an exclusive reservation to A or B; two agents must never edit or allocate the same shared contract/schema sequence concurrently. B owns migration execution plans; A requests additive changes through that interface.
- Only the integration authority prepares promotion evidence; only the owner approves protected external actions. Neither development orchestrator may independently mutate cloud resources, shared databases, secrets, or release targets. Serialize every database/backend/worker/IaC mutation for an environment through the same external lock.

## Permission boundaries

| Action | Required authority |
| --- | --- |
| Authorized reversible local edits, bounded read-only inspection, non-UI local checks using synthetic/disposable fixtures | May proceed unattended within scope |
| Integrating code, workflow, schema, contract or governance changes | Reviewable PR, applicable checks and designated reviewer; pushing still needs owner authorization unless already granted |
| Any live cloud change, Staging release/provisioning, shared database write/migration, IAM/sharing change, credential rotation, distribution/publication, destructive operation or remote-history rewrite | Explicit owner authorization for the concrete target/action; existing session authorization can satisfy it |
| Beta deployment or migration | Release evidence, recovery proof, exclusive lock and explicit owner approval tied to source/artifact/schema/config target |

Never infer external authorization from local preparation, a plan, a green CI run or a past release. No Beta data for local testing, no destructive tester-data cleanup, and no production provisioning under a Beta/Staging task.

## Architecture and product contracts

- Preserve the modular monorepo: mobile, API/workers, schema lifecycle and infrastructure are separate change boundaries. Dependencies point inward to Core; Core has no ORM, transport, ASP.NET or MAUI dependencies. UseCases depends on Core; adapters implement ports. UI ViewModels never consume DbContext/repositories or persistence models.
- Preserve HTTP/JSON DTOs, routes, XAML bindings, navigation, localization and persistence semantics. Check XAML before renaming/deleting bound members. Keep current iOS-only targeting; do not add Android. C#/.NET remains the production runtime. Sensitive offline snapshots use encrypted JSON and SecureStorage keys; no SQLite or sensitive Preferences storage.
- Keep one authoritative workout generation/acceptance path. Characterize legacy/local paths and prove equivalence before retiring one; never silently substitute a local generator after an API/schema/auth failure. Preserve registration, persisted preferences, preview/Apply, schedule, execution/logging, injury restrictions, history, hydration and measurements.
- Preserve the source's tested first-plan automatic acceptance exception and explicit replacement acceptance until a separate product decision changes them. Apply supplies the required acknowledgments. Missing/stale/unknown injury answers must remain distinct from false; never relax a restriction or increase intensity because data is absent. Use safe strength substitutions where permitted; withhold an unsafe session with a review reason when no permitted strength block exists.
- Health synchronization stays dormant unless the owner authorizes its scope. No health records, identifiers, photos or secrets to external AI/services. Use synthetic fixtures and secret-free logs. Do not invent clinical approval or new health rules.
- For UI changes read [DESIGN.md](DESIGN.md) and its references. Preserve one shared page system, `ButtonPrimary`/`ButtonSecondary`, `CardPrimary`/`CardSecondary`, role-based colors, shared modal lifecycle, EN/RU semantics and accessibility. Do not introduce competing style systems.

## Schema, environments and release safety

- Environment must be explicit and allowlisted. Local tests reject external hosts. Staging roles must be unable to write Beta DB, buckets, queues or secrets. Resource names or Lambda aliases alone do not prove isolation. Never log connection strings/tokens/secret material.
- `dbinit` writes supplemental SQL, security policy, catalog and metadata; it is not a harmless drift repair or a versioned upgrade. Never run it to silence fingerprint failures. `EnsureCreated` is permitted only in explicitly disposable/test initialization once guarded; shared evolution uses the reviewed migration workflow and version ledger. No runtime or cold-start migrations.
- Schema changes follow expand → compatible backend/worker → supported mobile clients → deferred contract. Validate the old and new applications against allowed schema versions; never update a fingerprint merely to make an incompatible database appear compatible. Data migration reversal is not guaranteed by code rollback.
- Promote the exact verified backend/worker package digest; publish immutable versions and route approved aliases. No `$LATEST` serving target for the protected design. Mobile binaries may differ when compiled endpoint/signing differs: record each binary's source, endpoint identity and signing provenance.
- Three release tracks: DB, backend/API/workers, mobile/TestFlight. IaC has its own protected plan/validate/apply workflow. PR code never receives deploy credentials. Notion is personal notes/habits only and is never a build, approval, deployment, release-state or recovery dependency.

## Verification, states and completion

- Use G0–G9 from [verification gates](docs/VERIFICATION_GATES.md) and the [layer/environment matrix](docs/architecture/github-transition/04-ci-cd-verification-matrix.md). Run affected non-UI builds, analyzers, regression/contract/security checks; persistence requires real disposable PostgreSQL and supported-version rehearsals; deployed gates require target evidence. Do not claim current tests passed from a dated report.
- MAUI build/run/screenshots need task-specific owner authorization. The 2026-11-04 exception is not standing permission. Until authorization and actual device evidence exist, report those gates as unverified; do not bypass them for a mobile release.
- **BLOCKED:** a required acceptance condition fails or access, authorization, compatibility or recovery evidence is missing. Continue independent safe work; name the blocked package. **READY FOR REVIEW:** scoped diff complete, applicable local checks pass, evidence attached and remaining external gates stated. **SAFE TO PROMOTE:** protected target checks, compatibility, recovery, security and owner approval all match the candidate. This state is not permission to execute.
- Evidence records source SHA/tree/branch, package digest and storage version, environment/account/resource identity, configuration hash, schema version/compatibility range, test commands/results, reviewer and owner approval, lock owner, deployment timestamps and measured outcomes. Omit secrets and user data.
- Definition of Done: DB has verified ordering/checksums/locks and integrity/recovery rehearsal; backend/worker has verified immutable package/contracts/auth/smoke/observability; mobile has verified endpoint/signing/install/user flows/device acceptance; IaC has reviewed change set, policy/isolation checks and authorized post-apply verification. Documentation-only work proves none of these runtime outcomes.

## Progress and handoff

Keep [STATUS.md](STATUS.md) and [ROADMAP.md](ROADMAP.md) compact. Checkpoint significant decisions, exact file paths, commands/results, remaining work, base/head SHAs, reservations and blockers in a scoped repository handoff. Before reaching the platform's actual context limit (or 200k tokens if lower), checkpoint while space remains; resume only through supported session handoff. Never invent scheduled continuations or background progress.

Finish with implemented/proposed changes, verification actually performed, unrun gates and next action. Never auto-close unresolved security/reliability findings or describe planning as migrated, deployed, AWS-tested or release-ready.
