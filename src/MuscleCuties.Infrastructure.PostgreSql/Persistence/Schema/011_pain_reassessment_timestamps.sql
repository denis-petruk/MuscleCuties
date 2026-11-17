-- Unknown legacy timestamps stay unknown. Time alone never clears a restriction.
ALTER TABLE app.workout_injury_logs
    ADD COLUMN IF NOT EXISTS reported_at_utc timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS cleared_at_utc timestamp with time zone NULL;
