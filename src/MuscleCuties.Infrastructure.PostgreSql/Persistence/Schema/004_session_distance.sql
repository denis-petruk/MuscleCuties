-- Migration 004: optional distance actual for a completed session.
-- Nullable and additive: legacy sessions keep NULL, older clients that omit the field keep
-- finishing unchanged, and fresh model-created databases already contain the column.

ALTER TABLE app.workout_session_logs
    ADD COLUMN IF NOT EXISTS distance_km numeric(8, 2) NULL;
