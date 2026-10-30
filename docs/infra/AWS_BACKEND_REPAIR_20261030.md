# AWS backend repair follow-up — local verification

Date: 2026-10-30. Branch: `infra/aws-lambda-postgres`.

This follow-up records work in the current local checkout and disposable PostgreSQL 17 databases. No AWS database, Lambda, deployment, alias, or pipeline was changed. The prior read-only audit recorded 31 of 59 source migrations applied in the AWS database; that state was not rechecked during this local run.

## Repairs

- Removed the production S3 bucket name from both server appsettings files. All photo S3 adapters now require explicit `Storage:S3:BucketName` and `Storage:S3:Region`; the old `musclecuties-media` and `us-east-1` fallbacks are gone. Photo cleanup validates the bucket before claiming database work.
- The PostgreSQL planning source now omits timed exercises with `SecondsPerRep = 0` from the per-repetition timing map. This prevents a valid timed exercise from making the complete planner profile fail its positive per-rep validation. The local planning persistence test covers that contract.
- Adjusted the generated-meal PostgreSQL assertion to use the test's existing float tolerance for database round trips.

## Local verification

| Check | Result |
| --- | --- |
| Core Release tests | 989 passed |
| Server Release tests | 90 passed, including 23 media safety tests |
| Registration Release tests | 42 passed |
| Mobile contracts Release tests | 9 passed |
| PostgreSQL clean install | 0 applied → 59 applied; repeated migration command was a no-op |
| PostgreSQL upgrade | Synthetic 31-migration database upgraded to 59; synthetic user, injury, and session rows remained present |
| Reviewed exercise catalog | Local dry run matched 59 records, 160 directed pairs, and 52 template slots; local import completed with no issues |
| PostgreSQL planning acceptance | Focused transaction test passed, including positive-only `SecondsPerRep` source values |
| Generated meal readback | Focused PostgreSQL test passed using float tolerance |
| API with disposable PostgreSQL, no S3 bucket configured | `/health/live` 200, `/health/ready` 200, registration quiz 200 |
| API without a database setting | Startup failed with `ConnectionStrings:PostgreSql must be configured.` |
| API with invalid local database credentials | Liveness 200, readiness 503 |
| ARM64 Lambda package | `.NET 10` `linux-arm64` package built; archive includes `MuscleCuties.Infrastructure.PostgreSql.dll` and migration identifiers |

The injury reconciliation integration test remains blocked. Its preview returns no sessions against the local catalog fixture. A diagnostic run with spaced strength days still found required movements waiting for exercise-condition acknowledgment, an unsupported beginner experience for reviewed candidates, and missing duration metadata for the timed trunk movement. I left the fixture blocked rather than inventing acknowledgments or duration values.

An earlier full PostgreSQL integration run reported 86 passed and 3 failed: one database-name fixture mismatch, one planning acceptance case that passed in isolation, and this injury preview case. Focused reruns passed the generated-meal readback and planning acceptance cases; the full integration project was not rerun after those focused changes.

## Remaining release work

- Set an explicit `Storage__S3__BucketName` in the intended runtime after an operator verifies the bucket, policy, encryption, versioning, and region. The local run did not validate or modify a live bucket.
- The prior read-only AWS audit found 28 source migrations pending. Apply and verify the chain through the separate database operator process after approval; this task touched only disposable local databases.
- A Lambda deployment and post-deployment checks still require separate approval. No Lambda package was uploaded.
