# ADR-0006 — The App stack creates the API function; it does not import it

- Status: accepted
- Date: 2026-11-19
- Decision owner: operator
- Supersedes: the open question in brief §8.2

## Context

`musclecuties-backend-api` is live, is the only function in the account, and is owned by no stack.
The App stack must own it, and decision D4 requires the name `musclecuties-staging-backend-api`.
Two mechanisms can put it under CloudFormation:

- **import** the existing function, or
- **create** a new one from the preserved package and retire the old.

Facts established this run (2026-11-19), all verified rather than assumed:

1. `AWS::Lambda::Function`, `::Alias`, `::Version` and `::Permission` **do** support import
   (CloudFormation "Resource type support" table: Import = Yes, Drift = Yes, IaC generator = Yes).
   Brief §8.2's "unknown" is resolved.
2. **An import does not compare the template to the live resource** — it validates only that the
   resource exists, that the template is schema-valid with required properties present, and that the
   resource is not already in another stack. "CloudFormation doesn't check that the template
   configuration matches the actual configuration of resource properties."
3. **An import never changes a property** — "Import operations don't allow new resource creations,
   resource deletions, or changes to property configurations."
4. **Drift only tracks properties explicitly set in the template.** "CloudFormation only determines
   drift for property values that are explicitly set, either through the stack template or by
   specifying template parameters."
5. **Drift cannot map Lambda source code.** CloudFormation "cannot map the source code of a Lambda
   function back to the Code property", so code is outside drift coverage entirely.

## Decision

**The App stack creates `musclecuties-staging-backend-api`.** The live function is preserved
byte-for-byte, the new one is created alongside it, the alias and API integration are repointed,
and only then is the old function removed.

## Why — and why import loses

**Import cannot deliver the rename.** The function name is its identity; changing it is a
replacement. Importing therefore means keeping `musclecuties-backend-api` permanently, failing D4.
That is not a stylistic objection: the whole mission is coherent `musclecuties-staging-*` naming,
and the function is the resource every other resource in the account points at (API integration,
alarm, log group, deploy roles).

**A partially-described import is unsafe *and* undetectable.** Facts 2–4 combine into the trap:
import accepts a template that omits `Environment.Variables`, the import silently succeeds, drift
then reports **`IN_SYNC`** because omitted properties are not tracked — and the next unrelated
update reconciles the function to the template and **deletes all six secret environment variables**.
There is no alarm, no drift signal, and the failure appears as a runtime authentication outage.
Importing safely therefore *requires* fully declaring `Environment`, secrets included.

**The secrets problem is identical on both paths, so it is not a reason to prefer import.**
Whichever mechanism we choose, `Environment.Variables` must be expressible without putting the DB
password, `Resend__ApiKey`, the signing key and the two PEM private keys into a repository file.
The only hygienic way to declare them in a template is a Secrets Manager reference resolved at
deploy time (`{{resolve:secretsmanager:…}}`). Once that exists, the template can *create* the
function from nothing — import has no remaining advantage.

**Import would be a registration, not infrastructure as code.** A stack that can only adopt a
pre-existing function cannot rebuild this account. That defeats the stated success criterion
("every live resource is declared"): declared must mean *declarable from empty*. The template has
to be creatable anyway; if it is creatable, creating it here is strictly better than adopting it.

**A Lambda function is safe to replace; a database and a bucket are not.** The function holds no
durable state. Its code is a verifiable artifact (hash-pinned), its configuration is fully
enumerated in the baseline, its logs live in CloudWatch and survive, and its published versions are
disposable precisely because the exact code is preserved. This is the inverse of RDS and S3, where
import is mandatory because replacement destroys data.

### The general rule this run adopts

> Import stateful or authoritative resources, where replacement destroys data.
> Create stateless, artifact-reproducible resources, where replacement is cheap and naming is
> part of the goal. Rename only by replacement, and only *before* a stack owns the resource.

Under that rule: RDS, S3 buckets, IAM roles and the OIDC provider → **import**.
Lambda function and alias → **create**. SNS topic and alarms → recreate (already decided: the topic
name is account-unique and the current stack is drifted).

## Preserving it — the concrete "save" plan

Verified already: the live package was fetched from the presigned `Code.Location`, is
**17,232,584 bytes**, and its SHA-256 (base64) is `sb0M464m1fWi0OFx/qYCBf/bRd4HtE76gdc1E6tkj40=` —
an exact match to the published `CodeSha256` of version 21, which alias `beta` serves.

1. Upload that zip to `musclecuties-staging-release-artifacts-…/migrations/lambda/` as the immutable
   build artifact the new stack deploys from. Note: the Data stack's 90-day lifecycle rule applies to
   this prefix — either exempt it or accept that CI re-uploads deploy artifacts after 90 days.
2. Move the six secret values into one Secrets Manager secret via a script that never prints them,
   and have the template reference them with dynamic references. No secret enters the repository.
   *Cost: one secret, $0.40/mo. This is the only scope addition this decision needs.*
3. Keep the current function **alive and serving** until the new alias has taken traffic.
4. Retain the old log group (`/aws/lambda/musclecuties-backend-api`, 108 MB) so history stays
   queryable after the function is gone. Deleting a function does not delete its log group.
5. Verify the new function's `CodeSha256` equals `sb0M464m…` **before** repointing anything — a hash
   match is the proof that behaviour cannot have changed.
6. Rollback: the old function and the old integration target remain valid until step 7. Reverting is
   one `update-integration` plus one `update-alias`.
7. Delete the old function only after a soak window with zero errors.

## Consequences

- Positive: D4 satisfied; the stack can recreate the function in a fresh account; `Environment` is
  fully drift-covered; secrets leave the template entirely.
- Negative: a short cutover window; published versions 1–21 are lost with the old function (the code
  is preserved by hash, so this is cosmetic); one Secrets Manager secret is added.
- Constraint discovered: **alias `beta` serves a frozen published version.** Any environment change —
  the new secrets references *and* the Phase 1c DB host switch — needs `publish-version` followed by
  `update-alias`. Editing configuration without republishing the alias silently changes nothing.

## Still open

Whether the existing **HTTP API** (`y2ejsgrc6l`) can be imported with its stage, route and
integration, or must be recreated. Recreating changes the API URL, which is referenced by
`vars.BETA_API_ENDPOINT` in CI. This is a separate decision for `app.yaml` and does not affect the
Lambda decision above.
