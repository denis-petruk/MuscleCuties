# Implementation continuation — 2026-10-27

## Checkout and preservation

- Available source: Windows checkout on `codex/plan-generation-checkpoint`, starting at `caff22c`.
- Initial user changes included STATUS interview decisions and untracked HIIT capture contracts, rule, persistence, migrations and synthetic transaction fixtures. They were preserved and relevant unfinished capture code was continued. Unrelated untracked files were not edited.
- `git fetch origin codex/plan-generation-checkpoint` failed: cannot open `.git/FETCH_HEAD`: Permission denied. `git ls-remote` failed to connect to GitLab port 443. The cached remote ref matches HEAD, but live ahead/behind state is unknown.
- Staging the checkpoint failed: cannot create `.git/index.lock`: Permission denied. No commit was created and no push was possible. HEAD remains `caff22c`; none of this continuation's changes is pushed.

## Implemented in source

- Canonical strength preview uses one set per available required slot as its fallback floor. Safely omitted required movements no longer force an impossible complete-template floor. Optional trunk/accessory slots may be removed; exercise safety and timing requirements remain.
- Duration-limited previews expose a known complete minimum. Explicit longer choice updates only draft strength duration; explicit shorter choice regenerates with explained movement omissions. Full-body catalog order preserves lower-body movements first. Apply remains the preference/plan publication boundary.
- HIIT evidence remains valid for thirty account-local calendar days and fails closed on future capture dates or changed timezone version. Challenge consumption remains single-use, bounded and account-owned. Exact lost-response answer retries reuse the challenge and return the recorded result; conflicting reuse is rejected.
- Planned HIIT Start checks current server-stamped capture and qualifying week evidence under the account lock. Unplanned HIIT cannot bypass canonical eligibility. The API/cloud/UI distinguish history confirmation from an ordinary conflict; the user may confirm saved answers, update them, or leave for other activities.
- Questionnaire uncertain saves retain the exact request in memory. Edits after an uncertain save require an authoritative answer refresh and review. No persistent offline write queue was added.
- Onboarding routes into generation using persisted initial preferences, retaining the existing required-question/preview/automatic-first-Apply flow.
- Clearance day is day one of the optional period; required restoration begins on day eight. Narrow exact cleared-injury reconciliation uses account/catalog locks and the existing acceptance savepoint, validates causative lineage, and rolls back mismatched restoration. Proven unrelated cleared history can regenerate without inventing restoration; unknown lineage stays pending.
- Fixed pre-existing compile failures in style selection, list count mapping, schedule classifier imports and synthetic fixtures; aligned relevant fixtures with the current choices, metadata and clearance rules.
- Closed an omission safety bug: incomplete restriction or demand-tag metadata cannot establish that no safe candidate exists. Required movement resolution remains limited instead of converting unknown data into an accepted omission; both missing-metadata variants are tested.

## Verification

- .NET SDK 10.0.403 is available at `C:/Users/Deniska/.dotnet/dotnet.exe`; the default PATH selects unsupported SDK 9.
- Offline test restore uses the installed package cache and an isolated temporary NuGet configuration/profile; no personal NuGet configuration or secrets were read.
- Core/application tests: **915 passed, 2 failed, 917 total**. The remaining failures are unchanged UI consistency assertions: `Canonical_cards_have_no_local_surface_overrides` (InjuryLogContent padding) and `Xaml_buttons_use_shared_styles_without_local_appearance_overrides` (five existing appearances in CycleCalendarCard, CyclePhaseSummarySheet, PasswordRecoveryPage, AppStartupPage and WorkoutHistoryPage). New duration buttons use shared styles.
- Server tests: **82/82 passed**. The synthetic TestServer fixture now uses ephemeral data-protection keys and clears host log providers, avoiding access to personal key storage and Windows Event Log.
- Registration tests: **41/41 passed**. Mobile contract tests: **9/9 passed**, including exact in-memory pending-answer retry.
- PostgreSQL integration test sources compile. A deliberately nonmatching test filter executed **zero tests**; this is not database integration evidence. `MC_REGISTRATION_WRITE_TEST_POSTGRES` is absent. No migrations, backfills, scheduler changes or deployments were run.
- The explicit `net10.0-ios` simulator build failed before application compilation: `MSB4184`, SDK display-name evaluation could not access `C:/Users/Deniska/AppData/Local/Microsoft SDKs`. A supported, accessible iOS/Mac/Xcode build environment and signed-in device verification remain required. No Android, Windows or Mac Catalyst build was run.
- `git diff --check` passes; changed workout preview XAML is well-formed XML. Changed Core/UseCases imports contain no new outward framework or persistence dependency. No properties/commands were deleted or renamed in this continuation's production code. No synchronous wait or EF call was introduced.
- One noncompiling duplicate synthetic test was removed; its safe non-strength remainder and empty-plan scenarios remain covered by two passing public `PreviewStrengthPlanV020UseCase` tests. Existing plans, completed history, and logged-set preservation still require the documented PostgreSQL transaction verification; source review alone does not prove those properties.
- Diff and independent static reviews do not substitute for transaction, runtime, device or deployment verification.

## Remaining source and release gates

1. Persist and execute per-occurrence adjusted prescriptions for demanding same-day activity pairs. Current hard/hard/HIIT pairing restrictions remain; removing them without a coherent dose would misstate the executed prescription.
2. Complete active-injury alternative/omission lineage, empty unsafe-session removal with safe remainder, and broader cleared-injury alternative/restoration cases. Preserve all logged/completed work.
3. Migrate needed performance/recovery/progression/deload inputs and behavior from the retained weekly generator into the canonical lifecycle; do not delete the historical rule until equivalence is proven. Complete and verify the one-time alternative-strength-style offer.
4. Verify full catalog coverage for supported onboarding choices, especially actual missing restriction/timing data and safer high-intensity prescriptions. No invented safety metadata or general clinical-review prerequisite is allowed.
5. Run synthetic disposable-PostgreSQL success, post-write rollback, exact replay, stale preview, lease/takeover, simultaneous Apply, active-session suppression, cleared-injury optional-date and reconciliation races. New HIIT capture schema must be provisioned before its matching backend is released.
6. Complete all permitted test suites, an iOS build on a supported Mac/Xcode, signed-in narrow-device EN/RU preview/Apply/HIIT/injury/restart checks, and deployed revision parity.
7. Resolve Git metadata-write and GitLab network access, verify the live branch, and commit/push reviewed scoped updates without including unrelated files.

## Separate deployment sequence

After source and verification gates pass: review additive migrations and least-privilege grants; execute them only in a separately authorized environment; deploy the matching backend and client together; provision pending-owner discovery credentials and an external dispatcher schedule; review any provable-history backfill separately; complete synthetic-account parity and device acceptance before rollout. Nothing in this checkpoint authorizes a production database action.
