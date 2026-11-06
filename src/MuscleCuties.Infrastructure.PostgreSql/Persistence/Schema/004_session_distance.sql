-- Migration 004: optional distance actual for a completed session.
-- Nullable and additive: PostgreSQL adds a nullable column without a default as a metadata-only
-- change (brief ACCESS EXCLUSIVE lock, no table rewrite), so legacy sessions and older clients keep
-- NULL. Apply this migration before deploying an API/client that sends distanceKm.

ALTER TABLE app.workout_session_logs
    ADD COLUMN IF NOT EXISTS distance_km numeric(8, 2) NULL;
