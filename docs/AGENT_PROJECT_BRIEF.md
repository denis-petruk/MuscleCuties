# MuscleCuties — Agent Project Brief

**Purpose:** a single hand-off document an implementation agent can read end to end before touching this repository. It explains what the product must do, every data structure that exists, the workflow each feature must follow, and the rules that must not be broken.

**Prepared:** 2026-10-31, by direct inspection of this working tree (branch `resolving-conflicts`, integration worktree). It is not copied from an earlier audit.

**Precedence when documents disagree:**

1. This brief's **Rules** section (§1) and the owner's recorded product decisions (`docs/orchestration/MUSCLECUTIES_IMPLEMENTATION_SPEC.md`).
2. The actual source code in this tree.
3. `services/postgresql/README.md`, `services/postgresql/INTEGRATION_REVIEW.md`.
4. `docs/orchestration/CONTINUE_INTEGRATION.md`, `OWNER_HANDOFF_20261031.md`.
5. `BACKEND_INTEGRATION_LEDGER.md` (historical findings; some are already fixed).

The code wins over older prose. One concrete example found while writing this brief: the orchestration notes say "hydration routes remain unmapped", but `ApiApplication.cs` does call `app.MapHydrationEndpoints()` and `/api/v1/hydration/*` is registered. The real hydration blocker is the **data model**, not route registration (§9.9). Verify before repeating any claim from an older document.

---

## 1. Non-negotiable rules

These are binding. An agent that violates one has produced unacceptable work even if the code compiles.

### 1.1 Authority and safety

1. **The server is authoritative.** Plan generation, safety restrictions, readiness gating, eligibility, acceptance and reconciliation are evaluated in `Core`/`UseCases` and enforced by the server. A MAUI view model must never be the only gate for a safety rule.
2. **Never invent policy.** Do not hard-code a new prescription number, multiplier, phase mapping, substitution, clinical rule, red-flag triage, or "stop exercising" wording. Approved numbers live in `WorkoutPlanningConfig`, the catalog, and `catalog.workout_planning_config_entries`. If a policy value is missing, the correct behavior is to **fail closed** and report a policy-review-missing outcome — not to guess.
3. **Missing is not "no".** A missing safety answer, symptom, injury field, equipment value, duration or preference is `unknown`, never `no injury`, `no restriction`, `none`, or `0`. Never turn null into a negative answer. Never fabricate a legacy field to make a DTO compile.
4. **Restrictions take effect immediately.** An injury/symptom restriction applies to the active plan at once, even while a replacement proposal is pending or rejected. A high readiness score never overrides an injury restriction.
5. **A newly generated or rebuilt plan is never silently activated.** It is a proposal until an explicit, version-checked acceptance. Rendering it, saving preferences, or running reconciliation must not promote it.
6. **Bounded, approved adjustments to an already accepted plan may be automatic**, with a recorded reason and a user-visible explanation — but only using reviewed substitutes, and only within the reviewed rule set. If no approved alternative exists, the exercise becomes `restricted`/`pending`; do not improvise.
7. **Completed work is immutable.** Finished sessions, set logs and historical results never change because today's plan changed.

### 1.2 Data and security

8. **Tenant isolation is enforced in PostgreSQL, not just in C#.** Every tenant read/write runs inside a transaction with `PostgresTenantContext.SetForCurrentTransactionAsync(...)`, which sets transaction-local `app.user_id`. Never take a tenant UUID from a request body, query string, or header. Never use session-level `SET`.
9. **Never log or return secrets or health payloads.** No access/refresh tokens, passwords, connection strings, symptom text, injury restriction details, or deleted measurement values in logs, traces, error messages, analytics or telemetry.
10. **Client-facing errors are sanitized.** Return a stable error code plus a safe message. Do not echo arbitrary `Exception.Message` / provider text to the caller.
11. **Deletion is real deletion.** A deleted body measurement is removed from live rows, projections, caches under application control and unprocessed retry payloads, and cannot be resurrected by a stale client. Only value-free IDs/revisions/tombstones may remain.
12. **Idempotency is mandatory for externally visible mutations.** Use the account-scoped idempotency record (§11.1) before retrying an ambiguous commit.

### 1.3 Process

13. **Do not deploy, upload to S3, mutate a shared database, run migrations against a non-disposable target, change IAM/roles, or activate HealthKit.** Those require separate explicit authorization.
14. **Do not run builds or tests unless the owner authorizes them.** The product spec allows local validation; the owner's later direct instruction prohibited it. Ask first, then report actual commands and output.
15. **Do not delete tests to make a build green.** Reconcile or adapt them with equivalent evidence, and keep security regression coverage.
16. **Do not reset, clean, auto-stash or discard existing work.** Other checkouts (`~/RiderProjects/MuscleCuties`) contain separate work — leave them untouched.
17. **Clean Architecture boundaries are fixed** (§4). Do not move the prescription algorithm into a view model, or persistence into `Core`.

---

## 2. What the product is

MuscleCuties is an **unreleased** .NET MAUI fitness and wellness app for iOS (first target), with a first-party ASP.NET Core API hosted on AWS Lambda and PostgreSQL persistence.

- No published release, no beta users, no existing customer profiles to migrate. Treat all work as **first-launch development**, never as a compatibility upgrade for a population that does not exist.
- Launch target: a **new, empty PostgreSQL user-data database** with the full `app`/`catalog`/`ops` schema, roles, grants, forced RLS and the reviewed catalog seed.
- AWS Lambda (API) and an S3 bucket for the deployment ZIP are said by the owner to exist; their identities and configuration are **unverified**. Never assume an RDS instance, a bucket name, or a role from application config.

**Product behavior in one sentence:** users keep control over accepting *newly generated workout plans*, while the active workout can receive *automatic, explainable, approved safety- and readiness-related adjustments* that never bypass reported restrictions.

### 2.1 Feature scope

In scope: registration/authentication, onboarding, quiz, profile and units, workout preferences, conditional safety questions, injury tracking, plan proposal/acceptance/active-plan management, daily check-ins and readiness, cycle tracking, active workout sessions, workout history/progress, injury reconciliation, nutrition diary and catalog, hydration, body measurements, progress photos.

Out of scope unless separately approved: account migration, legacy API compatibility periods, old-client cutover, forced-upgrade screens, automated deployment, infrastructure provisioning.

---

## 3. Current state — read this before planning work

The tree is **not release-ready**. Backend builds and tests pass; owner-only UI/device verification and deployment gates remain. The blocker table below is the original audit snapshot; see `STATUS.md` for resolved items.

Known blockers (as recorded and re-checkable in source):

| # | Blocker | Where |
|---|---|---|
| 1 | Mobile preference view model calls a removed `SaveAndUpdatePlanAsync` and references removed `WorkoutPreferencesError` members; several retained tests target retired rules. Full solution build fails. | `src/MuscleCuties.UI`, `tests/*` |
| 2 | Legacy preference contract retired: `PUT /api/v1/profile/workout-preferences` always returns `409 WorkoutPreferencesRequireProposal`; the client store returns `SaveFailed`, so preference editing is not functional end to end. | `ApiApplication.cs:496`, `CloudProfileWorkoutPreferencesStore` |
| 3 | `PostgresProfileDataStore.ToSnapshot` substitutes an empty cycle-tracking mode and zero session duration — fabricated legacy values. | `Infrastructure.PostgreSql/Profile` |
| 4 | Hydration ledger and `DailyNutritionLogRecord.WaterIntakeMl` can disagree; a notes-only write can resend a stale water total; preset/reminder compare-and-replace is not proven atomic. | `Infrastructure.PostgreSql/Hydration`, `HYDRATION_RECONCILIATION_BLOCKER.md` |
| 5 | Refresh/revoke race: a concurrent revoke can miss a just-committed successor under `READ COMMITTED`. A user-row lock candidate was never proven under the real auth principal/RLS. | `PostgresAccountAuthenticationUseCase` |
| 6 | Static installer `services/postgresql/*.sql` predates the integrated workout model; it is a historical baseline, not a release installer. | `services/postgresql/` |
| 7 | Scheduled worker is deployed and enabled with a real Lambda runtime handler; staging accounts are purged and the DLQ is empty. Monitoring is CloudWatch plus the DLQ. | `MuscleCuties.WorkoutReconciliation`, `musclecuties-workout-reconciliation` Lambda/EventBridge rule |
| 8 | Cycle/readiness prescription adjustment is active under the provisional `MC-PLANNING-REVIEW-TEST` approval; replace it with a real reviewed record when available. | `PlanningReleaseApproval`, `CycleReadinessPrescriptionRule`, `PostgresCycleReadinessPrescription` |

Deliberate, correct behavior that looks like a bug:

- The readiness-driven set/phase prescription change returns unchanged prescriptions with `CycleReadinessPolicyReviewMissing` when approval metadata is absent or incomplete. The shipped `MC-PLANNING-REVIEW-TEST` record is a provisional internal approval, not a clinical sign-off; do not describe it as reviewed.
- `PUT /api/v1/workouts/plan` (legacy direct generation) and `GET /api/v1/workouts/plan` are retired and return conflict/error. Do not reinstate them; all plans go through proposals and acceptance.
- `TodayPlanningDecisionRule.ReadinessSuggestionsEnabled` is `false`; the readiness-proposal apply path rejects application.

---

## 4. Repository map and layer ownership

```
MuscleCuties.sln
src/
  MuscleCuties.Core/                       pure domain: policy rules, entities, config, enums
  MuscleCuties.UseCases/                   workflow + contracts (DTOs, store interfaces)
  MuscleCuties.Infrastructure/             client-side adapters: API client, cloud stores, cache
  MuscleCuties.Infrastructure.PostgreSql/  server persistence: EF model, adapters, review data
  MuscleCuties.UI/                         MAUI app: pages, view models, platform code
  MuscleCuties.DbMaintenance/              CLI: dbinit/dbcheck, catalog seeding and maintenance
services/
  MuscleCuties.Server/                     ASP.NET Core API + Lambda host (route mapping only)
  postgresql/                              local PostgreSQL tooling (roles, grants, compose)
  static/                                  static support, privacy and legal pages
tests/                                     unit, contract, PostgreSQL integration
scripts/verify.sh                          solution build + four self-contained test projects
scripts/generate-static-install.sh         execution-disabled draft generator
```

**Layering rules**

- `Core`: no MAUI, no ASP.NET Core, no EF, no I/O. Pure functions and versioned config.
- `UseCases`: orchestration, contracts, store/client interfaces, result types. May depend on `Core`.
- `Infrastructure(.PostgreSql)`: implements store interfaces. EF entities/mapping stay here; migrations run through `DbMaintenance`.
- `Server`: maps HTTP routes to use cases/stores, resolves `sub` claim to user id, maps result statuses to HTTP codes. No business policy in endpoints.
- `UI`: presentation and view models only. No authoritative prescription or safety logic.

**Working directory for this tree:** `/Users/denispetruk/.codex/worktrees/integration-backend/MuscleCuties`.

---

## 5. Architecture and request pipeline

```
MAUI client
  → Authentication (JWT bearer, sub = user id)
  → ASP.NET Core endpoint (ApiApplication + *EndpointMappings)
      → UseCase / Store interface (UseCases)
          → PostgreSQL adapter (Infrastructure.PostgreSql)
              → EF Core DbContext
                  → transaction with set_config('app.user_id', <from token>, true)
                      → RLS policy tenant_isolation
```

### 5.1 Host behavior

- Lambda entry point: `services/MuscleCuties.Server/Program.cs` + `ApiApplication.Build`.
- Lambda mode wraps the connection string with bounded **RDS Proxy** settings from `LambdaHostingConfiguration`. `Lambda__DatabasePool__RunMigrationsOnColdStart=false` — the API never migrates on cold start.
- Required configuration: `ConnectionStrings:PostgreSql`, `Authentication:SigningKey` (≥32 bytes), `Authentication:Issuer`, `Authentication:Audience`, optional `Authentication:TrustedProxies`.
- Forwarded headers are honored only for configured trusted proxies (forward limit 1).
- Health: `GET /health/live` returns `{status, version}`.
- Global exception handler returns a generic HTTP 500 problem document plus a correlation id (`X-Request-Id`); diagnostics are suppressed. Keep it that way.

### 5.2 Identity resolution

Every authorized endpoint resolves the account from the token:

```csharp
Guid.TryParse(principal.FindFirst("sub")?.Value, out var userId) && userId != Guid.Empty
// or ClaimTypes.NameIdentifier
```

A failure returns `401`. Never read the account id from the body. Some clients accept `Guid` directly in build-mode contracts, but the server path must always derive it from the principal.

### 5.3 Transaction + tenant context pattern

```csharp
await using var transaction = await context.Database.BeginTransactionAsync(ct);
await PostgresTenantContext.SetForCurrentTransactionAsync(context, authenticatedUserId, ct);
// ... read/write/save
await transaction.CommitAsync(ct);
```

Because the provider enables transient retries, **any explicit transaction must run inside EF's configured execution strategy** (`Database.CreateExecutionStrategy().ExecuteAsync(...)`), otherwise the retry strategy rejects the manual transaction.

Never accept a tenant id from an untrusted request body, and never set session-level `SET app.user_id` — it leaks across pooled connections.

---

## 6. Data platform

### 6.1 Schemas

| Schema | Contents | Runtime access |
|---|---|---|
| `app` | All tenant/user data plus auth and operational support tables | `musclecuties_app` (tenant tables via RLS), `musclecuties_auth` (lookup functions only) |
| `catalog` | Read-only reference data: exercises, templates, volume budgets, quiz, planning config, food catalog | App role reads only |
| `ops` | Operational logs (food sync) | Not tenant-facing |

### 6.2 Roles

Created in `bootstrap.sql`, ownership in `ownership.sql`, grants in `roles-and-grants.sql`.

- `musclecuties_app` — runtime data connection. Read `catalog`; read/write only RLS-protected tenant tables. No access to global email challenges, ops logs, migration history, or schemas.
- `musclecuties_auth` — authentication connection. May call `app.lookup_user_by_email(email)` / `app.lookup_user_by_apple_id(subject)` and use the email challenge table. **No direct access to `app.users`.**
- `musclecuties_migrator` — `SET ROLE` target for the installer and maintenance; `NOBYPASSRLS`. It cannot see tenant rows without tenant context (this matters for backfills).
- `musclecuties_workout_dispatch` — strict discovery role for the reconciliation worker (created by a migration; verify membership before scheduling).
- Login roles are provisioned through the secret manager with `Options=-c role=<group>` so `current_user` inside RLS names the least-privilege role.

Discrepancy to be aware of: the auth and app roles as written in the static installer do not carry direct grants on `app.refresh_sessions` / `app.revoked_access_tokens`. Any auth change must reconcile principal and grants — **never broaden roles or weaken RLS just to make an auth change pass**.

### 6.3 Row-level security

- 35 tenant tables have `FORCE ROW LEVEL SECURITY`. A table owner does not bypass it.
- The standard policy is `tenant_isolation`:
  ```sql
  CREATE POLICY tenant_isolation ON app.<table>
    USING (user_id = (NULLIF(current_setting('app.user_id', true), ''))::uuid)
    WITH CHECK (user_id = (NULLIF(current_setting('app.user_id', true), ''))::uuid);
  ```
- `app.command_idempotency` uses the same predicate under the name `command_idempotency_tenant_policy`.
- Special policies: `auth_user_lookup` (migration-owner-only RLS on `app.users` for the lookup functions), `cleanup_dispatch_cursor_access`, `cleanup_due_owner_discovery`, `photo_object_reconcile_maintenance`, `photo_object_reconcile_owner_access`, `photo_object_reconcile_stage_purge`.
- Consequence: any query executed without tenant context over a forced-RLS table returns **zero rows** and updates nothing — silently. A "successful" bulk backfill under `musclecuties_migrator` can change nothing at all. Verify counts, never infer success from exit status.

### 6.4 Schema ownership and exclusions

`schema.sql` intentionally omits `public."__EFMigrationsHistory"`. A fresh static install has **no** migration history. The legacy EF chain is only for databases created before the static installer, and never for an already populated static schema.

---

## 7. Data dictionary

60 tables: 43 in `app`, 16 in `catalog`, 1 in `ops`. Columns are the physical columns in `services/postgresql/schema.sql`. `RLS` = participates in forced row-level security on `user_id`.

### 7.1 Identity, auth and account

| Table | Columns | Purpose / rules |
|---|---|---|
| `app.users` | `id, username, email, normalized_email, apple_user_id, credential_type, password_hash, created_at, updated_at, is_onboarding_complete, privacy_notice_accepted_at_utc, privacy_notice_version` | One row per account. `normalized_email` is a generated `lower(btrim(email))` column — use it for lookups, never a raw email comparison. `credential_type` distinguishes password / Apple. **RLS forced**, but the auth role reaches it only through `SECURITY DEFINER` lookup functions. |
| `app.refresh_sessions` | `id, user_id, family_id, token_hash, created_at_utc, expires_at_utc, revoked_at_utc, replaced_at_utc` | Rotating refresh tokens. Only the **hash** is stored; the raw successor cannot be reconstructed. Rotation marks the old row `replaced_at_utc` and inserts a successor in the same transaction. Presenting a replaced token revokes the whole `family_id`. Not in the forced-RLS list — confirm grants before querying. |
| `app.revoked_access_tokens` | `id, user_id, token_id, expires_at_utc, revoked_at_utc` | Access-token deny list by `token_id` (JWT `jti`). |
| `app.apple_sign_in_nonces` | `id, nonce_hash, expires_at_utc, redeemed_at_utc` | Single-use Apple identity nonces (hash only). |
| `app.password_login_attempts` | `id, scope_key, attempted_at_utc` | Throttling record for password sign-in. No email or IP in clear. |
| `app.email_challenges` | `id, purpose, redeemed_at_utc` | Global challenge receipt; deliberately not reachable by the app role. |
| `app.email_challenge_sessions` | `id, normalized_email, purpose, hmac_key_id, code_mac, created_at_utc, expires_at_utc, resend_available_at_utc, consumed_at_utc, superseded_at_utc, locked_at_utc, failed_attempts, receipt_id, privacy_notice_accepted_at_utc, privacy_notice_version` | Email verification/OTP flow. Code is stored as an HMAC (`code_mac`), never plaintext; `failed_attempts`, `locked_at_utc` and `superseded_at_utc` implement throttling and resend semantics. |
| `app.source_account_claim_nonces` | `id, destination_user_id, source_installation_key_hash, source_account_key_hash, nonce_hash, created_at_utc, expires_at_utc, redeemed_at_utc` | Nonce for a one-time claim linking an anonymous/local source account to a destination account. Hashes only. |
| `app.source_account_mappings` | `id, claim_id, destination_user_id, source_installation_key_hash, source_account_key_hash, created_at_utc` | Durable result of a claim. No personal data beyond opaque hashes. |
| `app.user_profile_snapshots` | `id, user_id, snapshot_reason, profile_json, created_at` | Point-in-time profile copy used for quiz provenance and proposal input snapshots. Append-only. |
| `app.user_quiz_responses` | `id, user_id, quiz_question_id, quiz_answer_id, answered_at, user_profile_snapshot_id` | Onboarding quiz answers, linked to the snapshot they produced. |
| `app.command_idempotency` | `id, user_id, command_key, request_hash, response_json, completed_at` | Account-scoped idempotency ledger (§11.1). `command_key` ≤128 chars; `request_hash` is a hash of the canonical request; `response_json` + `completed_at` are set together (CHECK constraint). |

### 7.2 Profile

| Table | Columns | Purpose / rules |
|---|---|---|
| `app.user_profiles` | `id, user_id, name, date_of_birth, height, weight, goal, weight_goal_pace, training_experience_level, cycle_tracking_mode, current_cycle_phase, workout_days_per_week, cycle_length, dietary_tags, session_duration_minutes, equipment_level, phase_baselines_json, preferred_workout_activity_types, unit_system, body_weight_unit, food_mass_unit, height_unit, distance_unit, energy_unit, nutrition_goals_json, reproductive_context_json, profile_image_path, updated_at, concurrency_version` | The single profile row. `concurrency_version` is the optimistic-concurrency token — every write must compare and increment it. JSON columns are versioned payloads, not free-form dumps. `cycle_tracking_mode` / `current_cycle_phase` / `session_duration_minutes` are legacy fields retained for schema compatibility; a migration that removes them must not fabricate replacements (§9.9 blocker 3). |
| `app.user_exercise_preferences` | `id, user_id, original_exercise_id, preferred_exercise_id, created_at` | Durable "swap this exercise for that one" preference, written when a swap is saved with `SavePreference`. Consulted by eligibility/substitution. |
| `app.user_movement_notes` | `id, user_id, exercise_id, exercise_name, notes, created_at, updated_at` | Per-exercise movement notes surfaced in session detail. `exercise_name` is denormalized for display. |

### 7.3 Workout planning and prescription

| Table | Columns | Purpose / rules |
|---|---|---|
| `app.workout_plans` | `id, user_id, name, is_active, cycle_phase_target, created_at` | Plan header. At most one active plan per account (enforce in the write path; verify before adding rows). A new plan starts inactive and becomes active only through acceptance. |
| `app.workout_days` | `id, user_id, workout_plan_id, day_of_week, workout_type, name, week_index, date` | One row per scheduled day. `week_index` supports multi-week templates; `date` is the resolved calendar date. `workout_type` is a catalog/planning string (strength, cardio, recovery, …). |
| `app.workout_day_exercises` | `id, user_id, workout_day_id, exercise_id, sets, reps, duration_seconds` | Prescribed exercises for a day. `exercise_id` references `catalog.exercises`. Optional/extra prescription detail (optional sets, movement notes, dose adjustment) is carried by the plan/prescription payloads and the contract DTOs, not by this table alone. |
| `app.daily_readiness_logs` | `id, user_id, date, sleep_hours, steps_yesterday, energy, pain, bloating, weight_kg, sleep_quality, soreness, previous_day_load, readiness_score, readiness_tier, phase, created_at, planning_sleep_quality, planning_soreness, planning_stress, planning_time_zone_id, planning_recorded_at_utc, planning_revision` | Daily check-in. `energy`/`pain`/`sleep_quality`/`soreness` are the recorded observations; `readiness_score` (0–100) and `readiness_tier` are derived. `planning_*` columns plus `planning_revision` make the planning evidence reproducible. Missing values stay missing — never default them to a healthy answer. |
| `app.workout_injury_logs` | `id, user_id, site_flag, status, since, pain, date, created_at` | Injury observations. `site_flag` is a **bitmask** (`WorkoutInjurySite`: Foot=1, Knee=2, Ankle=4, Shoulder=8, LowBack=16, Wrist=32, Hip=64, Neck=128), so combinations are possible. `status` ∈ acute / recovering / cleared. `pain` is 0–10 on this path (a separate entry rule clamps 0–5 — do not convert silently). `since` is onset; `date` is the logged date. |
| `app.workout_logs` | `id, user_id, workout_day_id, date, completion_percent, notes, created_at` | Per-day completion record. |
| `app.workout_session_logs` | `id, user_id, workout_name, started_at, completed_at, total_duration_seconds, total_volume_kg, notes, rpe_rating, is_deleted, deleted_at, created_at, updated_at` | Executed session header. Soft-deleted with `is_deleted`/`deleted_at`; totals are derived at finish. |
| `app.workout_exercise_logs` | `id, user_id, workout_log_id, workout_day_exercise_id, exercise_id, completed_sets, completed_reps, weight_kg, completed_duration_seconds, distance_km, average_heart_rate_bpm, pace_seconds_per_km, power_watts, cadence_rpm, effort_rating, created_at` | Per-exercise execution for endurance and strength modalities. Metric columns are nullable — absence means "not measured", not zero. |
| `app.exercise_set_logs` | `id, user_id, workout_session_log_id, exercise_id, exercise_name, set_number, weight_kg, reps_completed, rpe, is_warmup, rest_time_seconds, is_deleted, deleted_at, created_at, updated_at` | Individual set records, editable after the session (`PUT /api/v1/workouts/history/sets/{setId}`). Soft delete only. |

### 7.4 Cycle

| Table | Columns | Purpose / rules |
|---|---|---|
| `app.cycle_logs` | `id, user_id, start_date, end_date, cycle_length, created_at` | Recorded cycle intervals. Phase estimation requires **3 completed intervals (4 recorded starts) that agree**; otherwise the phase is `unknown`. |
| `app.cycle_phase_logs` | `id, user_id, cycle_log_id, phase, flow_intensity, logged_at, note, created_at, local_date` | Daily phase/flow observations attached to a cycle. `local_date` anchors the entry to the user's local day. |
| `app.symptom_logs` | `id, user_id, cycle_log_id, date, symptom_type, severity, notes, created_at` | Cycle symptom diary. Severity is user-reported; `notes` is free text and must never be logged or echoed outside the user's own response. |

### 7.5 Nutrition

| Table | Columns | Purpose / rules |
|---|---|---|
| `app.daily_nutrition_logs` | `id, user_id, local_date, water_intake_ml, target_calories, target_protein, target_carbs, target_fat, total_calories, total_protein, total_carbs, total_fat, notes, is_deleted, deleted_at, created_at, updated_at` | One row per user-local day: targets, rolled-up totals, and the legacy water total. `water_intake_ml` is the subject of the hydration conflict (§9.9). |
| `app.food_item_entries` | `id, user_id, daily_log_id, local_date, meal_type, name, serving_size, serving_unit, servings_count, calories, protein, carbs, fat, logged_at_timestamp, is_deleted, deleted_at, created_at, updated_at` | Individual diary food entries with a denormalized nutrient snapshot so catalog edits never rewrite history. |
| `app.logged_meals` | `id, user_id, date, logged_at, meal_type, accepted_suggestion_name, accepted_suggestion_phase, created_at` | A meal-level entry (a composed meal rather than a single food). |
| `app.logged_meal_entries` | `id, user_id, logged_meal_id, catalog_food_item_id, user_food_item_id, grams` | Components of a logged meal, by grams. Exactly one of the two food references is meaningful per row. |
| `app.user_food_items` | `id, user_id, name, calories, protein, carbs, fats, fiber, iron, vitamin_c, vitamin_d, vitamin_a, folate, calcium, magnesium, zinc, potassium, fdc_id, data_type, brand_owner, brand_name, gtin_upc, ingredients, serving_size, serving_size_unit, serving_options_json, last_synced_at, created_at, updated_at` | User-created or imported custom foods (USDA-shaped). Full micronutrient set is preserved. |
| `app.user_meal_templates` | `id, user_id, name, description, meal_type, dietary_tags, phase_tags, sort_order, created_at` | User meal templates. |
| `app.user_meal_template_entries` | `id, user_id, meal_template_id, catalog_food_item_id, user_food_item_id, grams` | Template components. |
| `app.recipes` | `id, user_id, name, snapshot_json, created_at_utc` | User recipes stored as a versioned snapshot. Read-modify-write must preserve unknown JSON fields. |
| `app.saved_meal_templates` | `id, user_id, name, meal_type, snapshot_json, created_at_utc` | Saved meal snapshots for copy/reuse. |
| `app.nutrition_diary_command_events` | `id, user_id, cursor, diary_date, command_id` | Ordered event log for diary commands; the read model's cursor source. |
| `app.nutrition_diary_event_cursors` | `user_id, id, last_cursor` | Per-user cursor for the diary event stream. |

### 7.6 Hydration (blocked — see §9.9)

| Table | Columns | Purpose |
|---|---|---|
| `app.command_idempotency` | see §7.1 | Currently also holds hydration day state (baseline, entries, receipts) as JSON in the restored adapter. |
| `app.daily_nutrition_logs.water_intake_ml` | see §7.5 | Competing water total. |

There is **no dedicated hydration table** in the static schema. The restored adapter stores hydration state inside `command_idempotency` JSON while the diary reads `water_intake_ml`. That is the structural reason the two views can disagree.

### 7.7 Photos

| Table | Columns | Purpose / rules |
|---|---|---|
| `app.photo_journey_entries` | `id, user_id, date, image_path, weight_kg, notes, phase, created_at` | Visible progress-photo entries. `image_path` is a key, not a URL. |
| `app.photo_upload_intents` | `id, user_id, idempotency_key, content_type, declared_size_bytes, created_at_utc, expires_at_utc, state` | Presigned-upload lifecycle. `state` is the `PhotoUploadIntentState` value; an intent must be committed before its object is referenced. |
| `app.photo_object_cleanups` | `id, user_id, photo_id, reason, not_before_utc, state, attempt_count, lease_token, lease_generation, lease_expires_at_utc, last_failure_category, last_attempt_at_utc, created_at_utc, completed_at_utc` | Durable queue for deleting S3 objects. Leased (`lease_token`/`lease_generation`/`lease_expires_at_utc`) so workers cannot double-delete; `not_before_utc` is the retry backoff anchor. |
| `app.photo_object_reconcile_targets` | `id, target_key, not_before_utc, created_at_utc, lease_token, lease_generation, lease_expires_at_utc` | Maintenance targets for reconciling storage against the database. Not tenant-scoped; protected by dedicated policies. |
| `app.photo_cleanup_dispatch_state` | `state_id, after_owner_id` | Cursor for the cleanup dispatcher scan. |

### 7.8 Catalog (read-only reference)

Seeded from the reviewed manifest. `seed_code` is the stable natural key used by the seed file; numeric `id`s are catalog identity and must stay stable because prescriptions reference them.

| Table | Columns |
|---|---|
| `catalog.exercises` | `id, seed_code, code, name, description, image_url, video_url, technique_notes, primary_muscle, secondary_muscles, joint_areas, is_injury_friendly` |
| `catalog.exercise_muscle_contributions` | `id, seed_code, exercise_id, muscle_group_id, fraction` |
| `catalog.workout_muscle_groups` | `id, seed_code, name` |
| `catalog.workout_exercise_definitions` | `id, seed_code, name, pattern, required_equipment, long_length_loaded, fatigue_cost, skill_demand, setup_seconds, seconds_per_rep, eligible_for_hi_block, unilateral_doubles_time, is_bodyweight, contraindications, preferred_for, supine_or_prone, valsalva_demand, substitute_group_id` |
| `catalog.session_archetypes` | `id, seed_code, code, display_name, is_lower_dominant, contains_heavy_hinge` |
| `catalog.slot_templates` | `id, seed_code, archetype_id, block, allowed_patterns_json, primary_muscle_id, sets_min, sets_max, reps_min, reps_max, target_rir, droppable, superset_group` |
| `catalog.week_templates` | `id, seed_code, days_per_week, archetype_sequence_json` |
| `catalog.volume_budget_rows` | `id, seed_code, days_per_week, muscle_group_id, fractional_sets` |
| `catalog.goal_tier_weights` | `id, seed_code, goal, muscle_group_id, tier` |
| `catalog.workout_planning_config_entries` | `id, seed_code, section, key, value` |
| `catalog.food_items` | `id, seed_code, name, calories, protein, carbs, fats, fiber, iron, vitamin_c, vitamin_d, vitamin_a, folate, calcium, magnesium, zinc, potassium, fdc_id, data_type, brand_owner, brand_name, gtin_upc, ingredients, serving_size, serving_size_unit, serving_options_json, last_synced_at, created_at, updated_at` |
| `catalog.food_item_versions` | `id, food_item_id, nutrient_snapshot, versioned_at, change_source` |
| `catalog.meal_templates` | `id, seed_code, name, description, meal_type, dietary_tags, phase_tags, sort_order, is_system, created_at` |
| `catalog.meal_template_entries` | `id, seed_code, meal_template_id, food_item_id, grams` |
| `catalog.quiz_questions` | `id, seed_code, question, order_index, question_type` |
| `catalog.quiz_answers` | `id, seed_code, question_id, text, order_index, mapped_value` |

`catalog.food_item_versions` is the only catalog table not populated by the seed; rows appear when a food is updated (`change_source` explains why).

### 7.9 Operations

| Table | Columns | Purpose |
|---|---|---|
| `ops.food_sync_logs` | `id, started_at, completed_at, items_upserted, items_failed, status, error_details` | Food import run log. `error_details` must not contain user data. |

### 7.10 Seed and installer expectations

- `install.sql` runs, in order: `bootstrap.sql` → `schema.sql` → `ownership.sql` → `roles-and-grants.sql` → `catalog-seed.sql`.
- The installer **fails** if `catalog.week_templates`, `catalog.exercises` or `catalog.workout_exercise_definitions` is empty, and **refuses to take ownership of pre-existing application schemas**.
- The canonical manifest requires a matching **SHA-256**; the seed command rejects a mismatch. Documents record 742 rows (README) and 746 manifest rows (ledger) — read the manifest rather than trusting either number.
- Never hand-edit `schema.sql` or `catalog-seed.sql`. Regenerate from a disposable PostgreSQL 17 database after the reviewed EF chain + seed, then validate on a second empty database.

---

## 8. Data lifecycle rules

| Rule | Applies to |
|---|---|
| **Append-only history** | `user_profile_snapshots`, `nutrition_diary_command_events`, `workout_session_logs` after finish, `exercise_set_logs` history, `workout_exercise_logs`, `catalog.food_item_versions`. Never rewrite; write a new row or a new version. |
| **Soft delete** | `daily_nutrition_logs`, `food_item_entries`, `workout_session_logs`, `exercise_set_logs` use `is_deleted`/`deleted_at`. Reads must filter them out; queries must not sum deleted rows. |
| **Hard delete with tombstone** | Body measurement values: erase the value, keep a value-free id/revision so a retry cannot resurrect it. |
| **Optimistic concurrency** | `user_profiles.concurrency_version`, planning lease `fence`/`version`, diary water `ExpectedVersion`, activity answers `ExpectedVersion`, `workout_foreground_timezone.ExpectedVersion`. A stale write returns conflict and changes nothing. |
| **Denormalized snapshots** | Food entries keep their own nutrient numbers; logged meals keep the accepted suggestion name/phase; `exercise_set_logs.exercise_name`; movement notes keep `exercise_name`. Catalog edits must not retroactively rewrite user history. |
| **Provenance** | Record who/what produced a value: `snapshot_reason`, `planning_revision`, `change_source`, planning policy/ruleset versions, `cycle_phase_target`. Reason codes are machine-readable; never dump raw health text as the reason. |
| **Time** | `timestamptz` for instants, `date` + `local_date` for user-local days. A day boundary is decided in the user's time zone (`planning_time_zone_id`, timezone version), never the server's. Time-zone transitions must not duplicate a drink, session or check-in. |

---

## 9. Required workflows

Each workflow states the sequence, the tables touched, the invariants, and the failure behavior. Endpoint names are the real routes in this tree.

### 9.1 Registration, onboarding and quiz

1. `POST /api/v1/auth/apple/nonce` (Apple) or `POST /api/v1/accounts/registration` → creates `app.users` (`is_onboarding_complete=false`, privacy notice timestamp + version) and the auth credential.
2. `GET /api/v1/accounts/registration/quiz` returns questions/answers from `catalog.quiz_questions` / `catalog.quiz_answers`.
3. Quiz answers are persisted to `app.user_quiz_responses`, linked to the `app.user_profile_snapshots` row they produce.
4. `PUT /api/v1/profile` writes the profile (`concurrency_version` compare-and-increment), including goal, experience, units, equipment, activity preferences.
5. Onboarding completion sets `users.is_onboarding_complete`.

**Invariants:** a brand-new account has **no active plan**; profile writes never generate a plan; quiz answers are never inferred from the profile or vice versa.

### 9.2 Authentication and session lifecycle

- `POST /api/v1/auth/password` — password sign-in. Failures are throttled via `password_login_attempts`; never reveal whether the email exists in the error.
- `POST /api/v1/auth/apple` — Apple identity token exchange, nonce validated against `apple_sign_in_nonces`.
- `GET /api/v1/auth/email-exists`, `GET /api/v1/auth/username-exists` — availability checks; keep them non-enumerating in spirit (they are intentionally explicit, but must not leak account data).
- `POST /api/v1/auth/refresh` — atomic rotation: lock the presented token row, mark `replaced_at_utc`, insert a successor in the same `family_id`, commit, then respond. A already-replaced token revokes the family (reuse detection). **Never add a grace period that accepts the old token without distinguishing intent.**
- `POST /api/v1/auth/revoke` / `revoke-refresh` — revoke the session/family; the route requires a valid access token by design.
- `POST /api/v1/auth/password/reset`, `/password/change` — lock the user row first, then revoke sessions (lock ordering: user → sessions).
- `DELETE /api/v1/accounts/me` — account deletion: purge tenant rows and photo objects via the cleanup queue; keep only what policy requires.
- Email challenge: `POST /api/v1/email-challenges/start|resend|verify` using `email_challenge_sessions` (HMAC code, attempt limits, supersede on resend).

**Invariants:** only token hashes are stored; a lost refresh response must lead to safe recovery or a fresh sign-in — never a weakened replay check; no token, password or secret in logs.

### 9.3 Workout preferences and conditional safety questions

- `GET /api/v1/profile` returns the profile, including resolved workout preferences.
- `PUT /api/v1/profile` updates profile fields; legacy cycle mode / days-per-week / session duration in a submission **fail closed** rather than being silently ignored.
- `PUT /api/v1/profile/workout-preferences` is **retired**: always `409` with `WorkoutPreferencesRequireProposal` ("Submit workout preferences with a workout plan proposal."). Do not add a compatibility shim that silently regenerates a plan.
- Preferences that affect planning are submitted with the proposal request (`StrengthPlanPreviewRequestV020.PreferenceDraft`, optionally version-checked with `PreferenceDraftVersion`).
- Activity-safety answers are stored through `PUT /api/v1/workouts/plan/answers` (`SaveActivityAnswersRequest` with `ExpectedVersion`): swimming facility/ability/style, climbing competence/instruction, demanding-yoga flag, HIIT evidence. Missing answers make the activity **unsafe/unknown**, not selected.

**Invariant:** after a preference change, ask only the *new, affected or expired* safety questions — never a full re-onboarding.

### 9.4 Injury capture and restriction

1. `PUT /api/v1/injuries` (and `PUT /api/v1/workouts/injuries`) persists an injury observation (`site_flag`, `status`, `pain`, `since`) with the two conditional questions: which movements worsen symptoms, and whether a clinician advised avoiding anything. `WorkoutInjurySite` is a bitmask; status is acute/recovering/cleared.
2. `GET /api/v1/injuries` lists observations; `DELETE` removes one.
3. `GET /api/v1/workouts/injuries/impact` returns `WorkoutInjuryImpactDto` (blocked exercises, rehab exercises, blocked activities, summary).
4. `GET /api/v1/workouts/exercises/{exerciseDefinitionId:int}/contraindicated` and `/swaps` evaluate a single exercise against active injuries and approved alternatives. Both singular (`/api/v1/workout/...`) and plural prefixes are mapped for client compatibility.

**Invariants:** contraindication is a **bitwise overlap** of exercise contraindication flags and active injury flags. Restrictions apply immediately, including to an accepted active plan. If no eligible approved substitute exists, leave the exercise `restricted`/pending and ask only the relevant follow-up. Never assume "no flag" means safe when the observation is missing.

### 9.5 Plan proposal → lease → acceptance (the canonical lifecycle)

This is the most important state machine in the product.

**States**

| Artifact | States | Storage |
|---|---|---|
| Proposal | `Feasible` (persisted, immutable) or not feasible (returned, not persisted, `IsPersisted=false`) | Proposal store; `ProposalId`, `ProposalVersion`, `ReplacesExistingPlan` |
| Lease | open (`LeaseId`, `Fence`, `ExpiresAtUtc`, 5 minutes), renew, close, takeover | Proposal store; `PlanningLifecycleRule` |
| Plan | draft → `is_active=false` → active (only on acceptance); previous plan becomes superseded | `app.workout_plans`, `app.workout_days`, `app.workout_day_exercises` |
| Preview | active preview (`/lease/active`) so the client can resume after a crash | Proposal store |

**Sequence**

1. `GET /api/v1/workouts/catalog` — planning catalog (activities, equipment, style offers, exclusions).
2. `GET /api/v1/workouts/plan/exercise-exclusions` — the exclusion catalog for the account.
3. `POST /api/v1/workouts/plan/strength-preview` — validate inputs and produce a **preview without persisting**. Returns `Status` (`Feasible` / other), schedule, rationale, `ReplacesExistingPlan`.
4. `POST /api/v1/workouts/plan/strength-proposals` — persist the preview as an immutable proposal, acquiring a lease/fence. Only a `Feasible` preview with a non-empty mixed-activity schedule is stored (`IsPersisted=true`); otherwise nothing is persisted. A draft-version mismatch raises `WorkoutPlanningInputsChangedException` → `409 WorkoutPlanningInputsChanged`.
5. `POST /api/v1/workouts/plan/lease/renew|close|takeover` — lease maintenance. `takeover` requires a `proposalId`. A stale or expired fence must fail.
6. `POST /api/v1/workouts/plan/strength-proposals/{proposalId:guid}/accept` — acceptance with `ProposalId`, `LeaseId`, `Fence`, `ProposalVersion`, `ReplacementConfirmed`, `ReducedVolumeAcknowledged`. The store performs an **atomic compare-and-swap** over the captured input versions (`PlanningInputVersions`: preferences, active plan, answers, injuries, experience, catalog hash, policy version, timezone). Result: `Accepted` / `InvalidInput` / `NotFound` / `Conflict` / `Stale` / `Limited` / `InvalidProposal`.

**HTTP mapping in use**

| Status | Code |
|---|---|
| `InvalidInput` | 400 |
| `NotFound` | 404 |
| `Conflict`, `Stale` | 409 |
| `Limited`, `InvalidProposal` | 422 |
| anything else | 409 |

**Invariants**

- Saving a proposal **never** activates it. Rendering, saving preferences, or a reconciliation run never activates it.
- Acceptance requires confirmations when the proposal replaces an existing plan or reduces volume (`PlanningLifecycleRule.ConfirmationsSatisfied`).
- Fence arithmetic fails closed on overflow (`NextFence` returns `null` at `long.MaxValue`).
- A stale input (preferences, injuries, catalog, policy) must be rejected, not silently applied.
- `POST /api/v1/workouts/plan` (legacy direct generation) returns `409 LegacyPlanBypassDisabled`; `GET` on it returns an error directing clients to `/overview` or `/progress`.

### 9.6 Readiness, cycle phase and automatic adjustment

- `PUT /api/v1/check-ins` records today's readiness (`sleep quality, energy, soreness, stress`, each 1–5). Score = `100 × ((sleep−1)+(energy−1)+(5−soreness)+(5−stress))/16`; bands ≥75 ready, ≥50 consider lighter, <50 easier day. Planning-specific fields, once supplied, require a complete valid planning evidence set and a time zone.
- `GET /api/v1/dashboard/overview` and `/calendar` surface today's state and the plan calendar.
- `POST /api/v1/workouts/readiness-proposals/review|apply|keep` — optional readiness suggestions. `apply` currently rejects application (`ReadinessSuggestionsEnabled=false`); keep it disabled unless policy approval changes.
- Cycle: `PUT /api/v1/cycle/history/cycles` (save cycle), `DELETE /api/v1/cycle/history/cycles/{id}`, `PUT /api/v1/cycle/history/symptoms`, `DELETE .../symptoms/{id}`, `PUT /api/v1/cycle/history/phases` (phase log), `GET /api/v1/cycle/history`, `GET /api/v1/cycle/history/prediction`, `GET /api/v1/cycle/overview`.
- Gating (`WorkoutGatingRule`) applies only approved defaults: low readiness → strength set multiplier **0.7** with **RPE cap 7**; two consecutive low days → recovery/yoga; cardio on a low day → yoga; HIIT requires min sleep 6.5 h, max pain 1, explicit opt-in, established underlying activity and prior-week evidence; unknown phase → conservative recovery dose.
- Cycle/readiness **prescription** changes (`CycleReadinessPrescriptionRule`) are approval-gated: they require a `PlanningReviewApproval` with a reference, a UTC date, ruleset version `MC-PLANNING-0.2`, reviewed training-policy version `0.2.0`, and coverage of `TrainingPrescriptions` + `AutomatedProgression`. No such record exists, so the rule returns `CycleReadinessPolicyReviewMissing` and changes nothing. **Do not enable phase-specific weight/rep changes** — no reviewed mapping exists.

**Invariants:** a high score never overrides an injury restriction; repeated check-ins must not accumulate repeated reductions; every automatic adjustment records rule id/version, reason, effective date, and before/after values.

### 9.7 Active session execution

- `POST /api/v1/workouts/sessions/start` — start a session (optionally for a planned day).
- `GET /api/v1/workouts/sessions/planned/{workoutDayId:int}` and `/history` — the planned session and its previous performance.
- `POST /api/v1/workouts/sessions/{sessionId}/timing/resume` / `/timing/checkpoint` — resumable timing with checkpoints (crash/background safe).
- `PUT /api/v1/workouts/sessions/{sessionId}/sets` — record sets (`WorkoutSetDetailDto`: number, weight, reps, RPE, warmup, rest).
- `POST /api/v1/workouts/sessions/{sessionId}/finish` — close the session, compute duration/volume/RPE, persist `workout_session_logs` + `workout_exercise_logs` + `exercise_set_logs`.
- `POST /api/v1/workouts/sessions/{sessionId}/cancel` — abandon without recording completed work.
- Session detail exposes dose-adjustment metadata: `DoseAdjustmentReason` (DailyReadiness / ProvisionalPhaseFeeling / RepeatedPhasePattern / SafetyReview), `DoseAdjustmentConfidence` (Provisional / Repeated / Current), `DoseAdjustmentPolicyVersion`. UI must show the reason and before/after, never a bare changed number.
- Optional exercises/session: `PUT /api/v1/workouts/optional-exercise`, `PUT /api/v1/workouts/optional-session`.
- Swaps: `GET /api/v1/workouts/day-exercises/{workoutDayExerciseId:int}/swaps`, `POST /api/v1/workouts/day-exercises/swap` (optionally persist as a preference).
- Movement notes: `GET|PUT /api/v1/workouts/day-exercises/.../movement-note`.
- Reschedule: `POST /api/v1/workouts/reschedule`.

**Invariants:** finishing/cancelling is idempotent per session; completed logs are immutable; a restriction discovered mid-session blocks only the affected exercise.

### 9.8 History, archive and progress

- `GET /api/v1/workouts/history`, `/months`, `/month/{year:int}/{month:int}`, `/count`, `/month/.../count` — archived sessions with counts.
- `GET /api/v1/workouts/history/sessions/{sessionId:guid}` — session detail with sets/exercises.
- `PUT /api/v1/workouts/history/sessions/{sessionId:guid}/notes`, `PUT /api/v1/workouts/history/sets/{setId:guid}` — corrections to archived data (soft-delete aware).
- `GET /api/v1/workouts/history/personal-records` — derived PRs.
- `GET /api/v1/workouts/progress` — experience level, credited days, promotion pending (`WorkoutProgressResponse`).
- `PUT /api/v1/workouts/foreground-timezone` — timezone version for day crediting (`ExpectedVersion` compare).
- `GET /api/v1/workouts/overview` — the canonical current-plan read model (replaces the retired `GET /workouts/plan`).
- Promotion: 20 credited completed days → intermediate, 130 → advanced, unless onboarding baseline is higher.

### 9.9 Nutrition diary and hydration (partially blocked)

**Diary** (`/api/v1/nutrition/diary/*`): read a day, read/upsert entries, metadata, notes, delete an entry, history, event stream (`/events` with cursor), batch logging, copy preview/commit, quick add. Tables: `daily_nutrition_logs`, `food_item_entries`, `nutrition_diary_command_events`, `nutrition_diary_event_cursors`, `logged_meals`, `logged_meal_entries`.

**Catalog** (`/api/v1/nutrition/catalog/*`, `/api/v1/nutrition/recipes|templates|meals|meal-plan|suggestions|targets|overview`): search, barcode, custom foods, recipes, templates, meal plan generation/refresh.

**Hydration** (`/api/v1/hydration/*`): presets (`GET`, `POST /presets/initialize`, `POST`, `PUT /{presetId}`, `DELETE`, `PUT /presets/reorder`), intake (`GET /intake`, `POST /intake`, `PUT /intake/total`), reminders (`GET|PUT /reminders`). Routes **are** registered.

**The blocker is the data model, not routing:**

- The restored adapter keeps per-day baseline/entries/receipts as JSON in `command_idempotency`; the diary reads `daily_nutrition_logs.water_intake_ml`; the diary metadata PUT can also overwrite that column. Two writers, two truths.
- Version comparison happens after a plain read with no row lock or conditional update, so two concurrent writers can both pass.
- `CloudNutritionAndWorkoutStores.UpdateNotesAsync` reads the diary and resends the previously read water total with new notes — the server cannot tell a stale notes-only write from an intended absolute water replacement.

**Required resolution before this feature is release-safe:** one authoritative water model with one atomically committed value; explicit versioned operations for *notes* (never touching water) and *absolute water replacement*; conflict (`409`) on stale versions with no mutation; fencing by account + date (including absent-row creation); absolute replacements represented as explicit events/checkpoints so lower totals remain possible without deleting history; and a documented compatibility decision for the installed client. Until then, do not "fix" hydration by picking a winner — that is a data-loss decision.

### 9.10 Body measurements and deletion

- `GET /api/v1/progress/measurements`, `POST`, `PUT /{id:guid}`, `DELETE /{id:guid}`.
- A mutation takes a transaction-scoped account lock to avoid lost concurrent writes (unverified against PostgreSQL — treat as unproven).
- Deletion is permanent for the value: remove it from live rows, history, projections, caches and unprocessed retry payloads; keep only a value-free id/revision so a retried offline request cannot resurrect it. Then issue a safe deletion marker/version.
- Error responses are generic; do not echo the submitted value (an existing path attaches an overflowing value to an exception — do not repeat that pattern).

### 9.11 Progress photos

1. `POST /api/v1/photos/upload-url` → `photo_upload_intents` row + presigned URL.
2. `POST /api/v1/photos/commit` → validates the intent, creates `photo_journey_entries`.
3. `GET /api/v1/photos`, `GET /api/v1/photos/{photoId}/download-url`.
4. `DELETE /api/v1/photos/{photoId}` → row removed, `photo_object_cleanups` row queued (leased, backoff via `not_before_utc`).

**Invariants:** an object is never referenced without a committed intent; cleanup is leased so concurrent workers cannot double-delete; photo keys and URLs are not logged.

### 9.12 Workout reconciliation (background host)

`services/MuscleCuties.WorkoutReconciliation` has been restored and is deployed as the `musclecuties-workout-reconciliation` ARM64 Lambda. It scans one bounded page of pending owners per invocation, uses a distinct discovery login and tenant login, and persists the cursor under a row lock. The authenticated `POST /api/v1/workouts/plan/reconcile` route and the `WorkoutPendingRevisionLambda` handler remain alternate entry points; outcomes remain `Completed` | `Deferred` | `NeedsReview`.

- Idempotent by account + observation revision + active-plan revision + policy version. Re-processing must not double-substitute or reduce sets twice.
- Deferred/NeedsReview is an ordinary outcome; never clear a pending flag to silence an alert.
- The EventBridge rule is enabled at `rate(5 minutes)`; staging has the current schema and `dbcheck` passes. The worker registers a Lambda runtime handler; `NeedsReview` is logged but does not fail the invocation, while technical failures still do. All staging accounts were purged on owner instruction and the DLQ is empty. Failures are retained in `musclecuties-workout-reconciliation-dlq`; there is no separate monitoring dashboard yet.

### 9.13 Catalog and configuration maintenance

`src/MuscleCuties.DbMaintenance` provides model-first `dbinit` (create, secure and seed) and `dbcheck` (drift and SECURITY DEFINER ownership verification), plus catalog and photo maintenance. There is no EF migration chain; `PostgresDbContext` is the schema authority and reviewed SQL lives under `Persistence/Schema`.

---

## 10. HTTP surface

All routes are under `/api/v1` except health. Singular/plural aliases exist for injuries, exercise contraindications, history and swaps.

| Area | Routes |
|---|---|
| Health | `GET /health/live` |
| Auth | `POST /auth/apple/nonce`, `POST /auth/password`, `POST /auth/apple`, `POST /auth/refresh`, `POST /auth/password/reset`, `POST /auth/password/change`, `POST /auth/revoke`, `POST /auth/revoke-refresh`, `GET /auth/email-exists`, `GET /auth/username-exists` |
| Account | `POST /accounts/registration`, `GET /accounts/registration/quiz`, `DELETE /accounts/me` |
| Email challenges | `POST /email-challenges/start`, `/resend`, `/verify` |
| Profile | `GET|PUT /profile`, `PUT /profile/nutrition-preferences`, `PUT /profile/nutrition-targets`, `PUT /profile/workout-preferences` (retired), `GET|PUT /profile/units` |
| Workout planning | `GET /workouts/catalog`, `GET /workouts/plan/exercise-exclusions`, `GET /workouts/plan/style-offer`, `POST /workouts/plan/style-offer/{offerId:guid}/dismiss`, `POST /workouts/plan/strength-preview`, `POST /workouts/plan/strength-proposals`, `POST /workouts/plan/strength-proposals/{proposalId:guid}/accept`, `GET /workouts/plan/lease/active`, `POST /workouts/plan/lease/{action}`, `GET|PUT /workouts/plan/answers`, `POST /workouts/plan/hiit-evidence-challenge`, `POST|GET /workouts/plan` (retired) |
| Workout read model | `GET /workouts/overview`, `GET /workouts/progress`, `PUT /workouts/foreground-timezone` |
| Active session | `POST /workouts/sessions/start`, `GET /workouts/sessions/planned/{workoutDayId}`, `GET .../planned/{workoutDayId}/history`, `GET /workouts/sessions/{sessionId}`, `PUT /workouts/sessions/{sessionId}/sets`, `POST .../timing/resume`, `POST .../timing/checkpoint`, `POST .../finish`, `POST .../cancel` |
| Plan/day management | `POST /workouts/reschedule`, `PUT /workouts/optional-session`, `PUT /workouts/optional-exercise`, `GET /workouts/day-sessions/{workoutDayId}`, `POST /workouts/day-sessions/log`, `GET /workouts/day-exercises/{id}/swaps`, `POST /workouts/day-exercises/swap`, `GET|PUT /workouts/day-exercises/movement-note` |
| Readiness proposals | `POST /workouts/readiness-proposals/review|apply|keep` |
| Injury | `GET|PUT /injuries`, `DELETE /injuries/{id}`, `PUT /injuries/{id:int}` (404), `GET /workouts/injuries/impact`, `GET /workouts/injuries|exercises/{exerciseDefinitionId}/contraindicated|swaps`, `POST /workouts/injuries/swap`, singular aliases `/workouts/injury/...` |
| History | `GET /workouts/history`, `/months`, `/month/{year}/{month}`, `/count`, `/month/{year}/{month}/count`, `/sessions/{sessionId}`, `/personal-records`, `PUT /workouts/history/sessions/{sessionId}/notes`, `PUT /workouts/history/sets/{setId}` |
| Check-ins | `GET|PUT /check-ins`, `DELETE /check-ins/{id}` |
| Cycle | `GET /cycle/history`, `/history/prediction`, `PUT /cycle/history`, `PUT /cycle/history/cycles`, `DELETE /cycle/history/cycles/{id}`, `PUT /cycle/history/symptoms`, `DELETE /cycle/history/symptoms/{id}`, `PUT /cycle/history/phases`, `GET /cycle/overview` |
| Nutrition | `GET /nutrition/diary/day`, `/entries/{itemId}`, `/history`, `/events`, `PUT /nutrition/diary/entries`, `/metadata`, `/notes`, `DELETE /nutrition/diary/entries/{identity}`, `POST /nutrition/diary/batch`, `/copy`, `/copy/preview`, `/copy/commit`, `/quick-add` |
| Nutrition extras | `GET /nutrition/catalog/search`, `POST /nutrition/catalog/barcode|custom-foods`, recipes CRUD + preview/portion-preview, `POST|GET|DELETE /nutrition/templates`, `GET /nutrition/targets|overview|meal-plan|suggestions`, `POST /nutrition/meal-plan/refresh`, `POST|PUT /nutrition/meals`, `GET /nutrition/meals/{mealId}/ingredients` |
| Hydration | `GET /hydration/presets`, `POST /hydration/presets/initialize`, `POST /hydration/presets`, `PUT|DELETE /hydration/presets/{presetId}`, `PUT /hydration/presets/reorder`, `GET|POST /hydration/intake`, `PUT /hydration/intake/total`, `GET|PUT /hydration/reminders` |
| Measurements | `GET|POST /progress/measurements`, `PUT|DELETE /progress/measurements/{id}` |
| Dashboard | `GET /dashboard/overview`, `GET /dashboard/calendar` |
| Photos | `POST /photos/upload-url`, `POST /photos/commit`, `GET /photos`, `GET /photos/{photoId}/download-url`, `DELETE /photos/{photoId}` |

---

## 11. Cross-cutting contracts

### 11.1 Idempotency

`app.command_idempotency` is account-scoped (`user_id` + `command_key`), stores a canonical `request_hash`, and records the stored `response_json` with `completed_at`.

Protocol:

1. Write a pending record (or use the pattern already implemented by the store) keyed by account + command.
2. If the key exists with the **same** `request_hash` and is completed → replay the stored response.
3. If the key exists with a **different** `request_hash` → reject with a conflict; never apply the new payload under an old key.
4. Only mark complete after the mutation commits. A crash before completion must be safely retryable.

`IdempotencyKeyResolver` reads the client key; operations with externally visible effects need an operation id before any ambiguous commit.

### 11.2 Versioning

| Version | Where | Meaning |
|---|---|---|
| `concurrency_version` | `user_profiles` | Optimistic profile write |
| `ProposalVersion`, `LeaseId`, `Fence` | Proposal store | Acceptance must match all three; fence expires in 5 min |
| `ExpectedVersion` | activity answers, water intake/total, timezone | Stale write → conflict, no mutation |
| `planning_revision` | `daily_readiness_logs` | Which check-in revision the planning input came from |
| Catalog hash, policy version | `PlanningInputVersions` | Acceptance rejects a proposal built on a changed catalog/policy |
| Timezone version | `workout_foreground_timezone`, HIIT capture | Day crediting and evidence validity are timezone-version-bound |

### 11.3 Status and error vocabulary

Use the strings the code already defines; do not invent snake_case codes.

- Planning statuses: `Accepted`, `InvalidInput`, `NotFound`, `Conflict`, `Stale`, `Limited`, `InvalidProposal`.
- Planning error codes: `WorkoutPreviewOpen`, `WorkoutPlanningInputsChanged`, `WorkoutAnswersUnavailable`, `WorkoutPreferencesRequireProposal`, `LegacyPlanBypassDisabled`.
- Policy gate: `CycleReadinessPolicyReviewMissing`.
- Reconciliation: `Completed`, `Deferred`, `NeedsReview`.
- Session adjustment: `DailyReadiness`, `ProvisionalPhaseFeeling`, `RepeatedPhasePattern`, `SafetyReview`; confidence `Provisional`, `Repeated`, `Current`.

Error responses carry a stable `code`, a safe human message, and the request id. Never return provider or exception text.

### 11.4 Dates and time zones

- Server stores `timestamptz` for instants and `date`/`local_date` for user days.
- The user's IANA time zone (`planning_time_zone_id`) decides "today"; `WorkoutForegroundTimeZoneRequest` carries `ExpectedVersion`.
- Day-keyed tables (`daily_nutrition_logs.local_date`, `daily_readiness_logs.date`, `cycle_phase_logs.local_date`, `food_item_entries.local_date`) must be written with the resolved local date, consistently across features.

---

## 12. Policy constants and provenance

Recorded values (owner-approved where noted; none are a clinical review):

| Area | Value | Source |
|---|---|---|
| Low-readiness strength sets | multiplier **0.7** | `WorkoutPlanningConfig.GatingConfig.LowReadinessSetMultiplier` |
| Low-readiness RPE cap | **7** | `WorkoutPlanningConfig.GatingConfig.LowReadinessRpeCap` |
| Consecutive low days → rest | **2** | `GatingConfig.ConsecutiveLowDaysToRest` |
| HIIT minimum sleep | **6.5 h** | `GatingConfig.HiitMinSleep` |
| HIIT maximum pain | **1** | `GatingConfig.HiitMaxPain` |
| HIIT menstrual energy floor | **5** in practice (`CreateDefault`), 4 only on a hand-built config | `WorkoutPlanningConfig.CreateDefault` vs `GatingConfig` |
| Check-in bands | ≥75 ready, ≥50 lighter, <50 easier | `ReadinessConfig.HighTier/ModerateTier` |
| Phase priors | Menstrual −10, Luteal −5, others 0 | `DefaultPhasePriors` |
| Phase estimate | 3 completed intervals (4 starts) all agreeing | `WorkoutEstimatedCyclePhaseRule` |
| Established activity | 12 consistent weeks, 2 sessions/week, 28-day returning window | `ActivityExperienceRule` |
| Promotion | 20 credited days → intermediate, 130 → advanced | `WorkoutExperiencePromotionRule` |
| Injury pain/load | 0–3 / 4–6 / 7–10 bands; cleared load capped at 85% of known pre-injury max; 7-day restoration window | `InjurySafetyProgressionRule`, `WorkoutInjuryClearanceRule` |
| Injury location blocks | e.g. knee blocks HIIT+running (acute also climbing); lower back acute blocks HIIT/running/climbing | `InjuryActivityRestrictionRule` |
| Population | 18–64; explicit pregnancy unsupported | `WorkoutPopulationScopeRule` |
| Planning versions | ruleset `MC-PLANNING-0.2`, training policy `0.2.0`, cycle/readiness `MC-CYCLE-READINESS-0.2` | `WorkoutPlanningConstants`, `PlanningReviewApproval` |

**Provenance rule:** every automated policy needs a recorded approval reference, UTC approval date, ruleset version, covered sections, training-policy version and numeric scope. The repository has **no** approval record for the 0.7 multiplier / RPE 7. `PlanningReviewApproval` exists but is constructed only in tests. Missing evidence is a release blocker for the feature that depends on it — never a reason to fabricate one.

---

## 13. Build, test and database workflows

### 13.1 Repository verification

```bash
./scripts/verify.sh
```

Builds `MuscleCuties.sln` in Release with `-p:EnableAppleTargets=false`, then runs four self-contained test projects: `MuscleCuties.Core.Tests`, `MuscleCuties.Mobile.Contracts.Tests`, `MuscleCuties.Registration.Tests`, `MuscleCuties.Server.Tests`. `Registration.Integration.Tests` is intentionally excluded (needs live PostgreSQL). Unused-member analyzers are configured in `.editorconfig` and **fail the build**.

iOS build (requires .NET 10 SDK, MAUI iOS workload, Xcode, and a signing certificate):

```bash
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-arm64
```

Android-only (no Apple signing):

```bash
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -p:EnableAppleTargets=false
```

### 13.2 Database — three distinct paths

| Path | Starting state | Mechanism | Status |
|---|---|---|---|
| Migration-managed | empty database | `DbMaintenance migrate --confirm` | legacy upgrade path only |
| Fresh static install | empty database | `services/postgresql/install.sql` | **stale** vs integrated model |
| Static-install upgrade | populated static schema | not designed | **blocked** |

Fresh install rehearsal (disposable PostgreSQL 17, loopback only):

```bash
psql "$DATABASE_URL" -f services/postgresql/install.sql
```

Requires an administrator able to create roles and `SET ROLE musclecuties_migrator`. The catalog-floor ownership transfer has needed `CREATE` on the `catalog` schema in rehearsal — record, don't grant it blindly.

Static-install integration test (opt-in, needs Docker with `postgres:17-alpine`):

```bash
MC_BOOTSTRAP_DOCKER_TEST=1 dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj --filter FullyQualifiedName~PostgreSqlStaticInstallTests
```

`scripts/generate-static-install.sh` is **execution-disabled** by design; its host validation and secret handling must be fixed before it can run. Never pass credentials as process arguments.

Read-only preflight (only when authorized; never against a shared target):

```sql
select version();
select relname, relrowsecurity, relforcerowsecurity
from pg_catalog.pg_class c
join pg_catalog.pg_namespace n on n.oid = c.relnamespace
where n.nspname = 'app' and c.relkind = 'r' order by relname;
select to_regclass('public."__EFMigrationsHistory"') is not null as has_history;
select rolname, rolsuper, rolbypassrls, rolcanlogin
from pg_catalog.pg_roles where rolname like 'musclecuties\_%' order by rolname;
```

Unknown values stay **unknown**. Do not infer an RDS resource, bucket or role from app config.

### 13.3 Fresh-install acceptance gates

- Script run on an empty disposable database with a recorded manifest checksum.
- Generated drafts diffed against `schema.sql`, every difference attributed.
- Assembled installer validated on a **second** empty database.
- Ownership, grants and RLS checks recorded; runtime role cannot read another tenant without tenant context, and can read its own with it.
- Auth role resolves one account via the lookup functions and cannot select `app.users` directly.
- Read-only target preflight complete, with unknowns listed.

---

## 14. Definition of done (per workstream)

A workstream is done for **review** (not deployment) when:

- [ ] The decision it implements has a code path in the correct layer and at least one test that fails without it.
- [ ] Missing/unknown inputs fail closed with a specific reason code; no fabricated values.
- [ ] Tenant isolation holds: authenticated user id from the token, transaction-local context, RLS enforced, cross-account read/write impossible.
- [ ] Mutations are idempotent where externally visible, and stale versions conflict without mutation.
- [ ] Errors are sanitized; no secrets or health data in logs/responses.
- [ ] Completed history is untouched; deleted values cannot be resurrected.
- [ ] `./scripts/verify.sh` (or the smallest relevant build/test) passes, with the exact commands and output recorded — or the blocker is reported as a blocker.
- [ ] No infrastructure mutation, deployment, S3 upload, role change or migration against a non-disposable target occurred.

---

## 15. Wiring playbook — how to add or repair a feature

Follow this order; skipping a step is how the current blockers happened.

1. **Locate the layers.** Find the existing contract in `UseCases/Features/<Area>` and its PostgreSQL adapter. If either is missing, add the contract there first — not in the server file.
2. **Check the schema.** Confirm every column exists in `services/postgresql/schema.sql`. If the feature needs new columns, add an EF migration in `Infrastructure.PostgreSql` and plan the static-installer regeneration; do not hand-edit `schema.sql`.
3. **Define the contract.** DTOs and store/client interfaces in `UseCases`. Use explicit status/error enums — not booleans that collapse distinct outcomes. Include the version fields the operation needs.
4. **Implement policy in `Core`.** Pure function, versioned config, no I/O. If it depends on a policy approval that does not exist, return the fail-closed outcome.
5. **Implement the adapter.** Transaction + tenant context + execution strategy. Add idempotency for mutations. Never log payload contents.
6. **Map the route.** In `services/MuscleCuties.Server/<Area>/...EndpointMappings.cs`, keep a `const string` route, resolve `sub`, map statuses to HTTP, and translate exceptions into safe codes. Register the mapping in `ApiApplication`.
7. **Update the mobile client.** `Infrastructure` cloud stores call the route through the API client; UI shows before/after and reason, never a bare changed number.
8. **Preserve history.** New writes append; corrections create a new version; deletions are tombstones without values.
9. **Test the contract.** Unit tests for `Core`, contract tests for the DTO/route shape, PostgreSQL integration only against a disposable `mc_test_*` database.
10. **Record the state.** Update `BACKEND_INTEGRATION_LEDGER.md` and the relevant `docs/orchestration/*` note with concrete changes, evidence, and what remains blocked. Do not claim release readiness.

### Quick reference: where things live

| Need | Path |
|---|---|
| Gating/readiness numbers | `src/MuscleCuties.Core/Models/Workout/Planning/WorkoutPlanningConfig.cs` |
| Gating behavior | `src/MuscleCuties.Core/Domain/Workout/WorkoutGatingRule.cs` |
| Proposal/lease lifecycle | `src/MuscleCuties.Core/Domain/Workout/Planning/PlanningLifecycleRule.cs`, `src/MuscleCuties.UseCases/Features/Workout/Planning/StrengthPlanProposalV020.cs` |
| Planning endpoints | `services/MuscleCuties.Server/Workout/Planning/WorkoutPlanningEndpointMappings.cs` |
| Injury rules | `src/MuscleCuties.Core/Domain/Workout/Planning/Injury*Rule.cs` |
| Injury contracts | `src/MuscleCuties.UseCases/Features/Workout/Injury/Contracts/WorkoutInjuryContracts.cs` |
| Planning approval gate | `src/MuscleCuties.Core/Domain/Workout/Planning/CycleReadinessPrescriptionRule.cs` |
| Auth rotation | `src/MuscleCuties.Infrastructure.PostgreSql/Account/Authentication/PostgresAccountAuthenticationUseCase.cs` |
| Tenant context | `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgresTenantContext.cs` |
| Schema truth | `services/postgresql/schema.sql` |
| Reviewed exercise catalog | `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/ReviewData/ReviewedExerciseCatalogV020.json` |
| App composition | `services/MuscleCuties.Server/ApiApplication.cs` |

---

## 16. Forbidden actions (summary)

- Inventing policy values, approvals, substitutions, safety answers, diagnoses, or legacy field values to make code compile.
- Activating a proposal without explicit version-checked acceptance; reinstating direct plan generation.
- Adding a refresh-token grace period or accepting a replaced token to "fix" a lost response.
- Relaxing RLS, broadening roles, or passing a tenant id from the request.
- Echoing exception/provider text, tokens, health payloads, or deleted values in responses or logs.
- Picking a winner between hydration totals without a reviewed reconciliation contract.
- Running the historical EF chain against a populated static schema, or the static installer against a non-empty database.
- Deleting tests or weakening assertions to pass verification.
- Deploying, uploading, migrating a shared database, changing IAM, or activating HealthKit without separate authorization.
- Claiming release readiness, a validated package, or a checksum while implementation and validation are incomplete.
