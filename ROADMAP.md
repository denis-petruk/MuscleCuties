# Roadmap — MuscleCuties

Updated 2026-11-08. Short sequence of remaining work; evidence stays with its owning audit, gate register or runbook. Release state and workout policy have one source of truth: this roadmap for sequence, `STATUS.md` for verified state, `docs/PRODUCT_WORKFLOWS.md` for user-visible policy and `docs/architecture/github-transition/execution/CONTROL.md` for package lifecycle.

## Where we are

The GitHub transition (Plan I P00–P13, Plan II R01–R07) is complete and merged. The corrective beta plan is implemented through the five first-beta packages, which are merged at `4ee9b4f2` (PR #39; the package work landed in PR #38) and locally verified only:

1. **B1 Provisionable schema** — frozen model baseline, ordered `migrate`, `seed`, `dbcheck`, one role model, and an empty-database CI rehearsal. A fresh database now provisions without `EnsureCreated`.
2. **B2 Honest release** — GitHub OIDC deployer for both Lambda functions by immutable digest, report written only after serving digests match, recorded rollback target, and an explicit rollback workflow mode. Beta-only; the mobile workflow builds `beta` only and no Staging job, endpoint or allowlisted host remains.
3. **B3 Real training capture** — per-set rows with RIR and technique feed `ObservedSets`; row-less timed sessions send activity actuals; progression uses the real tier and muscle group with EN/RU copy.
4. **B4 Recovery and preview** — prerequisite recovery CTA, all limitation reasons disclosed at once, weekly coverage/volume summary, per-movement rationale and timed-phase detail.
5. **B5 Consistency and docs** — fasting and exercise media explicitly deferred, canonical docs reconciled to the tree.

## Remaining sequence

### 1. Owner review of the five packages
Review the working-tree diff per package. B1 and B2 are platform-owned; B3 and B4 product-owned; B5 integration-authority-owned. No package is `SAFE TO PROMOTE` from local checks alone.

### 2. Authorized external gates (blocked until owner authorization)
- Signed `ios-arm64` archive with verified SDK and provenance, then TestFlight internal upload.
- QA01 physical-device acceptance: themes, EN/RU, accessibility, gestures, strength and timed flows.
- Qualification on a genuinely isolated Staging API and database, including the empty-DB `migrate` + `seed` rehearsal there.
- One real Beta deployment followed by a negative-path run and a live alias rollback rehearsal.
- Owner approval tied to source SHA, artifact digest, schema version, environment and rollback plan.

### 3. Post-approval operations
Merge through a reviewed PR into protected `main`; promote the exact verified backend/worker package; distribute the mobile build through TestFlight; record measured outcomes against the evidence fields in `AGENTS.md`.

## Explicitly deferred (not half-shipped)

- **Fasting:** domain, use case and storage exist with no client surface. Not presented as available.
- **Exercise media:** no image or video player; text guidance only, with no broken placeholder to fix.
- **Android targeting:** not added; iOS-only stands.
- **Health synchronization:** dormant unless separately authorized.
