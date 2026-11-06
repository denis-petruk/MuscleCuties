# TestFlight beta audit and work queue

Prepared 2026-11-04 (America/Toronto). **Current decision: NO-GO pending the evidence and fixes below.** This is a source/evidence audit and a plan for completing full acceptance; it is not a completed runtime, cloud, database, security or device audit.

This package is additive and does not edit the active coordinator's CONTROL, root STATUS/ROADMAP, existing remediation documents or application source. No implementation, Git publication, cloud operation, schema write, MAUI build/run or TestFlight distribution was performed.

## Read in this order

1. [Start instructions](START_HERE.md).
2. [Full implementation plan and canonical package map](IMPLEMENTATION_PLAN.md).
3. This audit and its acceptance matrix.
4. [Workout policy proposal](WORKOUT_POLICY_PROPOSAL.md) and [unified activity experience](ACTIVITY_EXPERIENCE.md).
5. [Full coordinator execution prompt](ORCHESTRATOR_PROMPT.md).

The implementation plan owns current package IDs/dependencies. Preparing these artifacts starts no application work; explicitly invoking the execution text supplies its bounded future scope.

The external research and stated preferences are saved in [the private Notion research report](https://app.notion.com/p/40f47be9496581ccb0a5f440dd8b1236). Essential policy requirements and primary source links are also in this repository package: Notion is not a release, build, approval or recovery dependency.

## Candidate and source drift

| Source inspected | Identity and limitation |
| --- | --- |
| Owner-designated Rider checkout | `/Users/denispetruk/RiderProjects/MuscleCuties`, branch `docs/github-transition-governance-20261104`, HEAD `acdb131140dec58c4625132682982b7baba4a4c7`. Existing dirty STATUS/ROADMAP/design audit/remediation work was preserved. |
| Active coordinator | `/Users/denispetruk/.gemini/antigravity/worktrees/MuscleCuties/execute_gemini_cli_roadmap/docs/architecture/github-transition/execution/CONTROL.md`. It records P00–P13 and R01–R05 as completed/merged/deployed and R06 in progress. These are claims to reconcile with acceptance evidence. |
| Product candidate inspected | `/Users/denispetruk/.gemini/antigravity/worktrees/MuscleCuties/product_r06_injury_reconciliation_progression`, first `2ab653db…`, then clean HEAD `9c980fa296f7b8499986769d3751ff8438909a57`. |
| Local cached GitHub main ref | Advanced during inspection to `f010787a928d4d10696761c207c7973b323deaf1`, R06 PR #31 merge. Diff from `2ab653db…` contains only two added progression tests and an evidence document; no production policy or release-workflow fix. This is a local remote-tracking observation, not a fresh server/branch-protection verification. |

There is active parallel work. Recheck candidate identity and changed paths before using this audit. Do not reset Rider, overwrite R06, or start a competing coordinator. R05's environment cache/token wiring is present in the current candidate; the older P09 “helpers unused” finding is withdrawn. A remaining shared legacy-token fallback is described below.

**Planning refresh, 2026-11-04:** local cached origin/main advanced to `d840d62745337a604f56f4ba32568fb66ae53158`; the coordinator now reports R06/R07 and its full roadmap complete. R07 removes a separate unused Infrastructure planner/interface/duration estimator and registration, with a source guard. Preserve that completed cleanup. Its six-file diff does not change the workout-policy, logging or platform/release findings below; Core helper reachability remains unproven. The initial snapshot table above remains historical evidence. The full plan starts corrective packages against the verified current successor, not a replay of R07.

At final documentation verification, a separate concurrent documentation commit advanced Rider to `a2d55adc331dcdcf7d7089d061c9037a1d12e079`, recording the pre-existing design/remediation/status files. No application source changed in that increment. This release-audit folder remains untracked and must still be made available to new worker checkouts.

## What is actually awaiting orchestration

| Existing document/prompt | Disposition |
| --- | --- |
| `docs/architecture/github-transition/08-execution-orchestrator-prompt.md` and `09-gemini-cli-execution-prompt.md` | The overall job has already been launched elsewhere. Resume/reconcile the existing control ledger; do not rerun P00–P13 merely because Rider's dated roadmap is behind. Merged scaffolding does not close operational gates. |
| `docs/design/orchestration/01-ui-ux-remediation-orchestrator.md` | Prepared A.UI sublane for RM00–RM08, including crash/recovery, photo/archive widths, nutrition editor and shared styling. Its existence does not prove implementation. |
| `docs/design/orchestration/02-backend-wiring-orchestrator.md` | Prepared A.API sublane for diagnostics, typed errors, data/persistence and fixtures. It remains under Product A, separate from Platform B. |
| `docs/design/UI_UX_BACKEND_REMEDIATION_PLAN_2026-11-04.md` | Relevant source audit and pending fixes; integrate its issues with the new workout requirements rather than replace it. |
| `docs/infra/checkpoints/lambda-release-orchestrator.md` and `postgres-schema-orchestrator.md` | Historical infra continuity. Reconcile against current workflows/model and exact target; never execute old instructions blindly. |
| `IMPLEMENTATION_PLAN_BACKEND_FIRST.md` in the document workspace | Historical October 31 source-integration plan. Useful provenance; its migration inventory and unfinished work are not current acceptance evidence. |

## Verified source findings and release implications

Paths below are relative to the owning checkout. Platform findings were inspected on current R06 source; product policy observations apply to Rider and the inspected candidate unless stated otherwise. Line references identify the audit snapshot and may move later.

| ID | Finding and evidence | Required closure |
| --- | --- | --- |
| A01 | The canonical proposal uses `PreviewStrengthPlanV020UseCase.cs:125–191` → `StrengthPlanPreviewRule`. `WeeklyWorkoutPlanGeneratorRule` and `ResolveDoubleProgression` have no production callers located under src/services. | Prove the intended generation → logging → next prescription path. Testing unconnected helpers cannot close it. |
| A02 | `LowerBodyCoverageRule.cs:20–26` guarantees selected lower-body regions twice, treating others as best effort. Preview caps actual General Strength template selection at two (`StrengthPlanPreviewRule.cs:561–579`). | Enforce the newly requested every-main-muscle minimum and preferred third exposure, with explicit infeasibility and injury exceptions. |
| A03 | Active `CycleReadinessPrescriptionRule.cs:105–114` removes one optional accessory set; it does not adjust weight or produce the requested lighter-load/higher-rep/set mode. `PlanningReleaseApproval.cs:10–34` activates coverage with `MC-PLANNING-REVIEW-TEST` while admitting no clinical sign-off. | Review a real versioned policy and its authority; remove fixture-based production approval. Research is not itself sign-off. |
| A04 | `StrengthProgressionRule.cs:152–188` takes the last two supplied records without validating consecutive week numbers or set loads; it cannot prove all planned sets were completed. UI logs aggregate sets/reps/weight, without the required observed per-set RIR/technique evidence. | Connect progression to complete persisted evidence and define increase/hold/regress/recovery outcomes. Do not overstate R06's two new tests. |
| A05 | Schedule projects only primary + one companion (`PostgresWorkoutPlanAndSessionStore.cs:1260–1262`; `WorkoutOverviewContracts.cs:25–38`); UI filtering reads only those IDs (`WorkoutViewModel.cs:469–476`). | Preserve all ordered sessions per date through DTO, UI, persistence and history; test three and four activities. |
| A06 | Acceptance creates exercise rows only for strength (`PostgresAcceptStrengthPlanProposalStoreV020.cs:401–418`); no-exercise activities enter a timed flow (`WorkoutSessionViewModel.cs:536–539,826`). Running's existing exercise-based distance/pace fields are bypassed. | Implement one activity logging contract/anatomy with relevant modality fields and matching history correction; retain valid duration-only logs. |
| A07 | Current G5 is reopened by reported native Button.LayoutButton crash and plan-recovery/layout failures (`docs/design/GATE_REGISTER.md:5,45–47`). The failing installed binary is unidentified. | Separate native layout/lifetime failure, transport/recovery error, and deterministic generator/policy failure. Reproduce on the exact candidate; do not call every failure a workout engine defect. |
| A08 | Existing source audit finds nullable injury answers rendered as false, hydration failures swallowed, and stale cycle fallback manufacturing certainty. See F01–F03 in the app-wide design audit. | Verify these against the final candidate and close tri-state, persistence acknowledgement and uncertainty scenarios. |
| B01 | `.github/workflows/backend-release.yml:94–98,135–139` writes `status: verified` with echo; no actual deployment or target verification occurs there. | Replace placeholder success with candidate-bound deployment/verification, API+worker artifact identity, compatibility and recovery evidence. |
| B02 | `.github/workflows/db-release.yml:48–53` runs `dbinit` for shared release. `PostgresSchemaBootstrap.cs:57–87` remains EnsureCreated plus an override guard, not an ordered migration/adoption lifecycle. | Build/rehearse reviewed schema adoption, versioning, locking, checksums and compatibility. Never run dbinit to repair unknown shared drift. |
| B03 | `infra/cloudformation/staging.yaml:18–78` represents buckets/queues/security group/logs, without complete isolated DB/API/compute/runtime-role wiring. P10 evidence only probes existing `/health/live`. | Read back actual resources/isolation, then qualify a synthetic isolated target with authenticated success flows and worker/media tests. Source omissions do not prove resources do not exist. |
| B04 | Artifact role permissions and upload verification disagree (`identity.yaml:53–61`; `upload-artifact.sh:21–35`). Legacy release scripts still require GitLab context despite retirement assertions. | Review deployed permissions, repair verification and prove supported GitHub-only operation/recovery. |
| M01 | `mobile-release.yml:47–59` produces `iossimulator-arm64` output, not a signed device IPA/TestFlight build. | Signed ios-arm64 candidate, endpoint/signing/SDK/entitlements/privacy provenance, physical acceptance, then controlled upload. |
| M02 | Current R06 `SecureStorageSnapshotCacheStores.cs:43–46` falls back from environment-specific keys to an environment-unknown shared legacy token. `AppEndpointConfiguration.cs:29–35` defaults Debug to Local and unspecified/invalid Release to Beta. | Fail closed for target selection; safely scope legacy migration; test account/environment contamination and actual compiled endpoint identity. |
| M03 | Public privacy page observed as publication draft; AASA URL returned generic HTML. Manifest required-reason coverage needs packaged inspection because Release code calls Preferences. | Verify/finalize policy and App Privacy, required API reasons and associated-domain JSON; distinguish capability defects from guaranteed Apple rejection. |
| M04 | Physical flows, signed archive, actual backend/schema/worker compatibility and installed-client matrix have no current candidate-bound proof in this audit. | Complete acceptance matrix below. A dated simulator pass is insufficient. |

## Ordered queue for the existing coordinator

1. **BA00/WP00:** reconcile candidate/evidence and characterize the active workout path; start independent platform/RM fixes.
2. **PD00/DS00:** prepare concrete versioned policy decisions and compatible data seams; hold only consequential unresolved prescribing changes.
3. **WP01–WP05:** coverage/time, evidence catalog, observed performance/progression, composed readiness/cycle and pain handling. WP02/WP03 use the a/b subpackages in the full plan.
4. **UX01–UX03:** complete ordered Day/All, activity-level actuals/shared logging, then media/results/history, using the full plan's a/b dependencies.
5. **RM00–RM08 / PL01–PL05:** preserve existing remediation meanings; correct endpoint/schema/release/target/privacy gates in parallel where files do not overlap.
6. **QA00/TF00/QA01:** integrated acceptance, signed device candidate and actual native/device evidence.
7. **TF01/TF02:** concrete authorized internal TestFlight, then chosen external/stability closeout.

The [full implementation plan](IMPLEMENTATION_PLAN.md) owns dependencies, subpackages, acceptance and launch scope. Preparing it is not execution authority. Broad refactors do not outrank release-blocking behavior, and core features must not be dropped to manufacture a green result. Preserve R07's completed source cleanup while reopening unmet operational acceptance.

## Full acceptance matrix

| Area | Required cases and evidence |
| --- | --- |
| Identity/onboarding | Registration, OTP/login/refresh/Apple where supported, expired/revoked tokens, persisted preferences, correct account/time zone/date. |
| Generation | Two/three muscle exposures; equipment/time/goal constraints; no eligible exercise; mixed activities; invalid/unknown inputs; deterministic output and explained limitations. |
| Acceptance | First-plan tested exception, replacement preview/Apply, acknowledgments, leases, repeat/retry, lost response, changed catalog/inputs, accepted plan preserved until valid replacement. |
| Safety/adaptation | Yes/No/Unknown/stale injury answers, pain-trigger exclusions, safe alternatives/no safe remainder, current versus inferred symptoms, unknown cycle, cycle/readiness composition, no fixture approval. |
| Progression | Complete/partial/missing actual sets; reps/load/RIR provenance; variable equipment increments, exercise swaps, skipped weeks, duplicate/out-of-order events, recovery load distinct from baseline, no mandatory increase. |
| Schedule/activity | Day/All, all sessions per date, strength→cardio→recovery, cardio-only days, account-local dates, reschedule/cancel/complete, three/four sessions, interrupted activity switching. |
| Logs/history | Strength per-set evidence and valid aggregates; running duration/distance/pace; cycling/swimming units; duration-only validity; missing optional sensors; correct activity summaries and historical corrections; idempotent saves. |
| Other core features | Nutrition/date isolation and edits, truthful micronutrient coverage, hydration failed/rapid writes, measurements, photos/media privacy, cycle uncertainty, archive/history preservation. |
| Reliability/security | Offline drafts and resync; account/environment separation; stale response suppression; server ownership; log redaction; cancellation/concurrency; deletion/revocation/media cleanup; secret/artifact review. |
| DB/backend/worker | Real disposable PostgreSQL tests, compatible schema adoption/upgrade, preservation and restore rehearsal, immutable artifacts, actual target probes, reconciliation worker, supported old-client compatibility and successful-path performance. |
| Native experience | Exact binary; crash/recovery sequence; supported iPhone sizes, EN/RU, light/dark, largest text, VoiceOver, Reduce Motion, keyboard/safe areas, gestures/background/reopen and failed saves. |
| Apple/distribution | Signed device IPA, current SDK evidence, provisioning/entitlements, privacy manifest/report, accurate policy/App Privacy, deletion/Apple auth, beta information/reviewer access/export compliance, processed build and installed smoke; external review if applicable. |

Use [G0–G9](../../VERIFICATION_GATES.md) and the current environment/layer matrix. Record exact source/tree, artifact digests, config/endpoint/schema identity, command/output, date, reviewer and remaining uncertainty. PASS requires actual evidence; NOT VERIFIED does not become PASS through a merge or checklist.

Apple requirements must be rechecked at upload: [minimum SDK notice](https://developer.apple.com/news/?id=ueeok6yw), [TestFlight overview](https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview/), [test information](https://developer.apple.com/help/app-store-connect/test-a-beta-version/provide-test-information/), [privacy manifests](https://developer.apple.com/documentation/bundleresources/privacy-manifest-files), [account deletion](https://developer.apple.com/support/offering-account-deletion-in-your-app/).

## Validation performed for this package

Read-only source/worktree comparison, existing prompt/evidence review, primary literature and Apple documentation research, and documentation/link verification. Four new Markdown documents were checked for local-link resolution and formatting; two independent reviews found no material corrections. Notion research and preference writes were fetched back. No application suite was run in this audit. Existing test counts and deployment claims remain attributed historical evidence, not newly passed checks.

Full-plan follow-up: six package documents and 23 local links were checked; no broken local targets or trailing whitespace remained. Independent product/platform reviews found no dependency cycles or critical scope gaps. Concrete prerequisite names and final-candidate target requalification were clarified. These are documentation checks, not application verification.
