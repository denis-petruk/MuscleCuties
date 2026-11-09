-- Migration 007: Make deload application idempotent and keep one active style offer per plan.
-- A deload window is applied to newly materialized plan rows. The marker records which plan has
-- already been adjusted so a retried materialization cannot reduce the same prescription twice.
-- The offers table already has one row per revision; a second revision of the same plan must not
-- stack a second undismissed offer, so only one active offer per plan is allowed.

ALTER TABLE app.workout_account_planning_states
    ADD COLUMN IF NOT EXISTS deload_applied_plan_id uuid;

-- Keep the newest offer per plan before the partial unique index is created.
DELETE FROM app.strength_style_offers stale
USING app.strength_style_offers kept
WHERE stale.user_id = kept.user_id
  AND stale.plan_id = kept.plan_id
  AND stale.dismissed_at_utc IS NULL
  AND kept.dismissed_at_utc IS NULL
  AND stale.id < kept.id;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_strength_style_offers_user_id_plan_id_active"
    ON app.strength_style_offers (user_id, plan_id)
    WHERE dismissed_at_utc IS NULL;
