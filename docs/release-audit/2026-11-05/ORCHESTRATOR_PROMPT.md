# MuscleCuties orchestration prompt

Use this prompt in a continuation of the MuscleCuties project. Read [AGENTS.md](../../../AGENTS.md), [architecture](../../ARCHITECTURE.md), [product workflows](../../PRODUCT_WORKFLOWS.md), [safety](../../SAFETY.md), [verification gates](../../VERIFICATION_GATES.md), and the [implementation plan](IMPLEMENTATION_PLAN.md). Read the target architecture and migration ledger before any shared schema work. Use the [handoff](HANDOFF.md) and [simulator checkpoint](../2026-11-05-simulator-checkpoint.md) to resume.

## Objective and authority

Lead with the Rider Debug data-loading failure on an iPhone simulator. Restore and verify the local API path, then bring the simulator evidence to the owner. TestFlight is the later milestone. The owner selected the corrective Beta candidate `continue_musclecuties_beta_plan` at `6e38535756494ab3e40a75cc74109ad247727213` as the review baseline. Compare it with the Rider checkout and the exact app binary before choosing an execution base. Reuse existing work and preserve unrelated changes.

Read the owner's current authorization and record it in the handoff. The preceding session authorized documentation under `docs/`. If that is still the only scope, prepare a reviewable local fix and validation packet. Begin source edits and simulator build/run when each is authorized. Use approved scope as given. A plan, a selected review branch, a successful check, or a release-ready label grants no additional authority.

## Assign and execute the simulator package

1. Act as Integration authority. Record package ID **P0**, source SHA/tree, branch/worktree, allowed and excluded paths, owners, shared-file reservations, dependencies, and checkpoint. Compare the selected candidate, Rider checkout, and active worktrees before modifying anything.
2. Assign Product A the Debug endpoint and client verification, and Platform B the guarded local API/database startup and read-only correlation. Reserve shared configuration changes to one owner. Use the [handoff input register](HANDOFF.md#inputs-and-credentials) to request only information that is genuinely missing. Generate disposable local values in the local secret store. Collect protected identities and approvals through the owner-controlled destination when a later package needs them.
3. Capture the actual simulator app's effective endpoint and reproduce the failure. The repository defaults Debug to port 5000, where this Mac's AirPlay listener returned 403. Use a verified free loopback port for the app and API, preserve allowlisting, and exercise the guarded disposable PostgreSQL path. Confirm the runtime points to the intended environment.
4. Run affected checks and record exact results. The local gate requires both health endpoints to return 200, anonymous session to return 401, and the previously failing view to load expected synthetic data after sign-in. Record retry/error behavior, source and binary, endpoint, and sanitized request correlation. Review the local evidence with the owner.

## Gate later work

Use the plan's active queue after the simulator review. Product A owns UI, adapters, and workout workflows. Platform B owns workflow verification, schema, release tooling and infrastructure. Integration owns shared contracts, migration numbering, package evidence and review reservations. Apply the documented acceptance matrix and independent review.

Treat Staging, shared PostgreSQL, backend/worker promotion, Apple signing and physical-device acceptance, and TestFlight upload/distribution as separately scoped stages. For each protected action, capture the exact target, candidate, recovery evidence, and matching authorization. Keep DB, backend/worker, and mobile promotion separate and serialize environment mutations through the approved external lock.

## Checkpoint before limits

At each meaningful transition and while context remains, update the scoped repository handoff with the package, base/head SHA and dirty paths, artifacts and endpoint identity, authorizations, reservations, checks and evidence, risks, and the exact next action. Checkpoint before the platform's actual context limit or 200k tokens, whichever comes first. Resume through a supported continuation, reread the checkpoint, and recheck drift and scope before acting. Report verified outcomes, any outstanding gates, and the next owner decision.
