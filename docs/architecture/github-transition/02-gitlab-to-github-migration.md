# One-time GitLab to GitHub transition

Status: proposed, not executed. Baseline and evidence: [audit E01–E06/E09/E22](00-current-state-audit.md). Target: GitHub alone for source/PR/release automation; no permanent GitLab deployer or bidirectional mirror. Package dependencies refer to [P00–P13](03-implementation-roadmap.md).

## History reconciliation: preserve the latest Rider implementation

| Ref at audit | SHA | Relationship to Rider HEAD |
| --- | --- | --- |
| Rider restore | fe350c4fd0026f18608f441fe2deb5dd3a3d5b86 | Owner-designated latest code; dirty documentation also preserved |
| GitHub main | ff3be300b5a769c1e0ccd1c5c3fa18d2d8626224 | 279 Rider-only / 65 main-only; common base 60f9b8d829eb7f1aeb16551cddc210e2b24737af |
| GitLab test-flight-beta | c8ffbccb031ea0e137ce1247906a6bf770080d9f | 8 Rider-only / 8 GitLab-only |
| GitLab codex/plan-generation-checkpoint | 47fd713064fcf6f79e2acd894e636817952816a9 | 54 Rider-only / 2 checkpoint-only (auditor result; recompute before integration) |

These are neither equal nor safe to overwrite. GitHub #14 is closed/unmerged; it does not prove checkpoint integration. No advertised remote tags were found. Before implementation re-run ls-remote; if tips moved, produce a new disposition ledger rather than applying stale SHAs.

1. P00 inventories every local and remote ref, tag, linked worktree and dirty file; snapshot only owner-selected work with file hashes. Produce `git bundle` backups plus a separate safe archive of uncommitted docs; validate with `git bundle verify` and a test clone. Bundles do not contain uncommitted files. Do not stash/reset the Rider checkout.
2. After owner authorizes remote publication, import source objects into an isolated managed integration worktree/repository. Use read-only fetches and uniquely named preservation refs, never overwrite existing refs. Pin source SHAs and tree IDs in the ledger. Keep restore's source tree as the behavior baseline.
3. Start `integration/github-history` from the pinned GitHub main so its final PR is fast-forwardable through a normal merge. Merge the pinned Rider donor with `--no-commit --no-ff`; resolve per module against the latest Rider behavior while reviewing each main-only commit. A blind “ours/theirs” whole-tree merge is forbidden. Document kept/reimplemented/superseded decisions, especially history, hydration, measurements and planning surfaces. Review `.gitignore`, AGENTS, STATUS and deleted docs separately.
4. Compare each GitLab-only commit to the candidate with `git log --left-right --cherry-pick`, `git range-diff` and path diffs. Merge preserved branches when their ancestry belongs in canonical history; otherwise cherry-pick reviewed missing changes with original SHA/source links and explicit disposition for every omitted commit. Keep imported archival refs until the owner agrees retention. Do not revive obsolete migrations or deleted product behavior solely to eliminate commit differences.
5. Verify `git merge-base --is-ancestor <pinned-github-main> <candidate>` and `<pinned-rider-head>` for the chosen merge strategy; if a GitLab source is cherry-picked rather than merged, prove patch/disposition coverage instead of falsely claiming ancestry. Run characterization tests and compare full tree/application paths to Rider. Stage only selected changes; root docs are reconciled with the captured dirty baseline.
6. Open the normal GitHub PR, attach commit-disposition and behavior evidence, require review/checks, then merge only when authorized. Never prescribe force push/reset of main. Exceptional history rewriting requires a separate owner-approved incident plan, full backups and collaborator coordination; this plan does not require it.

## Existing pipeline, including inherited behavior

`.gitlab-ci.yml` admits merge_request_event, branch push and branch web sources only (1–6); stages are source_verify, backend_verify, client_verify, backend_package, artifact_validate, artifact_upload, backend_deploy (8–15). `.dotnet_job` (23–47) pins SDK container 10.0.403, installs fontconfig/python, writes TRX and fails empty/unexecuted/failed counters; always retains test-results 30 days. No include, cache or test-report parser declaration was found. `.release_job` (217–250) adds OIDC setup, DbMaintenance lock runner, global resource_group `lambda-shared-target`, release.sh and 90-day deployment-report artifacts. Source behavior must be translated, not merely job names.

All GitHub workflow paths below are **anticipated**, not implemented. Permissions use default `contents:read`; only the named protected deploy jobs receive `id-token:write`, and no PR job gets AWS credentials. Artifact upload/download Actions permissions must be verified against pinned action requirements; no blanket repository write permission.

| GitLab source/job | Proposed Actions workflow/job; trigger | Needs / artifacts / runner details | Target role/permissions | Parity acceptance / status |
| --- | --- | --- | --- | --- |
| 49–56 verify_source | `ci.yml / source`; PR and main push | Verify SHA/tree, restore/build Server Release, exact reviewed SDK; Linux | No AWS | Same source identity/build/analyzers; remove SDK drift only through P03; NOT IMPLEMENTED |
| 58–69 verify_plan_generation | `ci.yml / backend-tests`; PR/main | After source; package-upload Python, fail-closed shell, release-engine unittest, Core/Server/Registration and model/catalog filter; TRX 30d | No AWS, contents read | Every listed command executes, nonzero TRX and filtered-test inventory match; no external credentials; NOT IMPLEMENTED |
| 71–116 verify_postgres_compatibility | `db-verify.yml / postgres`; workflow_call from CI, PR/main | Disposable PG17, same five MC_* fixture bindings, setup once, separate restricted migrator clone, full integration suite; preserve role/RLS checks and new equivalent upgrade tests | Local fixture roles only | Missing 3 old tests are a CONFIRMED failure, not inherited parity. P06 adds baseline/additive/old-new/RLS/drift tests and updates assertions; NOT IMPLEMENTED/BLOCKED |
| 118–122 verify_mobile_contracts | `ci.yml / mobile-contracts`; PR/main | After backend_verify logical gate; standalone contracts suite, TRX | No AWS/Apple secrets | Same suite and count guard; this is not a MAUI build or device gate; NOT IMPLEMENTED |
| 124–147 package_staging_lambda | `backend-build.yml / package`; workflow_call on reviewed main candidate | All above checks, Lambda.Tools 7.0.0, net10.0/Linux ARM64 ZIP, source/tree manifest; existing package job emits manifest but only persists ZIP, so retain both explicitly | No deploy role | Valid archive/native assemblies, digest/provenance for API AND separately worker; ZIP+manifest retention and build once; NOT IMPLEMENTED |
| 149–163 validate_lambda_artifact | `backend-build.yml / validate`; after package | Revalidate same ZIP/tree, persist manifest/results, no republish/rebuild | No AWS | Tamper/path traversal/wrong SHA/empty evidence negative cases; NOT IMPLEMENTED |
| 165–179 verify_lambda_arm64 | `backend-build.yml / arm64`; after validate | Actual Linux aarch64 runner (GitLab tag saas-linux-small-arm64 has no automatic Actions equivalent), font/native dependencies, exact ZIP startup + Skia PNG probe | No AWS, isolated runner | `verify-arm64-package.sh` on native architecture; liveness-only is not DB readiness; availability/minutes verified; NOT IMPLEMENTED |
| 181–215 upload_lambda_artifact | `backend-release.yml / upload`; workflow_dispatch immutable release ID, approved protected ref | Needs validation/native proof; OIDC, versioned S3 object/release-object.json, retain reports 90d; no upload on fork/PR | Dedicated artifact role, id-token write, scoped PutObject/version controls | Reject debug trace, wrong source/tree/object/digest/role; upload same verified bytes, no deploy permission; NOT IMPLEMENTED |
| 252–259 deploy_staging_lambda using .release_job | `backend-release.yml / deploy-staging`; explicit promotion of verified release | Needs upload/reference + P06 schema proof, P08 isolation, shared mutation lease + DB lock, protected environment | Staging deploy role only | Port GitLab guard contract rather than spoofing CI_* vars; alias revision checks/smoke/recovery/worker; owner-authorized Staging change; NOT IMPLEMENTED |
| 261–267 deploy_production_lambda, rules never | No active Production workflow. Add `backend-release.yml / deploy-beta` only in P12 after protections | Preserve current production-disabled semantics; Beta is a new explicitly gated target, not relabeling a disabled job | Beta role only with owner approval outside editable PR code | Negative approval/no-lock/cross-env tests; evidence-bound owner release; Production stays disabled; NOT IMPLEMENTED |

Separate **new** workflows fill current gaps: `mobile-release.yml` (macOS, endpoint-specific signed iOS builds/TestFlight), `db-release.yml` (reviewed migration artifacts/lock), `infra.yml` (template/policy validation/change-set/apply). They have no existing equivalent and cannot be labeled parity-complete. Reusable workflow/composite candidates: .NET/TRX setup, package validation and evidence schema. Avoid abstracting environment authorization into a generic script editable by arbitrary contributors.

## Actions design and security contract

- Pin every third-party Action to a reviewed full commit SHA and record update ownership. Pin SDK/container/workload/Apple toolchain; retain dependency lock/provenance and vulnerability results. No reusable self-hosted runner executes untrusted fork code with persistent release credentials.
- Required `ci / required` aggregation always runs. Path routing may skip unaffected expensive jobs only with an explicit dependency map: Core/UseCases/contracts → mobile/backend/DB consumers; model/security SQL → DB/backend; global.json/project/build/tooling → all dependent jobs; CI/AGENTS → governance checks. Unknown paths run all. Never leave required checks pending by filtering an entire required workflow. Never treat a skipped required safety job as success.
- `pull_request` is untrusted: no `pull_request_target` checkout/execution of PR code with secrets, OIDC or privileged token. Dependabot/fork behavior is tested. Branch protection covers main and release refs, stale approvals dismissed, required checks, conversation resolution, code-owner review for workflows/schema/IAM/contracts, restricted bypass and protected tag creation.
- Public repository currently makes environment protection broadly available on current plans; verify settings and roles rather than assuming configured. If visibility changes, revalidate feature availability before promotion. Private required-reviewer support varies by plan. [GitHub environment documentation](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments).
- OIDC uses `token.actions.githubusercontent.com`, audience `sts.amazonaws.com`, exact repository identity/approved environment in subject plus applicable ref restrictions. This repo predates July 15, 2026; verify whether immutable ID subjects were opted in. Do not hardcode the old name-only format without inspecting an actual non-secret claim sample. Restrict allowed workflow/ref/environment at issuer, role trust and deployment verifier; environment subjects need deployment branch policies. No permanent AWS admin keys. [GitHub AWS OIDC guidance](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-aws).
- Native environment approval preferred. Fallback: owner-operated authenticated short-lived deployment with no Actions Beta-assumable role, or an owner-held asymmetric signing key verified by an independent protected service/role that authorizes only environment+source/tree+artifact+schema+config+expiry+nonce. Do not place an approval signing key alongside its signature in ordinary CI variables; a workflow-dispatch button or HMAC checked only by candidate-controlled code is not an enforceable boundary. Require negative bypass tests.
- Environment `concurrency: mutation-<environment>` with cancel-in-progress false for mutating jobs; shared external lease also covers old CI/manual/IaC. Fork/read-only tests can cancel safely. Each artifact includes commit/tree, compiler/runtime, package checksum, workflow/run, SBOM/security findings, config-independent manifest; deploy reports add config/schema/alias/prior version and approval/lease.

## Variables, credentials and integrations transition

Source-visible names only; values must never be copied into docs/PRs. Remote inventory remains P00/P04 work because GitLab API returned 404 without authenticated metadata access.

| Existing contract | Replacement / retention |
| --- | --- |
| CI_COMMIT_SHA, CI_COMMIT_TREE_SHA, CI_PIPELINE_ID, CI_JOB_ID, CI_PROJECT_DIR | Explicit platform-neutral source/tree/build/run/job/workspace fields from trusted Actions context; no CI_* spoof layer as authorization |
| CI_PROJECT_PATH, CI_PIPELINE_SOURCE, CI_COMMIT_REF_PROTECTED, CI_COMMIT_BRANCH, CI_ENVIRONMENT_NAME, CI_DEBUG_TRACE | Replace source-provider trust logic in release.sh and release-engine.py; verify issuer/repository/environment/ref/approval at protected boundary, refuse trace/secret logging |
| AWS_RELEASE_ROLE_ARN, AWS_ROLE_ARN, AWS_RELEASE_BUCKET, AWS_REGION, AWS_OIDC_TOKEN | Environment-scoped role/bucket identifiers and Actions OIDC short-lived sessions; tokens never persisted as artifacts; separate artifact/runtime/migrator/IaC deploy roles |
| MC_RELEASE_POLICY_FILE, MC_DB_LOCK_RUNNER, ConnectionStrings__PostgreSql, MC_RELEASE_OFFLINE_TEST | Reviewed immutable target policy; protected in-network maintenance runner resolves DB secret at runtime; offline bypass allowed only in isolated tests and forbidden by production verifier |
| PRODUCTION_RELEASE_AUTHORIZATION_KEY / SIGNATURE | Existing production guard remains disabled. Beta uses redesigned owner boundary; retire old secret only after owner-authorized rotation/revocation plan |
| API signing, Apple private key, email HMAC/receipt RSA, Resend key; worker discovery/tenant credentials | Inventory names/owners/rotation dates; environment-specific managed secrets, no source or artifact values. Rotation is a separately approved staged compatibility change; do not rotate simply because a CI provider changes |
| Apple certificates/profiles/App Store Connect API key | Restricted temporary keychain on clean macOS runner, least-privilege App Store Connect identity, secret-free logs, cleanup and provenance; access unverified |
| GitLab hooks/status/badges/runner tags/registries/mirrors | Owner export metadata and check package restore URLs, webhooks, Codex/agent repo permissions, badges/support docs and deploy status links. Current GitHub #14/Copilot history is evidence of an integration, not proof of current agent permission configuration |

## Cutover phases and single active authority

| Phase | GitHub | Existing environment authority | Evidence to advance |
| --- | --- | --- | --- |
| 0 Preserve/inventory | Read-only until authorized history PR | UNKNOWN until owner/run inventory; default no deploy | P00 recovery snapshot, authority register, Rider baseline, data classification |
| 1 Reconcile + shadow | Build/test/package only, no cloud role | Exactly one recorded existing operator/CI | Reviewed history dispositions, equivalent meaningful tests and immutable package validation |
| 2 Isolated Staging | Sole deployment authority for NEW Staging after P08 | Existing environment remains protected under its recorded authority | OIDC/approval negative tests, independent resources/no-cross-write proof, migration rehearsal, smoke/rollback |
| 3 Beta cutover | Sole Beta authority after explicit owner approval | Disable/revoke old deploy authority BEFORE enabling new; manual recovery remains one recorded operator | Backups/restore, old/current client tests, device, performance, media, worker, signed candidate |
| 4 Retire | Source/release/ops all GitHub | GitLab read-only archive, deploy trust revoked | Repeat release/recovery with GitLab unavailable and no restore/build/runtime dependency |

Freeze only the affected release target during authority transfer; ordinary local product branches can continue. Record freeze start/end, in-flight pipeline cancellation/queue drain, worker compatibility window and rollback owner. No downtime window is assumed necessary; if alias/schema transition cannot preserve requests, owner must approve a specific maintenance window and client behavior before proceeding.

## Failure and recovery decisions

| Trigger | Immediate action | Recovery / no-go |
| --- | --- | --- |
| History/content regression | Stop reconciliation PR; retain Rider and all preservation refs | Fix candidate from disposition ledger; no reset/force of main |
| Shadow parity missing/zero tests/native failure | Block artifact promotion | Repair CI or tests; do not make checks optional |
| Staging isolation/credential cross-write failure | Deny any shared deploy; stop synthetic writes | Correct roles/resources and rerun negative tests; investigate any accidental access as incident |
| Backend health/auth/SLO regresses, schema still compatible | Halt traffic shift under lease | Repoint alias to recorded compatible prior version/config; verify worker/clients; do not rebuild prior artifact |
| Expanded schema breaks old API | Block contract/rollout; retain data | Compatible forward fix or rehearsed data-safe repair; code rollback only if matrix proves it |
| Data corruption/destructive migration | Stop writes and incident assessment | Owner-led restore/fail-forward with measured loss/reconciliation; snapshot availability alone is not permission to overwrite Beta |
| New CI unavailable after transfer | Freeze deployment, keep last good service serving | Owner-approved manual recovery using verified GitHub artifact; never silently re-enable GitLab |
| Lock/approval lost or resource revision raced | Abort before mutation or terminate supervised process; mark partial if already changed | Reconcile observed state, renew reviewed candidate; no parallel retry that bypasses lock |

## Retirement checklist

- [ ] All branch/tag/commit dispositions and GitLab MR/issues/releases/attachments exported or explicitly retained with source URLs/IDs; unknown access resolved or documented owner retention decision.
- [ ] GitHub main protected, Codeowners/review policy effective (including solo-owner alternative), required checks proven, supported runners/budgets verified.
- [ ] DB/backend/mobile/infra workflows independently gated, same backend digest promotion proven, endpoint-specific mobile provenance accepted.
- [ ] Existing environment retained/data protected, restore tested, minimum client policy and rollout telemetry approved.
- [ ] GitLab deploy schedules/manual jobs/webhooks disabled, queued/in-flight runs drained, IAM trust/roles/registry tokens/runner credentials revoked or restricted with separately authorized rotation; no double authority.
- [ ] README/badges/package URLs/agent integrations/status pages all point to GitHub; no hardcoded GitLab CI variable remains an operational requirement.
- [ ] Release API and worker, rehearse migration/rollback and export signed iOS build with GitLab network access disabled in the build/release environment. Verify last-good recovery artifact independently accessible.
- [ ] Owner signs measured cutover results and archive/retention policy. GitLab stays read-only historical fallback until then; no repository deletion is part of this plan.

The first safe milestone is GitHub **shadow CI on the reconciled Rider-based candidate**. The first deploy milestone is a protected release to newly isolated Staging. Neither requires changing Beta data or prematurely retiring GitLab.
