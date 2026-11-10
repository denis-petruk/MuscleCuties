# Roadmap — MuscleCuties

Updated 2026-11-10. Short sequence of remaining work; evidence stays with its owning audit, gate register or runbook. Release state and workout policy have one source of truth: this roadmap for sequence, `STATUS.md` for verified state, `docs/PRODUCT_WORKFLOWS.md` for user-visible policy and `docs/architecture/github-transition/execution/CONTROL.md` for package lifecycle.

## Where we are

The corrective beta plan and simulator QA gap closures are implemented and merged into `main` at `4b4847ee` (PR #48), extended by commit `bd185f5d` (HealthKit multi-metric sync) and `b6a509a6` (USDA FoodData Central integration):

1. **B1 Provisionable schema** — frozen model baseline, ordered `migrate`, `seed`, `dbcheck`, one role model, and an empty-database CI rehearsal.
2. **B2 Honest release** — GitHub OIDC deployer for both Lambda functions by immutable digest, report written only after serving digests match, recorded rollback target.
3. **B3 Real training capture** — per-set rows with RIR and technique feed `ObservedSets`; progression uses real tier and muscle group with EN/RU copy.
4. **B4 Recovery and preview** — prerequisite recovery CTA, limitation reasons disclosed at once, weekly coverage/volume summary, per-movement rationale and timed-phase detail.
5. **B5 Consistency and docs** — fasting and exercise media explicitly deferred, canonical docs reconciled to the tree.
6. **B6 Simulator QA Gap Closures (PR #48)** — session lifecycle debounced, resume at first unlogged exercise, explicit session abandonment modal, idempotent modal accessibility restoration, per-set `WasEdited` integrity, archive `CompletedAt != null` filter, dashboard truth, reinstall Keychain cleanup, photo fallback, and UI polish.
7. **B7 HealthKit Multi-Metric Sync (`bd185f5d`)** — authorized Apple Health integration for steps, sleep stages, active kcal, resting HR, and HRV; Daily Check-In pre-fill; rolling 7-day step trend caloric target adjustment.
8. **B8 USDA FoodData Central (`b6a509a6`)** — USDA food database search and macro calculation integration.

## Remaining sequence

### 1. TestFlight Build #3 Packaging & Distribution
- Increment application version (`<ApplicationVersion>3</ApplicationVersion>`).
- Build and archive iOS binary in Xcode / App Store Connect with distribution profile.
- Upload to TestFlight internal testing group.

### 2. External Beta Deployment Gates (owner-authorized)
- CloudFormation observability stack deployment (`observability.yaml`).
- 14-day RDS backup and point-in-time recovery rehearsal drill.
- Dedicated `beta` alias promotion and rotation of secrets in AWS Secrets Manager.

## Explicitly deferred (not half-shipped)

- **Fasting:** domain, use case and storage exist with no client surface. Not presented as available.
- **Exercise media:** no image or video player; text guidance only, with no broken placeholder to fix.
- **Android targeting:** not added; iOS-only stands.
- **In-session pain reporting (S-04):** deferred to post-beta update.

