# Roadmap — MuscleCuties

The active roadmap is maintained at [docs/v1/ROADMAP.md](docs/v1/ROADMAP.md).

## Quick Summary

- **Audit continuation:** Session completion now has a reliable Train refresh on return; verify the full exercise log and automatic return with a controlled synthetic account. Meal chooser paths now preserve the selected meal/time; verify a controlled save and blank-company custom food with optional micronutrients after the server-field rollout. Then finish shared profile typography and vault persistence checks. Today streak icons and Units picker styling are implemented. Keep steps gated until the current-day source and step-goal estimate rule are verified; keep unapproved numerical readiness and clinical suggestions disabled. See the audit.

- **Workout planning v0.2:** Core rules now have a live check-in capture path for optional sleep-quality, soreness, and stress values, timezone association, server timestamping, and PostgreSQL revision storage. Score display, suggestions, and training prescriptions remain gated pending verified section-level policy approval; the repository privacy page is a publication draft without workout sign-off, and the legacy plan generator remains active. Next, persist and connect plan previews/revisions, correct generator fallback and phase behavior, then wire reviewed policies through the API and MAUI. Full task breakdown: `docs/v1/WORKOUT_PLANNING_V02_HANDOFF.md`.

- **iOS page steps:** Verify Profile setup in a fresh simulator registration. Step 2 needs no changes. Today is implemented and its layout/navigation were checked in the simulator; populated phase and trends await backend rollout. Injury log, Cycle, and Train layouts are checked; Injury log save/clear still needs a controlled synthetic interaction after the refresh-error fix. Nutrition Food log history now opens. Continue session, meal-entry, and You work. See [audit](docs/v1/UI_WORKFLOW_AUDIT.md).

- **iOS workflow audit:** Account confirmation countdown spacing is implemented and built. Check it visually during cooldown in a fresh simulator registration. See [audit](docs/v1/UI_WORKFLOW_AUDIT.md).

- **Workout slider and food entry:** The 2–7 slider and repaired Add food search passed simulator checks. Seven-day planning passed nine disposable PostgreSQL combinations; deploy the updated planner and 742-row catalog before testing Save at 7 against the live account. See [active roadmap](docs/v1/ROADMAP.md).

- **Registration code requests:** The client now distinguishes HTTP 429, 503, and other failures with readable messages; 34 registration tests and an iOS simulator build passed. Ship this update in the next signed device candidate, retry with a controlled mailbox, and capture HTTP status if the request still fails. The live synthetic Resend-address challenge passed, and no backend redeploy is currently indicated.

- **Registration Finish backend:** The tested `20261022-registration-finish.zip` is deployed, with code SHA, health, and synthetic validation checks passing. Preserve `20261022-verification-window.zip` for rollback. Retry fresh real-mailbox Finish on a physical iPhone and record success or the returned error code.

- **iOS registration AutoFill:** Include the fixed Resend action and native field traits in the next signed candidate. Verify the website association and Strong Password, Passwords save/login, and available email suggestions on an iPhone; see [active roadmap](docs/v1/ROADMAP.md).

- **Email deliverability:** The `_dmarc.musclecuties.app` TXT record (`v=DMARC1; p=none;`) is published and Resend Insights marks it healthy. The owner chose to keep `noreply@` for now. Next, test received-message authentication and inbox placement with a controlled mailbox. See [email deliverability](docs/EMAIL_DELIVERABILITY.md).
- **Before-OTP error handling:** The empty email-challenge HTTP 429 now maps to a readable rolling-limit message in the client while retaining the five-challenges-per-rolling-day limit. Verify it on the next signed build; the affected mailbox's deliverability retest can resume after that window clears.

- **PostgreSQL Migration Phases (`PG01`–`PG10`):** **Accepted as code foundations**; live service acceptance remains open.
- **Cycle phase consistency (2026-10-22):** The simulator profile now matches the Cycle screen for a manually logged Luteal phase. Include the dashboard manual-log projection fix in the next Lambda deployment and verify Today also shows Luteal.
- **You tab route (2026-10-22):** The simulator crash's unresolved profile route is registered and navigation errors are contained. Replay a nested You preference → another tab → You sequence when live data loads again, then include the fix in the next iOS candidate.
- **Add food sheet sizing (2026-10-22):** The search sheet now leaves space for a visible scrollable result list; `Carrot, raw` was visible in the simulator. Include this layout in the next iOS candidate.
- **Workout split regeneration (2026-10-22):** Train has an action to regenerate from saved preferences and reload the schedule. Verify it in an authenticated simulator session, then roll out the seven-day backend/catalog before testing a saved seven-day split against the live account.
- **Baseline Safety & Beta Packages (`B01`–`B03`):** **Accepted as code foundations**; `PrivacyPolicyUrl` (`https://musclecuties.app/privacy`), registration privacy navigation, and Sign in with Apple token exchange/revocation (`AppleAccountTokenRevoker`) are wired in code.
- **Feature Plans (`P01`–`P06`):** **Product decisions resolved on 2026-10-21 (`P01` saved meal-template shelf retained; `P02` default `Overwrite` copy mode and daily macro target precedence; `P03` first-party/USDA catalog only; `P04` `250/500/750 ml` presets and `22:00–08:00` quiet hours; `P05` 6-site vocabulary and `0` calorie eat-back; `P06` included under `P06-01-v0` after completed external clinical review). Live PostgreSQL integration and physical-device acceptance remain open.**

See [docs/v1/ROADMAP.md](docs/v1/ROADMAP.md), [docs/v1/STATUS.md](docs/v1/STATUS.md), and [docs/OPERATOR_ACTION_MANUAL.md](docs/OPERATOR_ACTION_MANUAL.md) for full delivery matrices, scenario gates, and operator checklists.

**Next release milestone (updated 2026-10-21):** Complete live credential rotation, disposable PostgreSQL integration suites, and live Resend/S3/Apple verification in [submission readiness](docs/SUBMISSION_READINESS.md), then bump `ApplicationVersion` to `3` and archive the signed Release IPA.

The 2026-10-22 Lambda update extends the verified registration receipt to 24 hours after code confirmation; password-recovery receipts remain 10 minutes. Include the Go back label and removal of the Debug demo-code hint in the next signed iOS candidate. Retry registration with a controlled real mailbox, including a pause longer than 10 minutes after verification, before TestFlight acceptance.

The registration screen repair and readable registration error handling are ready in source and passed the iOS simulator build and 29 focused tests. Include them in the next signed candidate, then retry registration on a physical device and capture the actual server error code if completion still fails.

The public quiz API now maps Maintain health to `3`, Luteal to `3`, and Aggressive to `1`. The revised Lambda is deployed and returns a stable registration failure code in a synthetic invalid-email check. Restart the quiz to discard any catalog loaded before reseeding, then retry Finish. The revised iOS candidate remains to be signed and tested.

Include the revised quiz failure wording in the next iOS candidate. It describes a preference save failure and does not ask users to change valid choices.

Verify the direct registration Privacy Policy link and the shorter in-app Privacy summary on a physical device before the next TestFlight submission.

Verify the quiz Go back action on-device: Step 1 should reload staged details, and returning to the quiz should restart questions after a clear confirmation when answers were entered.

Verify the simplified Step 1 layout on an iPhone: measurements and activity types should read as distinct sections, and the top-right logo should be absent.

**UI audit next:** Train’s activity-choice sheet and direct activity entry are verified in the iOS simulator. Continue with the remaining session logging/completion and outstanding page acceptance checks in [the workflow audit](docs/v1/UI_WORKFLOW_AUDIT.md). Changes are pushed to the private GitLab `test-flight-beta` branch.

**GitHub sync (2026-10-23):** [Draft PR #10](https://github.com/denis-petruk/MuscleCuties/pull/10) brings the current beta app to `main` and removes tracked tool-specific instruction files. Complete disposable PostgreSQL verification, live-backend checks, and signed-device acceptance before marking the PR ready for review or merging.
