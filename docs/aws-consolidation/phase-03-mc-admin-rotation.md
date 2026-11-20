# Phase 3 — `mc_admin` rotation

The staging database master credential had leaked: a plaintext copy entered the working
transcript. It is also the credential the live Lambda authenticates with, so rotating it is a
coordinated change — rewrite the secret, rotate in the database, publish a version, move the
alias. Because a published Lambda version is immutable, **skipping the republish leaves the alias
running on the old credential**, which is the failure this document exists to prevent.

## Performed 2026-11-19

Ordered so that nothing breaks before the switch:

1. **Secret staged.** `musclecuties/staging/backend-api/env` rewritten with a 48-hex-character
   password (`openssl rand -hex 24`; hex avoids `;` and `=` so the connection string stays
   parsable). All 8 keys preserved — verified 8 before, 8 after. The running alias was unaffected.
2. **Function environment staged on `$LATEST`** with the new connection string. The alias still
   served frozen version 21, so still no customer-visible change.
3. **Database rotated** with `rds modify-db-instance --master-user-password --apply-immediately`,
   the supported path — it keeps RDS's own record of the master credential in step with
   `pg_authid`, which a bare `ALTER ROLE` would not.
4. **Version published and alias moved**: version **22**, `beta` → `22`. This is the step that
   makes the rotation real.
5. **GitHub secret updated** (`DATABASE_CONNECTION_STRING`) from Secrets Manager without the value
   ever being printed, so `db-release.yml` keeps working.

Host, database and username were deliberately unchanged — this rotates a credential, it does not
repoint anything.

## Evidence

| Check | Result |
|---|---|
| Alias target | `beta` → `22` |
| Version 22 holds the new password | yes (matched in-memory, value never printed) |
| Old credential | `FATAL: password authentication failed for user "mc_admin"` |
| New credential | `authenticated as mc_admin on 17.5` |
| Data visible | 76 tables across `app`/`catalog`/`ops`/`public` |
| `/health/live` | `200 {"status":"live","apiVersion":"v1"}` |
| `/health/ready` | `200 Healthy` |
| Lambda log auth errors since rotation | none |

`/health/ready` returning `Healthy` is meaningful: readiness does not pass unless the function
can reach the database, so it is end-to-end proof that the new credential works through the alias.

## Correction: the health routes do exist

Earlier in this work I stated that `/health/live` and `/health/ready` do not exist and that the
health gate had to be replaced with a 404 check. **That was wrong.** Both routes are served today
and return 200. The earlier conclusion came from probing with a hand-written event payload that
was not a valid API Gateway v2 request, which surfaced as a `NullReferenceException` in the
ASP.NET Lambda host rather than as a routing result. Probing the real HTTP endpoint settles it.

Consequence: the post-deploy smoke check in `backend-release.yml`, which requires both routes to
return 200, will pass and does not need to be reworked.

## Known single error in CloudWatch

`AWS/Lambda Errors` shows one datapoint, attributable to my own malformed test invoke (a
`NullReferenceException` at the timestamp of that call), not to the rotation. No authentication
failures appear in the logs.

## Not covered

- The `mc_workout_*` role passwords were rotated in Phase 0.2 and are unaffected by this change.
- No worker Lambda exists, so no second function needed a version bump.
- The credential appears in no commit and no file; the temporary file holding the new password
  during verification was deleted.
