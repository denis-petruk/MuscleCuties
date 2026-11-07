-- Migration 005: Persist real per-set effort and technique provenance.
-- Rows mirrored from aggregate-only legacy clients remain is_observed = false and are never
-- used as double-progression evidence.

ALTER TABLE app.exercise_set_logs
    ADD COLUMN IF NOT EXISTS rir_actual integer;

ALTER TABLE app.exercise_set_logs
    ADD COLUMN IF NOT EXISTS technique_acceptable boolean NOT NULL DEFAULT true;

ALTER TABLE app.exercise_set_logs
    ADD COLUMN IF NOT EXISTS is_observed boolean NOT NULL DEFAULT false;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_exercise_set_logs_rir_actual'
          AND conrelid = 'app.exercise_set_logs'::regclass
    ) THEN
        ALTER TABLE app.exercise_set_logs
            ADD CONSTRAINT ck_exercise_set_logs_rir_actual
            CHECK (rir_actual IS NULL OR rir_actual BETWEEN 0 AND 10);
    END IF;
END $$;
