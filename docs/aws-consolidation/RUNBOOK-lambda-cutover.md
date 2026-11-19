# Lambda cutover runbook — `musclecuties-backend-api` → `musclecuties-staging-backend-api`

Account `028417007003`, region `us-east-2`. Decision record: ADR-0006 (create, don't import).

**The one rule that makes this safe:** a published version is immutable, and an alias
serves a *published version*, not `$LATEST`. `update-function-configuration` writes to
`$LATEST` only — the alias keeps serving the old snapshot until you `publish-version` and
move the alias. A cutover that edits configuration without republishing silently changes
nothing, which is the most expensive mistake available here.

Set once:

```bash
export AWS_REGION=us-east-2 ACCT=028417007003 ENVIRONMENT=staging
OLD_FN=musclecuties-backend-api
NEW_FN=musclecuties-staging-backend-api
ART=musclecuties-staging-release-artifacts-$ACCT
KEY=migrations/lambda/musclecuties-backend-api-sb0M464m.zip
EXPECTED=sb0M464m1fWi0OFx/qYCBf/bRd4HtE76gdc1E6tkj40=
```

---

## Step 0 — preflight (read-only)

```bash
aws sts get-caller-identity                 # must be 028417007003
aws lambda get-alias --function-name $OLD_FN --name beta \
  --query '[Name,FunctionVersion]'          # expect ["beta","21"] — the known-good target
aws lambda get-function-configuration --function-name $OLD_FN \
  --query 'CodeSha256' --output text        # expect sb0M464m…
aws secretsmanager describe-secret --secret-id musclecuties/staging/backend-api/env \
  --query '[Name,LastChangedDate]'
```

Abort if the alias does not point at 21, or if the live hash differs from `EXPECTED`.

## Step 1 — prove the artifact before deploying anything

```bash
curl -sS "$(aws lambda get-function --function-name $OLD_FN \
  --query 'Code.Location' --output text)" -o /tmp/mc-fn.zip
ACT=$(openssl dgst -sha256 -binary /tmp/mc-fn.zip | openssl base64 -A)
[ "$ACT" = "$EXPECTED" ] && echo HASH_OK || { echo HASH_BAD; exit 1; }
```

Lambda reports `CodeSha256` as the base64 of the package's SHA-256, which is why the
comparison is base64 and not hex.

## Step 2 — deploy the App stack (creates the new function, no traffic yet)

```bash
aws cloudformation deploy \
  --stack-name MuscleCuties-$ENVIRONMENT-App \
  --template-file infra/cloudformation/app.yaml \
  --parameter-overrides Environment=$ENVIRONMENT \
  --capabilities CAPABILITY_NAMED_IAM \
  --no-fail-on-empty-changeset
```

The stack creates the function, a version, the `beta` alias, the invoke permission and a
**new** HTTP API. The old function and old API keep serving throughout — nothing has been
pointed at the new one yet.

## Step 3 — verify the hash of the NEW function (gate)

```bash
NEW_HASH=$(aws lambda get-function-configuration \
  --function-name $NEW_FN --query 'CodeSha256' --output text)
echo "new: $NEW_HASH"; echo "exp: $EXPECTED"
[ "$NEW_HASH" = "$EXPECTED" ] && echo 'MATCH — behaviour cannot have changed' \
                              || { echo 'MISMATCH — stop and roll back'; exit 1; }
```

A hash mismatch means the wrong object was deployed. Stop; do not proceed to Step 4.

## Step 4 — verify the alias and a live invocation

```bash
aws lambda get-alias --function-name $NEW_FN --name beta \
  --query '[Name,FunctionVersion]'

aws lambda invoke --function-name ${NEW_FN}:beta \
  --cli-binary-format raw-in-base64-out \
  --payload '{"version":"2.0","routeKey":"GET /live","rawPath":"/live","rawQueryString":"","headers":{"host":"example"},"requestContext":{"http":{"method":"GET","path":"/live"}},"isBase64Encoded":false}' \
  /tmp/new.json > /tmp/meta.json
jq -r '.statusCode, (.body // "")[:120]' /tmp/new.json
```

Expected: `404`. There is no `/live` route in the application — that 404 is ASP.NET
routing answering, which proves the function initialised, bound its handler and reached
the request pipeline. A `502` or a timeout means it did not start: check the log group
`/aws/lambda/musclecuties-staging-backend-api`. A `500` means it started but threw; check
for missing environment variables, most likely an unresolved dynamic reference.

## Step 5 — exercise the database path

The 404 proves routing, not connectivity. Confirm the DB separately:

```bash
psql "host=$(aws cloudformation describe-stacks --stack-name MuscleCuties-$ENVIRONMENT-Data \
  --query 'Stacks[0].Outputs[?OutputKey==`DbEndpoint`].OutputValue' --output text) \
  port=5432 dbname=musclecuties_staging user=mc_admin sslmode=require" \
  -c "select count(*) from information_schema.tables where table_schema='public';"
```

Then invoke once through the **new** HTTP API:

```bash
NEW_URL=$(aws cloudformation describe-stacks --stack-name MuscleCuties-$ENVIRONMENT-App \
  --query 'Stacks[0].Outputs[?OutputKey==`ApiEndpoint`].OutputValue' --output text)
curl -sS -o /dev/null -w '%{http_code}\n' "$NEW_URL/live"     # expect 404, from the app
```

## Step 6 — soak, then retire the old function

Watch for 10 minutes:

```bash
aws cloudwatch get-metric-statistics --namespace AWS/Lambda --metric-name Errors \
  --dimensions Name=FunctionName,Value=$NEW_FN \
  --start-time "$(date -u -v-15M +%Y-%m-%dT%H:%M:%SZ)" \
  --end-time "$(date -u +%Y-%m-%dT%H:%M:%SZ)" --period 300 --statistics Sum --output json
```

Then update the CI variables, because a new API means a new URL:

```bash
NEW_URL=$(aws cloudformation describe-stacks --stack-name MuscleCuties-$ENVIRONMENT-App \
  --query 'Stacks[0].Outputs[?OutputKey==`ApiEndpoint`].OutputValue' --output text)
gh variable set BETA_API_ENDPOINT --body "$NEW_URL"          # used as HEALTH_URL
gh variable set BETA_SERVER_FUNCTION --body "$NEW_FN"
gh variable set BETA_SERVER_ALIAS --body beta
```

Only after that:

```bash
aws logs create-export-task ...   # optional: archive /aws/lambda/musclecuties-backend-api
aws lambda delete-function --function-name $OLD_FN
```

Deleting the function does **not** delete its log group; 108 MB of history stays
queryable. Delete the old HTTP API (`y2ejsgrc6l`) after the variable is switched.

## Rollback

The old function and old API remain live until Step 6, so rollback is not a redeploy:

```bash
gh variable set BETA_API_ENDPOINT --body https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com
aws lambda update-alias --function-name $OLD_FN --name beta --function-version 21
```

For a bad build on the **new** function (not the name change), repoint the alias:

```bash
aws lambda update-alias --function-name $NEW_FN --name beta --function-version <previous>
```

---

## Alias republish — the standard change procedure

Every configuration change (environment variable, memory, timeout, VPC) follows this
sequence. Configuration edits alone reach `$LATEST` only; the alias keeps serving the
frozen version.

```bash
# 1. change configuration on $LATEST
aws lambda update-function-configuration --function-name $NEW_FN \
  --environment file://env.json          # never echo this file's contents

# 2. WAIT for the update to settle — publishing mid-update fails
aws lambda wait function-updated --function-name $NEW_FN

# 3. publish an immutable version and capture it
V=$(aws lambda publish-version --function-name $NEW_FN \
      --description "env change $(date -u +%Y%m%dT%H%MZ)" \
      --query Version --output text)
echo "published version $V"

# 4. move the alias
aws lambda update-alias --function-name $NEW_FN --name beta --function-version "$V"

# 5. prove it
aws lambda get-alias --function-name $NEW_FN --name beta --query '[Name,FunctionVersion]'
```

Step 2 is not optional. `publish-version` during an in-flight configuration update
returns `ResourceConflictException`, and retrying blindly can publish a half-applied
configuration.

Under CloudFormation the same rule holds: an `aws cloudformation deploy` that changes any
function property creates a new `AWS::Lambda::Version` and moves the alias, because both
are declared in `app.yaml`. Hand-edited configuration is drift and will be reverted by the
next deploy — which is the intended behaviour.

## Phase 1c coupling

The database rename changes `ConnectionStrings__PostgreSql`, which now lives in Secrets
Manager. Updating the secret does **not** change the running function: the dynamic
reference is resolved at deploy time, so the secret change must be followed by the
republish sequence above (or a stack deploy) to take effect on the alias.
