# RM07 candidate checkpoint

**Owner decision, November 5, 2026** Denis confirmed P0 worked and requested RM07. Record this as owner simulator acceptance. The chat confirmation supplied no separate installed binary or retry trace.

**Source and ownership** Product A implemented the work in `continue_musclecuties_beta_plan` at `6e38535756494ab3e40a75cc74109ad247727213`, with Integration owning composition, shared catalog registrations and review. While the work ran, Rider `main` advanced to `18e00858c22d575402f3349c8d77a23a844b3f26` through separate remediation integration. Integration carried the RM07 diff forward into the managed `rm07-current-rider` worktree based on that revision and resolved the three source conflicts in Food Log and Workout Archive. The original candidate diff is preserved. The Debug endpoint edits were concurrent P0 work and are outside the RM07 diff.

| Slice | Local result |
| --- | --- |
| Body measurements | Personal Information loads history and supports create, correct and delete with units, stable command IDs and version checks. A conflict retains the draft. A stale record must be reopened before it can be overwritten. The existing API validates against UTC and the form explains that cutoff when the selected local date leads UTC. |
| Workout notes and attempts | The active exercise can save or clear a movement note. The new guarded route checks the expected exercise under the account lock and preserves older route behavior. Older servers reject the guarded route without a fallback write. Recent planned-session starts and completions load in the intro, using the existing history contract. That contract omits abandoned attempts. |
| Personal records and check-ins | Workout Archive shows personal records from the existing API. Cycle displays recorded sleep and energy for the last 14 days. A missing value remains unknown. Returning or navigating away clears the loaded view. |
| Hydration | Food Log connects existing versioned intake, correction, undo, presets and reminder preference storage. Delivery of water reminders remains outside this implementation. A common diary-day lock preserves the legacy total. On drift between a legacy absolute total and the rich ledger, the rich API reports a conflict and preserves both records. |

**Closed RM07 item (Owner approved November 5, 2026)** Rescheduling is formally gated from internal Beta 1.0.0 to protect schedule and injury-lineage invariants. Deferred to Beta 1.1 for move-to-empty-date-only under optimistic concurrency.

**Local verification** The combined candidate passed 1,190 Core, 56 mobile contract, 44 registration and 113 server tests. The focused disposable PostgreSQL tests passed for guarded movement-note persistence and for hydration creation, readback, correction, undo, retry and mixed-client drift protection. The .NET history and measurement state checks, XML parsing and whitespace checks also passed. Native MAUI iOS simulator build succeeded with 0 errors and 0 warnings.

**Next checkpoint** Simulator walkthrough and screenshot evidence authorized on iPhone 16 simulator with synthetic data. Upfront replacement plan disclosure and auto-activation adopted (reconciled in WORKOUT_POLICY.md). Testers update before using new hydration journal. Arbitrary-date food copying (F05) deferred to Beta 1.1. Release candidate evidence packet TF00–TF02 finalized.
