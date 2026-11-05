# P12 — Beta Authority Transfer and Cutover Ledger

**Executed:** 2026-11-04  
**Authority:** Single Integration Authority (`denis-petruk/MuscleCuties`)  
**Deployment Region:** `us-east-2` (AWS Account `028417007003`)  

---

## 1. Beta Environment Classification & Data Preservation

The live Beta environment has been classified and isolated in accordance with AGENTS.md:

| Resource | Identity / Target | Protection Status | Preservation Guarantee |
| --- | --- | --- | --- |
| **Relational Database** | `musclecuties-staging-db` (Postgres 17.5, `cxi4kwwq853m.us-east-2.rds.amazonaws.com`) | Protected Beta Data Plane | **Preserved in place**. No DDL/drop/reseed executed. All tester accounts, workouts, and history remain intact. |
| **Media Storage** | `musclecuties-media-staging-028417007003`, `musclecuties-media-prod-028417007003` | Protected Object Storage | Preserved in place. Staging uses isolated bucket `musclecuties-staging-media-028417007003`. |
| **Worker Queue** | `musclecuties-workout-reconciliation-dlq` | Protected Messaging | Preserved in place. Staging uses isolated queues `musclecuties-staging-reconciliation-queue` and DLQ. |
| **API Compute** | `musclecuties-backend-api` (.NET 10, ARM64) | Active HTTP API backend | Live and healthy via API Gateway `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com/health/live`. |

---

## 2. Authority Transfer Proof

- **Previous Deployment Authority:** GitLab CI/CD (`MuscleCutiesGitLabStagingDeploy` IAM Role via `gitlab.com` OIDC).
- **New Deployment Authority:** GitHub Actions (`MuscleCuties-GitHubActions-*` IAM Roles via `token.actions.githubusercontent.com` OIDC).
- **Enforcement:**
  - Zero standing credentials: AWS access keys disabled/deleted for CI.
  - Strict OIDC subject restrictions: only `denis-petruk/MuscleCuties` repo can assume deployment roles.
  - Protected environment approval gates enforced for `beta` environment.
  - Concurrency group `mutation-beta` serializes cloud modifications.

---

## 3. Rollback & Forward-Fix Recovery Strategy

1. **API / Compute Rollback:**
   - Previous immutable ZIP versions remain permanently pinned and versioned in S3 bucket `musclecuties-release-artifacts-028417007003`.
   - Lambda function update can instantly roll back code to the prior verified `CodeSha256` or published alias version.
2. **Database Migration Safety:**
   - Database evolution follows `expand -> compatible backend -> mobile -> defer` contract.
   - Advisory session lock (`session-lock-runner`) prevents race conditions during schema checks.
   - Non-disposable databases reject destructive initialization (`IsDisposableTarget()` check).
