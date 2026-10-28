# Workout migration implementation — 2026-10-28

## Estimated phase and future-day correction

- Safe branch: `codex/estimated-phase-future-reconciliation-20261028` from GitLab checkpoint `79eb8f0`. This branch is outside the CI push/deploy workflow rules.
- The workout-specific phase estimate uses four recorded starts with linked menstrual anchors, three valid completed intervals, and agreement of the projected phase across every observed interval. It refuses an estimate on phase disagreement, an expired interval, an invalid or ended cycle, or a reproductive context that makes ordinary phase prediction unreliable. A linked current-day confirmed phase has priority. Existing unanchored manual cycle starts cannot be distinguished from inferred starts and retain normal dosage pending explicit provenance support.
- Cycle, symptom, check-in, and reproductive-context saves commit their data and pending state before a separate account-locked reconciliation attempt. The reconciler now processes unstarted current and future plan days with each day’s local date and phase provenance. It leaves active previews/sessions pending, preserves completed/logged work, and avoids using today’s check-in as a future-day observation. Future overlays are read by the schedule and revalidated at session start.
- Focused cycle Core tests: 33 passed. Current shared Windows tree: Core 1004, Server 82, Registration 41, Mobile contracts 9, and catalog import 3 passed. The shared tree also contains unrelated uncommitted nutrition work. PostgreSQL-backed cycle transaction tests require an explicitly disposable connection and did not run. iOS build was attempted and stopped at `NETSDK1147` because `maui-ios` is unavailable in this Windows workload set. No deployment or live migration was performed in this continuation.
- This corrects the prior completion language below. Database transaction, iOS, and authenticated device acceptance remain open.

## Historical production-hardening source checkpoint

On historical task branch `codex/workout-cycle-readiness-production-hardening-20261028`, source
work covered these workout-planning paths. The later estimated-phase and future-day correction
above supersedes the earlier verification claim:
- **Population scope**: Restricts general-fitness generation to adults aged 18–64; fails closed on pregnancy or invalid local date (`WorkoutPopulationScopeRule`).
- **Personalized cycle adaptation**: Implements evidence-informed phase personalization across all four phases (Menstrual, Follicular, Ovulatory, Luteal). Retains normal training dosage when no individual pattern exists (avoiding universal follicular high / luteal low prescriptions or ovulation risk assumptions). Evaluates individual pattern through bipartite matching across $\ge 3$ distinct cycles with trusted completed workouts within 180 days (`CycleReadinessPrescriptionRule`, `PostgresCyclePersonalizationEvidence`).
- **Daily readiness integration**: Unifies same-day check-in ratings (sleep, soreness, energy/fatigue, stress/mood, symptoms) into one bounded adjustment, preventing double-counting symptom fatigue through both cycle and readiness subsystems (`PostgresCycleReadinessReconciler`).
- **Safety precedence & injuries**: Fails closed to safety review on new pain or worsening symptoms (`CycleReadinessSafetyRule`). Preserves active injury substitutions, durable omitted-slot lineage records (`WorkoutInjuryOmittedSlotRecord`), and handles 7-day clearance optionality (Day 1 clearance date, Day 8 required if safe) and empty-session suppression (`PostgresWorkoutPlanReconciler`).
- **Volume floor & duration choices**: Implements fallback floor of 1 working set per available required safe movement slot (`TemplateVolumeFloorRule`), offers longer session or shorter plan with explained omissions, and prioritizes lower-body movements when shortening full-body sessions.
- **Verification**: Core unit test suite passes 966/966; Mobile contracts suite passes 8/8. No staging deployment was triggered.

## Current macOS continuation and pull recovery

The current checkout was fast-forwarded from `b482b9dd` to GitLab `274558d7` after
an authenticated fetch. Nineteen untracked source files were copied and SHA-256
verified in `/private/tmp/MuscleCuties-pull-recovery-20261028-20261028-58257-u52d9k`.
Eight incoming-path collisions were moved aside before the fast-forward; differing
local versions remain in the recovery folder. Superseded disconnected style-offer
and duplicate HIIT drafts are preserved there rather than compiled beside the
connected implementation. No tracked user changes were discarded.

Implementation continues on `codex/workout-cycle-readiness-production-hardening-20261028`.
The checked-in GitLab workflow permits pipelines only for the explicitly named
historical checkpoint and beta branches; this task branch cannot trigger a
deployment. No deployment, live migration or scheduler activation is authorized.

Fresh local PostgreSQL 17 migration and catalog preparation succeeded in an
isolated loopback-only container. The current broad Core/application run passed
964 tests. Targeted PostgreSQL suites have verified saved readiness overlays,
session replay, pending-update retries, account purge and automatic acceptance;
full integrated verification is still underway. Historical Windows/toolchain
statements below describe earlier checkouts. Model snapshot reconciliation,
fresh-schema verification, all-suite results and the Mac iOS build remain open.

## Safe task-branch continuation

- Branch `codex/workout-cycle-readiness-production-hardening` was created from clean `effec70`. The GitLab workflow rules run push pipelines only on `codex/plan-generation-checkpoint`; its staging deploy job is manual. This branch does not trigger that pipeline.
- A feasible preview now shows a specific explanation when Apply is disabled because the proposal was not persisted or its lease became unavailable. A stale preview directs the user to make a fresh one. Existing Edit details remains available.
- Mobile contracts: 9 passed. The edited generation XAML parses as XML. The iOS Release build was attempted and stopped at `NETSDK1147`: the `maui-ios` workload is absent from this Windows workload set. No PostgreSQL disposable connection is configured.
- This is a narrow UI increment. The implementation and release gates below remain open.

## Checkout and ownership

- Windows checkout on `codex/plan-generation-checkpoint`. An authenticated fetch reached GitLab. Local `be44260` and remote `b482b9d` diverged by two commits each; the existing merge was preserved and its four conflicts reconciled. The 2026-10-27 checkpoint retains both prior environment reports as historical records.
- Generation owner: Core/UseCases scheduling, safe remainder and Core tests. Safety owner: injury reconciliation and style-offer persistence. Mobile owner: UI merge and style-offer selection. Orchestrator: server wiring, integration review, tests, documentation and Git operations.
- The remote side's style-offer registration and endpoint references targeted types absent from this checkout. The existing `IStrengthStyleOfferStore`, `PostgresStrengthStyleOfferStore` and planning endpoints are the connected implementation.

## Implemented and reviewed in this checkout

- Canonical preview can carry a safe non-strength remainder and rejects an empty proposal. Same-day demanding yoga and climbing adjustments are validated and serialized as occurrence prescriptions in accepted plan days, active-session snapshots and session reads. Other demanding pairings still lack reviewed dose rules and remain restricted.
- The injury reconciler now accepts a reviewed replacement in the same proven slot and dose after clearance, retains original and episode lineage and the optional deadline, and rejects duplicate occurrence matches. Unknown legacy lineage, omitted slots and mixed active/cleared episode cases still defer.
- Successful automatic acceptance can create a revision-scoped one-time offer for another fitting selected strength style. The workout UI can select an offered style and open its preview. The dispatcher host remains explicitly invoked; credentials and external schedule are not configured.
- The merged CI deploy job is manual. No staging or production migration, backfill, scheduler change or deployment was run.

## Verification

- Core/application: **931 passed**. Server: **82 passed**. Registration: **41 passed**. Mobile contracts: **9 passed**.
- Registration integration project compiled. Its full run had **16 passed, 41 failed**: most database cases require `MC_REGISTRATION_WRITE_TEST_POSTGRES`, which is absent. Two database-independent fixtures with stale expectations were corrected and their focused six-test run passed. There is no configured disposable PostgreSQL service or Docker/Podman executable here; transaction, concurrency, isolation and catalog coverage gates remain open.
- `net10.0-ios` build was attempted and stopped at `NETSDK1147`: the `maui-ios` workload is absent. App/XAML compilation and signed-in device behavior remain unverified. No other platform build was run.
- `git diff --check` passed. The resolved conflict files have no conflict markers. No secrets or unrelated untracked files were included in the reviewed diff.

## Follow-up source pass

- Merge commit `ce138fd` was pushed to `origin/codex/plan-generation-checkpoint` successfully. The source changes below are a subsequent update.
- Injury preflight now accepts a complete snapshot for every active episode, including unaffected exercise rows and overlapping active episodes. Unrelated cleared history may coexist; an episode affecting a selected original still defers for restoration. Incomplete or conflicting lineage remains blocked.
- A first-plan preview with no safe scheduled session offers Explore other features alongside Edit. Required-answer and strength-style-choice preflight remain on their questionnaire paths. Style-offer actions display a retry message on failure.
- A durable omitted-slot lineage record is still needed because the present record requires an exercise row. A safe addition needs tenant isolation, a scheduled-date/template/session/slot identity, original dose and causal episode IDs, transactional acceptance/transfer, and PostgreSQL rollback and concurrency verification. No speculative migration was added.
- The catalog import snapshot tests pass, but its review manifest disables automatic substitutions. Canonical logs lack observed RIR, technique outcome and validated weekly underperformance evidence required by the retained legacy load progression and deload rules. Those behaviors remain gated.
- After this pass: Core/application **931**, Server **82**, Registration **41**, Mobile contracts **9**, and nine database-independent integration tests passed. The integration project compiles. Three selected injury database cases could not run because the disposable PostgreSQL connection is absent. `WorkoutPage.xaml` parses as XML; iOS compilation remains blocked by the missing workload.

## Remaining source and release gates

1. Implement active-injury safe replacement and omission lineage, empty unsafe-session handling, mixed injury histories and safe restoration for ambiguous legacy rows without inventing metadata or losing logged work.
2. Integrate observed-set, recovery and deload inputs from the retained legacy generator into canonical revisions; review remaining reachable legacy paths before removing any code.
3. Review complete catalog and high-intensity alternatives for all supported onboarding choices. Verify a real preview/Apply limitation response and deployed source revision; the historical disabled Apply observation still lacks those facts.
4. Run synthetic disposable-PostgreSQL rollback, stale preview, takeover, concurrent Apply, exact retry, injury, automatic reconciliation and account-isolation scenarios. Run an iOS build on a configured Mac/Xcode host and signed-in EN/RU device flow.
5. Provision the restricted discovery and tenant credentials, an external dispatcher schedule and monitoring only in a separate deployment task after source and verification gates. Then review additive migrations, deploy matching backend/client, and verify revision parity before rollout.

This is an implementation checkpoint. The migration is neither complete nor deployed.
