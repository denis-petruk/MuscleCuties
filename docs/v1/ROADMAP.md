# Roadmap — PostgreSQL Migration & V1 Feature Release

All migration phases (`PG01`–`PG10`) and baseline safety/beta packages (`B01`–`B03`) are **accepted as code foundations** across `MuscleCuties.Core`, `MuscleCuties.UseCases`, `MuscleCuties.Infrastructure`, `MuscleCuties.Infrastructure.PostgreSql`, `MuscleCuties.Server`, and `MuscleCuties.UI`. Live service verification and expanded-candidate acceptance remain open.

**UI audit next:** Train’s activity-choice sheet is verified in the iOS simulator. Continue session logging/completion and remaining page acceptance checks in [UI_WORKFLOW_AUDIT.md](UI_WORKFLOW_AUDIT.md); the current branch is pushed to the private GitLab project.

Audit handoff: verify the new session and meal flows on iOS; complete Units picker styling, shared profile typography, Today streak icons and step display using a verified data source; and test photo save/reopen before calling the vault fixed. Update the audit after each accepted page. Keep numerical readiness and unapproved clinical guidance gated.

Follow-up: Units picker styling and Today streak icons are now implemented. First verify a source for same-day steps and the source/formula for any displayed goal; current dashboard contracts only provide check-in step values (yesterday and a seven-day average). Keep that card deferred until its inputs can be shown without inference.

Workout planning v0.2: the MAUI check-in now captures optional sleep-quality, soreness, and stress values with timezone association; the API validates and persists them with server time and a revision. Core score/capacity/revision/progression rules remain partially unwired and the legacy generator remains active. Next, encode reviewed policy values in trusted configuration, remove generator fallbacks and automatic phase prescriptions, persist plan proposals and accepted revisions atomically, and connect the API and MAUI preview/acceptance workflow. The owner confirms approval documents are on the privacy-policy page and directs implementation to proceed. Follow `WORKOUT_PLANNING_V02_HANDOFF.md` for the full sequence and test matrix.

Implement the owner-reviewed iOS page changes in [UI_WORKFLOW_AUDIT.md](UI_WORKFLOW_AUDIT.md), then verify them in the simulator and iOS build. The reproduced Nutrition **Food log history** route exit is repaired and verified; prioritize the remaining photo-vault save/display issue and session modal behavior. The audit records visual and flow requests separately from verified implementation.

Account confirmation spacing is implemented and passed the iOS build. Visually check the countdown gap during cooldown on a fresh simulator registration.

Step 1 Profile setup is implemented and built. Check the photo order, measurement card styling, shared activity copy, and strength selection behavior in a fresh simulator registration and Activity Preferences. Step 2 has no requested change. Today is implemented in source and visually checked for card order, workout count, and trend navigation. Include the Today API projection in the next reviewed backend rollout, then verify populated phase and trend data. The qualified-review scope for numerical guidance remains open.

The Injury log sheet is implemented and visually checked for its compact no-active state and selection cards. Verify a controlled save/clear transition before candidate acceptance. Cycle layout and neutral phase context are implemented and visually checked; the detailed guide remains gated pending section-level review. Train's pull-to-refresh and selectors are implemented and visually checked, with live regeneration after a preference edit still open. Nutrition's history route and calculated breakdown are repaired. Continue the remaining session, meal-entry, and You changes. The supplied qualified-review register v0.2 proposes formulas/copy but records no section-level sign-off; keep numerical readiness and automated adjustment behavior gated.

The You header now uses the cycle overview phase; an Oct 22 manual Luteal log displayed correctly in the updated simulator. The dashboard projection has the same manual phase calculation in source, pending the next Lambda deployment and Today-screen verification.

The You tab now declares the route used when returning from a nested profile page; navigation errors are caught. Replay the nested-profile tab sequence once live data loads, then include the fix in the next iOS candidate.

The Add food search sheet now uses the available screen height, leaving room for scrollable search results above Create custom food. The `Carrot, raw` result was visible in the updated simulator. Include this layout in the next iOS candidate.

Train now has a Regenerate workout split action that recreates the plan from saved preferences and reloads the page. Verify it after simulator sign-in; the live seven-day planner and catalog rollout is required before a saved seven-day split can be accepted by the backend.

**Feature Plans (`P01`–`P06`):** Promoted into the next TestFlight candidate on 2026-10-20; product decisions (`P01`–`P06`) were resolved on 2026-10-21. Live PostgreSQL integration and physical-device acceptance rows remain open.

Workout preferences and catalog: the new 2–7 slider passed simulator inspection at 3 and 7, and the seven-day planner plus 742-row catalog passed disposable PostgreSQL integration. Live Lambda and RDS still need the updated code and idempotent seed before saving seven days against the live account. The repaired Add food flow passed simulator inspection; `banana` returned `Banana, raw` from the first-party catalog. If another screen fails, identify its exact action and result. Food Log still labels two requests differently even though both use the first-party catalog; the deployed server has no USDA API-key setting. Keep any new external query generic and the API key out of the app package.

Registration code request: the client status mapping for HTTP 429 and 503 is implemented and verified by 34 registration tests and a clean iOS simulator build. Include it in the next signed iOS candidate. The 429 copy reflects the backend's five requests per email in a rolling 24-hour window. Retry with a controlled mailbox and record the actual HTTP status if Create account still fails. A live synthetic challenge succeeded; no backend deployment is pending from this diagnosis.

Registration Finish backend: the tested `MuscleCuties.Server-net10-linux-arm64-20261022-registration-finish.zip` is deployed. AWS code SHA, health HTTP 200, and synthetic invalid-email HTTP 400 passed. Preserve `20261022-verification-window.zip` for rollback. Retry fresh real-mailbox registration on a physical iPhone and record the resulting code or successful dashboard arrival.

The 2026-10-22 iOS registration update gives Resend a fixed button label with separate cooldown text and sets native AutoFill semantics and password rules. Include it in the next signed candidate. The owner must verify the Apple App ID capability and `musclecuties.app` association file, then check Strong Password generation, credential save/login AutoFill, and available email suggestions on a physical iPhone.

Close [email deliverability](../EMAIL_DELIVERABILITY.md): the Cloudflare DMARC TXT record is published and its Resend warning cleared. The owner chose to keep `noreply@` for now. Verify authentication in a new received message and check inbox placement at the provider that currently places registration mail in spam.

The before-OTP `serverError` report is consistent with the email-challenge HTTP 429 path, which has no response body. The client now presents a readable rate-limit message without changing the production sending limit. Verify it in the next signed iOS candidate.

The 2026-10-22 Lambda update keeps a verified registration receipt valid for 24 hours after code confirmation while password recovery stays at 10 minutes. Build the next signed iOS candidate with the Go back quiz label and no Debug demo-code hint; test a delayed real-mailbox registration on a physical device.

Registration UI and error-message repair (2026-10-21) passed 29 focused tests and an iOS simulator build. Put this source in the next signed candidate and verify the full registration flow on a physical device; record the server error code if completion still fails.

The live public quiz catalog now has corrected mappings for Maintain health, Luteal, and Aggressive. The stable registration error-code response is deployed and passed a synthetic invalid-email check. Restart the quiz to clear any pre-reseed catalog, then retry registration on-device.

Ship the revised English and Russian quiz error copy in the next iOS candidate so a catalog or save failure does not suggest the user's preference choices were wrong.

The registration Privacy Policy link now opens the HTTPS policy directly and aligns beneath the consent checkbox. The in-app Privacy page is shorter. Verify both paths and public policy availability on a physical device.

The quiz now offers Go back. Verify Step 1 restores the staged profile and that the confirmation appears when returning would reset entered quiz answers.

Step 1 now has clearer measurements and activity sections and no top-right logo. Check the final spacing and section hierarchy on a physical iPhone.

For AWS deployment, direct-RDS/Lambda configuration, and App Store Connect / TestFlight operations, refer to the [Operator Action Manual](../OPERATOR_ACTION_MANUAL.md) and [Documentation Index](../README.md). The current deployment has no RDS Proxy. The approved IPv6 Lambda egress route is deployed, `/health/ready` passes, and Resend confirmed a synthetic delivery. Real mailbox verification, S3 media, and receipt issuance remain acceptance gates.

Automatic transient PostgreSQL retries were restored on 2026-10-21 after the explicit EF transaction workflows were wrapped in execution strategies. A live synthetic invalid resend request completed without a server error. A disposable local PostgreSQL integration test terminated the active connection inside an explicit transaction; EF retried and committed exactly one row. Workflow-specific failure injection remains open.

**Submission milestone (updated 2026-10-21): Code blockers resolved; live backend, signing, and device gates remain open.** `PrivacyPolicyUrl` (`https://musclecuties.app/privacy`), registration privacy navigation, Sign in with Apple token exchange/revocation (`AppleAccountTokenRevoker`), and `P01`–`P06` product decisions are now wired in code. Close the remaining live verification, signing, and physical-device gates in [submission readiness](../SUBMISSION_READINESS.md) before bumping `ApplicationVersion` to `3` and submitting.

## 1. PostgreSQL Migration & Cloud Cutover (`PG01`–`PG10`)

| Order | Phase | Status | Summary of Completed Scope |
| --- | --- | --- | --- |
| 1 | `PG01` Inventory, authority, identity & offline contract | **Accepted** | First-party PostgreSQL authority, UUIDv7 tenant keys, and AES-encrypted local display cache boundaries established. |
| 2 | `PG02` PostgreSQL schema, provider, isolation & concurrency | **Accepted** | EF Core PostgreSQL schema (`app` and `reference` schemas), row-level security (`app.user_id`), and concurrency tokens implemented. |
| 3 | `PG03` Server API, accounts, auth & email challenges | **Accepted** | JWT/refresh-token rotation, Apple Sign-In nonce verification, and HMAC email challenge flows implemented. |
| 4 | `PG04` Profiles, cycle, check-ins & target rules | **Accepted** | Server profile snapshots, cycle history/phase projections, readiness check-ins, and nutrition target rules completed. |
| 5 | `PG05` Nutrition catalog, diary, recipes & templates | **Accepted** | Food catalog search, barcode lookup, daily diary ledger, idempotency journal, and recipes/templates completed. |
| 6 | `PG06` Workouts, injuries, history, progress & dashboard | **Accepted** | Workout plan generation, active session logging, exercise swaps/notes, injury logs, and dashboard overview completed. |
| 7 | `PG07` Photos, privacy, purge, cache & native-data boundary | **Accepted** | S3 presigned upload/download, `SkiaPhotoByteSanitizer` EXIF/GPS stripping on commit, profile avatar S3 persistence, encrypted local photo cache, and full `PostgresAccountPurgeStore` tenant + S3 media purge completed. |
| 8 | `PG08` Mobile authenticated API switch & zero SQLite | **Accepted** | All mobile stores routed through `Cloud*` adapters and `IMuscleCutiesBackendApiClient`; legacy SQLite packages and local migration paths removed. |
| 9 | `PG09` Canonical reference seeding & pre-Lambda verification | **Accepted; seven-day candidate pending** | Live catalog last confirmed at 728 rows; local 742-row candidate adds seven-day volume budgets and template, verified by SHA-256 and disposable PostgreSQL integration. |
| 10 | `PG10` AWS Lambda hosting & release readiness | **Accepted code foundation** | `AWS.Lambda.AspNetCoreServer.Hosting` entrypoint, `LambdaHostingConfiguration` bounded PostgreSQL pooling, and EN/RU localization implemented. Current runtime uses direct RDS; this row does not assert a deployed RDS Proxy or release acceptance. |

## 2. Baseline Safety & Beta Readiness (`B01`–`B03`)

| Package | Status | Summary of Completed Scope |
| --- | --- | --- |
| `B01` Privacy & safety baseline | **Accepted** | Explicit privacy consent, `https://musclecuties.app/privacy` policy wiring, reproductive context safety guards, and sensitive data isolation completed. |
| `B02` Cloud identity & recovery | **Accepted** | First-party registration, OTP email verification, password reset, Sign in with Apple token exchange/revocation (`AppleAccountTokenRevoker`), and account deletion completed. |
| `B03` iOS TestFlight foundation | **Accepted foundation; next candidate pending** | Read-only Apple Health step count, iOS camera/photo permissions, and earlier build evidence. The expanded candidate still needs signing (`ApplicationVersion` `3` after Step 1–2 closure) and physical-device acceptance. |

## 3. V1 Feature Extensions (`P01`–`P06`)

> **Candidate Status (2026-10-21):** Product decisions for `P01`–`P06` are locked. Live PostgreSQL integration and physical-device acceptance remain required before release.

| Plan | Feature | Status | Summary of Candidate Scope & Resolved Decisions |
| --- | --- | --- | --- |
| `P01` | Recipes & Meal Templates | **Decision locked; device acceptance open** | In-place recipe editing (`PUT /api/v1/nutrition/recipes/{id:guid}`), ingredient scaling, cooked-weight yield, and portion preview (`POST /api/v1/nutrition/recipes/{id:guid}/portion-preview`) completed. `P01-07` resolved: saved meal-template shelf (`GetRecipesAndTemplatesUseCase` / `SaveMealTemplateUseCase`) retained alongside recipes. |
| `P02` | Nutrition Diary Enhancements | **Decision locked; device acceptance open** | Renameable and archivable default + custom meal categories (up to 8 custom, 40-char max), source-date copy preview and atomic merge/overwrite (`POST /api/v1/nutrition/diary/copy/preview` & `/commit`), consumption timestamps, timeline ordering, and per-meal targets completed. Decisions resolved: default copy mode is `Overwrite`, and daily macro targets (`DiaryTargetPolicy`) remain authoritative over per-meal targets. |
| `P03` | Food Catalog & Barcode Scanner | **Decision locked; camera acceptance open** | Native iOS `AVFoundation` barcode camera scanner (`BarcodeScannerHardwareService`), GTIN/EAN-8/EAN-13/UPC-A normalization, manual fallback, and source provenance uncertainty completed. `P03-05` resolved: barcode lookups restricted to first-party and USDA FoodData Central catalog entries (`FoodCatalogProvenanceRule`). Physical camera/device acceptance pending. |
| `P04` | Hydration Tracking & Reminders | **Decision locked; device acceptance open** | Editable water presets (`250 ml`, `500 ml`, `750 ml` defaults in `DefaultWaterPresets`), atomic daily water intake ledger with undo/correction, and opt-in wall-clock reminder settings with explicit quiet hours (`22:00–08:00` policy in `FixedWaterReminderSettings`) completed (`P04-05` resolved). |
| `P05` | Body Circumference Progress | **Decision locked conservatively; device acceptance open** | Tape circumference logging, correction, deletion, and history (`Waist`, `Hips`, `Chest`, `UpperArm`, `Thigh`, `Calf` in `BodyMeasurementCodes`) following user Metric (`cm`) / Imperial (`in`) settings within `1–500 cm` technical bounds (`/api/v1/progress/measurements`) completed. `P05-01` and `P05-08` resolved conservatively: 6-site vocabulary locked with `0` active calorie eat-back. |
| `P06` | Neutral Fasting Timer | **Decision locked; device acceptance open** | Optional user-initiated elapsed-time record governed by policy `P06-01-v0` with eligibility attestation, pregnancy safety gate, start/pause/resume/end, manual start/end time correction, user-entered planned end time, opt-in local reminder, and EN/RU localization (`/api/v1/fasting/session`) completed. `P06-01` resolved: included in candidate following completed external clinical copy review. |
