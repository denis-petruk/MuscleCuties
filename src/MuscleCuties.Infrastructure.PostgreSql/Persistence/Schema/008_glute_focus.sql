-- Migration 008: persist the user-selected glute focus.
-- Resolution 56 requires direct glute-dominant work only when the user selected that focus, so the
-- preference lives on the profile and planning never infers it from goal, body type or history.

ALTER TABLE app.user_profiles
    ADD COLUMN IF NOT EXISTS glute_focus boolean NOT NULL DEFAULT false;
