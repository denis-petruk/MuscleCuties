# P00 Baseline, Ref, Data & Authority Ledger

Date: 2026-11-04
Recorded by: Integration Authority (Main Gemini CLI Session)
Baseline Repository: `/Users/denispetruk/RiderProjects/MuscleCuties`
Integration Worktree: `/Users/denispetruk/.gemini/antigravity/worktrees/MuscleCuties/execute_gemini_cli_roadmap`

---

## 1. Reference & Commit Integrity

| Ref | Type | SHA | Commit Subject |
|---|---|---|---|
| `docs/github-transition-governance-20261104` | Local / Origin | `acdb131140dec58c4625132682982b7baba4a4c7` | `docs: add full-roadmap and Gemini CLI execution prompts` |
| `docs/github-transition-governance-20261104~1` | Base SHA | `f6fbd32ee87d9ed250d61be4ed982f13a6aa824c` | `docs: publish architecture plan and shared development files` |
| `restore` | Local branch | `fe350c4fd0026f18608f441fe2deb5dd3a3d5b86` | `forwardign the latest changes` |
| `origin/main` | Remote branch | `ff3be300b5a769c1e0ccd1c5c3fa18d2d8626224` | `Merge the final dead-surface cleanup slice` |
| `gitlab/test-flight-beta` | Remote branch | `c8ffbccb031ea0e137ce1247906a6bf770080d9f` | `Record the version 10 release that drops stale readiness adjustments` |
| `gitlab/codex/plan-generation-checkpoint` | Remote branch | `47fd713064fcf6f79e2acd894e636817952816a9` | `Restore the V02 planning rules as kept domain logic` |

Common ancestor of `origin/main` and `restore`: `60f9b8d829eb7f1aeb16551cddc210e2b24737af` (279 restore-only commits, 65 main-only commits).

---

## 2. Immutable Bundle Archive Verification

- **Archive File:** `mc-baseline-all.bundle`
- **Location:** `/Users/denispetruk/.gemini/antigravity/brain/4e842cd8-5086-4a9e-b7e6-8adc78683df1/scratch/bundles/mc-baseline-all.bundle`
- **Size:** 12,279,795 bytes
- **SHA-256 Digest:** `22d348b006c9637399d71364855a56213711e69522631e9e1e78fe51558d00ff`
- **Bundle Contents:** 23 refs, full commit history.
- **Verification:** `git bundle verify` passed with status 0.
- **Recovery Rehearsal:** Successfully cloned into `/tmp/test-bundle-clone` without network access; commit trees and logs match exactly.

---

## 3. Data & Resource Classification

- **Beta Data Plane:** Classified as **valuable tester data**. Contains live workout planning history, user preferences, hydration, body measurements, and active plans. Under no circumstances may Beta data be cleared, reseeded, reset with `EnsureCreated`, or mutated without an explicit, approved, additive migration.
- **Staging Data Plane:** To be provisioned as a completely isolated environment with synthetic data only. No tester data will be cloned to Staging.
- **AWS Environment Context:**
  - Active IAM caller: `arn:aws:iam::028417007003:user/denis.operator`
  - Account: `028417007003`

---

## 4. Authority & Governance State

- **Current Deploy Authority:** GitLab CI (legacy) and authenticated local operator.
- **GitHub Branch Protection:** Checked via GitHub API (`gh api repos/denis-petruk/MuscleCuties/branches/main/protection`).
  - **Status:** Unprotected (`HTTP 404: Branch not protected`).
  - **Prerequisite Gate for P01 Merge:** Owner must configure minimal PR-only protection on GitHub `main` (disallow direct pushes, require PR with review) before P01 reconciliation PR can be merged.
