# P04 OIDC & Owner-Controlled Approval Boundary Ledger

Date: 2026-11-04
Recorded by: Integration Authority (Main Gemini CLI Session)
Target Branch: `platform/p04-oidc-iam`
Base SHA: `e075193e62fcfecfa9bbd41076b15e47854ecda9` (P03 Candidate)

---

## 1. CloudFormation OIDC Template

- **Template Path:** `infra/cloudformation/identity.yaml`
- **Validation:** Validated using `aws cloudformation validate-template` (Account `028417007003`, User `denis.operator`).
- **Provider URL:** `https://token.actions.githubusercontent.com`
- **Audience:** `sts.amazonaws.com`

---

## 2. Least-Privilege Role Architecture

| Role Name | Trusted Subject | Permissions | Blast Radius Limits |
|---|---|---|---|
| `MuscleCuties-GitHubActions-ArtifactUpload` | `repo:denis-petruk/MuscleCuties:ref:refs/heads/main` | `s3:PutObject` to release bucket | Cannot read or delete objects; cannot invoke Lambda; no DB access. |
| `MuscleCuties-GitHubActions-StagingDeploy` | `repo:denis-petruk/MuscleCuties:environment:staging` | `lambda:UpdateFunctionCode`, `PublishVersion`, `UpdateAlias` on Staging functions | Strictly scoped to `*-Staging`; default-deny to Beta resources. |
| `MuscleCuties-GitHubActions-BetaDeploy` | `repo:denis-petruk/MuscleCuties:environment:beta` | `lambda:UpdateFunctionCode`, `PublishVersion`, `UpdateAlias` on Beta functions | Gated behind protected GitHub environment; requires explicit owner promotion. |

---

## 3. Negative Policy Verification

- Verified via `tests/MuscleCuties.Deployment.Tests/test_oidc_policy.py`:
  - OIDC client ID restricted to `sts.amazonaws.com`.
  - Artifact upload role restricted to `main` branch pushes.
  - Staging deploy role strictly restricted to `staging` environment and denied access to Beta Lambda functions.
  - Beta deploy role strictly restricted to `beta` environment.
