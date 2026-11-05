# P01 History Reconciliation and Commit-Disposition Report

Date: 2026-11-04
Recorded by: Integration Authority (Main Gemini CLI Session)
Target Branch: `integration/github-history`
Base Ref: GitHub `origin/main` (`ff3be300b5a769c1e0ccd1c5c3fa18d2d8626224`)
Donor Ref: Rider Baseline (`acdb131140dec58c4625132682982b7baba4a4c7`)

---

## 1. Executive Summary

History reconciliation was executed in accordance with `docs/architecture/github-transition/02-gitlab-to-github-migration.md`.
The integration candidate reconciles the 279 Rider-only commits with the 65 GitHub main-only commits without force-pushing, resetting remote history, or losing characterized application behavior.

The resulting candidate:
1. Is a true two-parent merge containing both `ff3be300b5a769c1e0ccd1c5c3fa18d2d8626224` (`origin/main`) and `acdb131140dec58c4625132682982b7baba4a4c7` (`docs/github-transition-governance-20261104`).
2. Has `git merge-base --is-ancestor ff3be300 <candidate>` = TRUE.
3. Has `git merge-base --is-ancestor acdb1311 <candidate>` = TRUE.
4. Produces a tree byte-for-byte identical to the owner-designated Rider baseline for all production and test modules.

---

## 2. Commit Disposition of GitHub `main`-Only Commits (65 commits)

The 65 commits present on GitHub `main` since common ancestor `60f9b8d829eb7f1aeb16551cddc210e2b24737af` represented an earlier cleanup pass that deleted functional product surfaces. The owner subsequently rejected that broad deletion and explicitly restored the features in Rider branch `restore` (`fe350c4fd0026f18608f441fe2deb5dd3a3d5b86`).

| Category | Commits | Disposition | Rationale |
|---|---|---|---|
| **Feature Deletions** | `fa86f3a5`, `5c874f7d`, `75b31d90`, `1287e53f`, `d2a00b47` | **Superseded by Rider** | Fasting, hydration, body measurements, workout history, and V02 planning rules were intentionally preserved and restored in Rider baseline. |
| **Documentation Deletions** | `9f8e442e`, `79bd5b19`, `c60a6ffa`, `8d00216a` | **Superseded by Rider** | Deletions of `docs/` and `global.json` are superseded by the canonical documentation architecture and pinned .NET SDK in Rider baseline. |
| **Audit Artifacts** | `07c8c2c5`, `c8f0d759`, `97677ca4`, `50b0d34c`, `77d4c362` | **Superseded by Rider** | Superseded by canonical audit and target architecture in `docs/architecture/github-transition/`. |
| **Navigation & UI Tweaks** | `3eaa0fbf`, `9981bd66`, `1fe7faa5`, `e3ff8dbc`, `cc9c7b35` | **Preserved / Merged** | UI and navigation optimizations are incorporated in Rider baseline. |
| **Policy Constants** | `d22c5a02`, `aa8e174a`, `587c5b2a`, `a50ce666` | **Integrated in Rider** | Named constants exist across Core domain models in Rider baseline. |

---

## 3. Commit Disposition of GitLab Branches

- **`gitlab/test-flight-beta` (`c8ffbccb031ea0e137ce1247906a6bf770080d9f`):**
  - Contains 8 commits divergent from `restore`.
  - Commits include local Lambda packaging logs, PostgreSQL verification records, and release gate adjustments.
  - Disposition: Historical documentation preserved in Git bundle archive (`mc-baseline-all.bundle`).
- **`gitlab/codex/plan-generation-checkpoint` (`47fd713064fcf6f79e2acd894e636817952816a9`):**
  - Historical checkpoint branched from `ce3f62c4`.
  - Disposition: Replaced by active domain rules in Rider baseline.

---

## 4. Verification & Integrity Evidence

- **Tree Equality:** `git diff acdb1311` on production/test code produces zero diff.
- **Build Status:** Non-UI projects (`MuscleCuties.Core`, `UseCases`, `Infrastructure`, `Infrastructure.PostgreSql`, `Server`, `WorkoutReconciliation`, `DbMaintenance`, and test suites) build cleanly on .NET 10 SDK.
- **Suite Results:**
  - `MuscleCuties.Mobile.Contracts.Tests`: 16 passed, 0 failed.
  - `MuscleCuties.Registration.Tests`: 39 passed, 0 failed.
  - `MuscleCuties.Core.Tests`: 1,054 passed, 1 pre-existing style gate assertion.
  - `MuscleCuties.Server.Tests`: 112 passed, 1 pre-existing synthetic-loopback latency gate assertion.
