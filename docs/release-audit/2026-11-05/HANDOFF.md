# MuscleCuties handoff and input register

Start with the [implementation plan](IMPLEMENTATION_PLAN.md), then use the [orchestration prompt](ORCHESTRATOR_PROMPT.md). The [simulator checkpoint](../2026-11-05-simulator-checkpoint.md) is the incident record.

The [RM07 checkpoint](RM07_HANDOFF.md) records the owner's P0 acceptance and the scoped local candidate work. The [approved Beta workout policy](WORKOUT_POLICY.md) records PD00 and governs the workout behavior.

## Resume from here

| Field | Current evidence |
| --- | --- |
| Owner decision | Simulator debug data path resolved (P0 completed). Next phase: In-app simulator verification against AWS, fast-forwarding Rider `main`, and progressing QA01 / TF00–TF02. |
| Observed Rider checkout | `main` at `d840d62745337a604f56f4ba32568fb66ae53158` in `/Users/denispetruk/RiderProjects/MuscleCuties`. Port 5080 fix applied to `AppEndpointConfiguration.cs`. Untracked audit files present. |
| Selected candidate | `continue_musclecuties_beta_plan` at `6e38535756494ab3e40a75cc74109ad247727213` in `/Users/denispetruk/.gemini/antigravity/worktrees/MuscleCuties/continue_musclecuties_beta_plan`. 1,346 automated tests green. Port 5080 fix applied. |
| P0 Simulator Status | **COMPLETED & VERIFIED.** Port 5000 AirPlay collision diagnosed. Port 5080 configured in `AppEndpointConfiguration.LocalBaseUrl`. Simulator `backend_api_base_url` pointed to AWS API Gateway (`https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`). App booted cleanly to login screen. Latency measured at 124–165ms warm, RDS PostgreSQL healthy. |
| Current authorized files | Full corrective beta plan in MuscleCuties. Local changes, simulator testing, and test runs authorized. Remote git pushes, cloud mutations, and external releases require owner approval. |
| Next pending packages | 1. **QA01 (In-App Journey Verification):** Execute authentication / workout generation on booted iPhone 16 simulator with AWS backend.<br>2. **Candidate Integration:** Fast-forward / merge candidate `6e385357` into Rider `main`.<br>3. **PL03/PL04:** Isolated Staging & backend release verification.<br>4. **TF00–TF02:** Code signing, device archive, and TestFlight preparation. |

## Inputs and credentials

Collect the following in order. Verify presence and effective target by identifier, role, test or fingerprint. Use protected storage for values and record only sanitized references and verification outcomes in this handoff.

| When | Input or exact name | Source, destination, and status |
| --- | --- | --- |
| Simulator now | Build/device and API URL | Owner supplied Rider Debug and iPhone simulator. Verify effective address. `backend_api_base_url` is the existing MAUI preference. Confirm a free loopback port and matching API listener; 5080 was available during the check. The server can bind with `ASPNETCORE_URLS`; the client resolves its own URL. |
| Simulator now | `ConnectionStrings__PostgreSql`, `Authentication__SigningKey` | Generate a disposable loopback database connection and fresh local key of at least 32 UTF-8 bytes. Keep them in the development secret mechanism and use a synthetic account. No shared AWS, Apple or Beta credential is needed for this gate. |
| Simulator now | `Authentication__Issuer`, `Authentication__Audience` | Existing local defaults apply unless the candidate uses a deliberate override. Treat them as non-secret configuration and verify token issuance and validation agree. |
| Affected disposable DB tests | `MC_PREPARE_DISPOSABLE_WORKOUT_DATABASE`, `MC_REGISTRATION_WRITE_TEST_POSTGRES`, `MC_REGISTRATION_TEST_POSTGRES`, `MC_RETRY_TEST_POSTGRES`, `MC_SCHEMA_COMPATIBILITY_POSTGRES`, `MC_PG09_LOAD_POSTGRES` | Documented in `db-verify.yml`. Select the variables required by the affected suites and use only guarded loopback fixtures. |
| After simulator, when GitHub/AWS work is approved | Environment identity, `AWS_REGION`, `AWS_RELEASE_BUCKET`, OIDC artifact upload/Staging deploy/Beta deploy roles | Repository configuration supplies expected names and target. Platform verifies live role trust, GitHub environment/reviewer and audience/subject, region/account, bucket versioning and least privilege. Use short-lived role credentials. This audit verified read-only access to the expected Beta API, not release-role authority. |
| After simulator, when shared PostgreSQL work is approved | GitHub environment secret `DATABASE_CONNECTION_STRING` → `ConnectionStrings__PostgreSql` | The protected DB workflow expects a dedicated migrator, verified target/version/lock and restore rehearsal. Platform validates the resolved target and grants before use. The runtime API gets its own least-privilege connection. Never infer environment identity from the resource name. |
| After simulator, when backend/worker is approved | API `Authentication__SigningKey`, `Authentication__Issuer`, `Authentication__Audience`; worker `MUSCLECUTIES_WORKOUT_DISCOVERY_CONNECTION` and `MUSCLECUTIES_WORKOUT_TENANT_CONNECTION` | Use environment-scoped secret references and dedicated principals. Verify distinct worker grants to the same intended database, TLS/VerifyFull and fail-closed runtime checks. Keep API auth secrets and database passwords in the approved secrets store. |
| Approved release verification | `MC_RELEASE_POLICY_FILE`, `MC_DB_LOCK_RUNNER`, `MC_SYNTHETIC_BEARER_TOKEN` where the reviewed harness consumes them | Confirm current GitHub implementation versus historical GitLab adapters. Select candidate-bound policy and shared lock. Use an approved short-lived synthetic identity for target smoke and keep its bearer value out of docs and logs. |
| Approved Apple signing and TestFlight | Apple Developer Team ID, distribution identity and provisioning profile, App Store Connect app access, bundle ID `com.musclecuties.app` | Owner chooses owner-operated signing or a protected automation path when the gate opens. Verify signing and upload authorization separately. Keep signing private material in the owner Keychain or approved protected store. For API upload, verify the key ID, issuer and scoped access there. The candidate's device workflow establishes no signed archive proof or validated distribution-secret schema. |

Provider settings apply only to authorized feature acceptance. Confirm which provider path is in use before requesting any input.

| Feature | Candidate configuration to verify |
| --- | --- |
| Apple sign-in/revocation | `AppleSignIn__Audience`, `AppleSignIn__TeamId`, `AppleSignIn__KeyId`, `AppleSignIn__PrivateKeyPem`, and `AppleSignIn__PublicKeys__<kid>` where applicable. These settings serve backend identity and revocation. |
| Email challenges | Provider key `Resend__ApiKey`, `RESEND_API_KEY`, or `EmailChallenges__ResendApiKey`, with `Resend__FromEmail` or `EmailChallenges__FromEmail`; relay path `EmailChallenges__RelayEndpoint` and `EmailChallenges__RelayBearerToken`. Verify `EmailChallenges__ActiveHmacKeyId` and `EmailChallenges__HmacKeys__<kid>` for the selected environment. |
| Email receipts | `EmailReceipts__ActiveKeyId`, `EmailReceipts__PrivateKeyPem`, `EmailReceipts__PublicKeys__<kid>`, `EmailReceipts__Issuer`, and `EmailReceipts__Audience` where the flow is enabled. |
| Media and cleanup | `Storage__S3__BucketName` and `Storage__S3__Region` with verified execution-role permissions. Temporary AWS credentials come from the runtime. Verify `ConnectionStrings__PhotoCleanupDispatch` and `ConnectionStrings__PhotoCleanupProcessor` for the dedicated cleanup paths when applicable. |

The owner's current inputs are recorded. Next ask only for a missing execution decision once the simulator package and exact scope are reviewable. Treat source-derived names as configuration inventory. Presence and privilege in protected environments require their own verification.

## Save before a limit

Write the next checkpoint while context is available. Include the package, base/head SHA, actual changed files and reservations, approvals and exclusions, source/artifact/configuration identities, relevant commands and results, remaining checks, recovery requirements, and the exact next action. Record where any stopped local service or disposable fixture can be safely recreated. Resume through a supported continuation with scope and drift checked again.
