# AWS consolidation — template faithfulness record

Account `028417007003`, region `us-east-2`. Compared 2026-11-19 against live `describe`/`get`
output for every resource a template is meant to adopt. `validate-template` was also run on all
five files; it checks syntax and template shape only, which is why this record exists.

## Hash path (executed, not asserted)

Runbook step 1, verbatim:

```
downloaded base64 : sb0M464m1fWi0OFx/qYCBf/bRd4HtE76gdc1E6tkj40=
runbook EXPECTED  : sb0M464m1fWi0OFx/qYCBf/bRd4HtE76gdc1E6tkj40=
live CodeSha256   : sb0M464m1fWi0OFx/qYCBf/bRd4HtE76gdc1E6tkj40=
hex sha256        : b1bd0ce3ae26d5f5a2d0e171fea60205ffdb45de07b44efa81d73513ab648f8d
bytes             : 17232584
```

All three equal. `CodeSha256` is the base64 of the package SHA-256, so the comparison is
base64 in the runbook and hex here for legibility.

## Per-file outcome

### data.yaml — adopts RDS + 2 buckets
Matched: db.t4g.micro, postgres, 17.5, 20 GiB, gp2, encrypted, retention 1, deletion protection
true, publicly accessible, MultiAZ false, SG sg-0921332c15883d1d9, subnet group
default-vpc-02c2c07f0bf07f145 (confirmed to exist), AZ us-east-2a, auto minor upgrade, copy tags
to snapshot, Performance Insights 7-day, CA rds-ca-rsa2048-g1, parameter group default.postgres17
(confirmed to exist, family postgres17). Bucket names, lifecycle (90 d / 180 d), public access
block and SSE-S3 all match what was applied to the live buckets.
Accept-and-reconcile: (1) identifier is the post-rename `musclecuties-staging-postgres` while live
is `musclecuties-staging-db`; (2) live TagList is empty but the template declares five tags;
(3) AZ is pinned deliberately.
Not modelled (CloudFormation leaves undeclared properties alone): option group, backup and
maintenance windows, monitoring interval, IAM auth, log exports, network type.

### app.yaml — creates Lambda + HTTP API (ADR-0006)
Matched against `get-function-configuration`: dotnet10, handler MuscleCuties.Server, 3008 MB,
27 s, arm64, ephemeral 512, Zip, VPC subnets and SG, execution role, PassThrough tracing, and
**all 25 environment variable names** (set comparison: 25 template == 25 live). The env map is the
one property where a missing key is destructive, so that comparison is the guard.
By design, not adoption: the function and the HTTP API are created. `AWS::ApiGatewayV2::Api`
import requires an OpenAPI Body and then forbids Route/Integration resources, so the API cannot be
adopted as four managed resources; the URL therefore changes and `vars.BETA_API_ENDPOINT` must move
with it. Integration targets the alias ARN, matching the live `...:function:<name>:beta` shape.

### identity.yaml — adopts OIDC provider + 3 roles
Matched exactly: URL, client id list, both thumbprints; role paths, max session 3600, trust
policies, inline policy names and documents, and the live resource scopes.
**Corrected during this pass:** an earlier revision had "improved" the artifact role to the renamed
bucket and the deploy roles to the real function. That is precisely what would have imported
`NOT IN_SYNC`, so the file now reproduces live, including two defects: the artifact role still
grants PutObject on the bucket being retired, and both deploy roles are scoped to
MuscleCuties-Server-*/Reconciliation-* functions that do not exist. Both are fixed by one
`update-stack` after adoption, documented in the file.
Not adopted: the GitLab OIDC provider, still present and unmanaged.

### observability.yaml — creates topic, alarms, drift rule, RDS events
TopicName matches live; live DisplayName is empty so none is set (corrected). Alarm semantics match
the one live alarm exactly — Sum / 300 s / 1 evaluation / threshold 5 / >= / notBreaching / same SNS
action — differing only in name, which makes it a duplicate condition while the unmanaged alarm
survives. Accept-and-reconcile: delete `MuscleCuties-backend-api-Errors` once these are live.
The event rule and RDS event subscription were confirmed absent before being declared.

### governance.yaml — creates trail, budget, Config
Nothing adopts an existing resource: `describe-trails`, `describe-configuration-recorders` and
`describe-delivery-channels` all returned empty. BudgetLimit 20 USD / COST / MONTHLY matches the
live unmanaged budget exactly.
Accept-and-reconcile: a budget named `MuscleCuties` already exists, and `AWS::Budgets::Budget`
creates by name, so the stack cannot deploy until it is deleted. Left to the operator as a
destructive action outside a template write.

## Cannot be confirmed without deploying

- That CloudFormation accepts these properties for import at all (import validation is server-side).
- `IN_SYNC` after import for the three adopting stacks.
- Whether CloudTrail and Config are permitted on this account plan — RDS already returns
  `FreeTierRestrictionError`, so the same class of limit may apply.
