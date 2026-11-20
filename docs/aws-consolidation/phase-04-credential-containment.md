# Phase 4 — trust-policy scoping and credential containment

Two jobs: make the `ArtifactUpload` role assumable only from the trusted release path, and prove
the pre-rotation database password no longer survives anywhere replayable.

## Correction: the trust policy was not missing a condition

I previously reported that `MuscleCuties-GitHubActions-ArtifactUpload` had **no `sub` condition**
and could be assumed "from anywhere". That was wrong, and the mistake was in my own query: I
looked under `Condition.StringEquals`, but the condition lived under `Condition.StringLike`. The
policy always had:

```
StringLike:
  token.actions.githubusercontent.com:sub: repo:denis-petruk/MuscleCuties:ref:refs/heads/main
```

The real defect was **worse than absence in a different way**: that scope is one the legitimate
caller never presents. The `upload_artifacts` job in `backend-release.yml` declares
`environment: beta`, so GitHub issues `sub = repo:denis-petruk/MuscleCuties:environment:beta`,
never `...:ref:refs/heads/main`. The role was therefore scoped so tightly to the *wrong* value
that the real release would have been denied — a breakage waiting for the first deploy, which is
also why the workflow needed no fix: the policy was the thing that was wrong.

## What the policy is now

```
StringEquals:
  token.actions.githubusercontent.com:aud: sts.amazonaws.com
  token.actions.githubusercontent.com:sub: repo:denis-petruk/MuscleCuties:environment:beta
  token.actions.githubusercontent.com:ref: refs/heads/main
```

`sub` matches what the job actually presents; `ref` pins it to `main`, so a `beta`-environment job
from any other branch is still refused. Applied live with `update-assume-role-policy` and mirrored
in `infra/cloudformation/identity.yaml`, which still validates.

## Does the restriction actually bite?

Evaluated with IAM's own policy engine against the **deployed** Condition, action
`sts:AssumeRoleWithWebIdentity`:

| Context | Decision |
|---|---|
| `environment:beta` + `refs/heads/main` (the real release path) | **allowed** |
| `environment:beta` + `refs/heads/attacker` | **implicitDeny** |
| `ref:refs/heads/main` (the old scope, no environment) | **implicitDeny** |
| `environment:beta` from a different repository | **implicitDeny** |

A live assume attempt from a non-OIDC principal is refused outright:

```
$ aws sts assume-role --role-arn ...:role/MuscleCuties-GitHubActions-ArtifactUpload ...
An error occurred (AccessDenied) ... User: ...:user/denis.operator is not authorized to perform:
sts:AssumeRole on resource: ...:role/MuscleCuties-GitHubActions-ArtifactUpload
```

**Limitation, stated plainly.** This is not a live OIDC exchange. Minting a token signed by
`token.actions.githubusercontent.com` is impossible from outside GitHub, so the condition could not
be exercised against a real token. The evaluation uses the deployed policy document with only the
`Principal` removed (`SimulateCustomPolicy` rejects trust documents) and the effect otherwise
untouched. The first real `backend-release` run on `main` is the definitive confirmation; if
GitHub omits the `ref` claim, that job will fail and the `ref` condition is what to relax.

## Containment sweep for the pre-rotation password

Every location searched, with the outcome:

| Location | Result |
|---|---|
| repo, tracked files (`git grep`) | **clean** |
| repo, full history (`git log -S`) | **clean** — never committed |
| repo, working tree including ignored files | **clean** |
| `.env` at repo root | present (9 local dev keys), **clean** |
| `Secrets Manager: musclecuties/staging/backend-api/env` | **CURRENT clean**; the previous version `275bcd48` **held the old value** → `AWSPREVIOUS` label removed |
| `Secrets Manager: musclecuties/staging/db-role-credentials` | **clean** |
| SSM parameters | **none exist** (0 parameters in the account) |
| Lambda `musclecuties-backend-api` `$LATEST` | **clean** |
| Lambda version 22 (current `beta` target) | **clean** |
| Lambda versions **1–21** | **all 21 held the old value → all deleted** |
| rollback dump in `private-docs/aws-consolidation/rollback/` | **clean** |

Re-scan after the changes: `$LATEST` and `v22` are the only versions left, **0 stale copies**.

The 21 old versions were unreferenced (`beta` → 22) but still retrievable by qualifier and still
invokable, so deleting them was the difference between a rotation and actual containment.

## Residual, not fixable without a bigger change

Secrets Manager keeps the deprecated version blob after its labels are stripped. There is no API
to delete an individual secret version, and deleting the secret would break the running
application, so the archived bytes remain until the secret itself is replaced or deleted. It is
unreachable through `AWSCURRENT`/`AWSPREVIOUS` and requires the exact version ID. Separately, the
old value also exists in the working conversation transcript; it is dead at the database, so it
cannot be replayed.

## Live service still intact, verified after every change

```
beta alias        -> 22
/health/ready     -> 200 Healthy
/health/live      -> 200 {"status":"live","apiVersion":"v1"}
new credential    -> ok as mc_admin
old credential    -> FATAL: password authentication failed for user "mc_admin"
```
