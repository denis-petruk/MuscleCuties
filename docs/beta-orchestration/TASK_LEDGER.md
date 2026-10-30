# Beta task ledger

Updated 2026-10-30. Source validated through `e16b6d39`; see [2026-10-30 evidence](evidence/20261030-b1-b2-validation.md).
verify the exact remote tip before continuing. The detailed B1–B6 criteria remain
in `docs/BETA_PRODUCTION_READINESS_AUDIT_20261029.md` and current implementation
checkpoints. Historical pass counts do not prove this candidate.

| ID | Subsystem | Status | Evidence and next action |
| --- | --- | --- | --- |
| DB-01 | Staging schema upgrade | PASS | 59/59 migrations, compatible preflight and healthy staging Lambda recorded in `docs/infra/STAGING_SCHEMA_CHANGE_APPROVAL_20261030.md`. |
| DB-02 | Migration runner prerequisite | PASS in source | `migration-prerequisites` and `migrate` gate added; 9 focused database/runtime tests pass on disposable PostgreSQL 17. Production runner must be checked against its own target before any migration. |
| SEC-01 | Production HMAC guard | PASS in source | Commit-bound HMAC guard added; 11 rollout-engine tests and failure injection pass. Production CI job remains disabled. |
| SEC-02 | Runtime database principal | BLOCKED | Candidate preflight implemented. Staging still logs in as `mc_admin`. Auth design, dedicated login, tenant-isolation tests and separate live approval remain. |
| REC-01 | PITR restore drill | NOT RUN | Runbook corrected for 59-migration staging. Isolated restore, canary, RPO/RTO and teardown evidence require separate authorization and resources. |
| B1 | Workout safety and canonical lifecycle | OPEN | Core 989 PASS; injury/omission/isolation checks pass. Next: acknowledged metadata-complete canonical injury/clearance acceptance journey, observed-set progression and client reconnect validation. |
| B2 | PostgreSQL and API integration | OPEN | Fresh PostgreSQL 17 at 59 migrations; post-fix integration 130/130 PASS excluding unchanged load fixture (separately PASS, 25,405 requests). Split runs; catalog and full journey scope remain open. |
| B3 | Beta backend parity | OPEN | Verify exact GitLab artifact and current staging routes, including authenticated synthetic flows; no new deploy is authorized by this checkpoint. |
| B4 | Authenticated service journeys | OPEN | Run registration, planning, nutrition, photo and deletion flows against the matched backend. |
| B5 | Public policy and privacy | OPEN | Verify AASA, notices, app privacy and data-flow assertions for the exact candidate. |
| B6 | iOS/TestFlight | OPEN | Build signed Release IPA, verify Apple processing and physical-device flows after owner release authorization. |

Do not mark `READY_FOR_OWNER_RELEASE` while B1–B6 or the production safety gates lack
evidence. Keep P2 deferrals explicit and preserve the existing staging deployment.
