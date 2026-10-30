-- Candidate reconciliation operation (reviewed, additive, idempotent).
--
-- Finding: the committed 31→59 EF chain creates app.workout_account_planning_states and
-- app.workout_completion_day_credits with RLS and tenant policies, but never grants the runtime
-- role access to either table. Without these grants the application cannot read or write its own
-- planning state or completion-day credit ledger.
--
-- This file is a candidate operation only. No approved reconciliation manifest ships with the
-- repository, so schema-reconcile fails closed until the owner approves the exact target
-- fingerprint, this operation hash, the transaction classification and the history policy.

GRANT SELECT, INSERT, UPDATE, DELETE ON app.workout_account_planning_states TO musclecuties_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON app.workout_completion_day_credits TO musclecuties_app;
