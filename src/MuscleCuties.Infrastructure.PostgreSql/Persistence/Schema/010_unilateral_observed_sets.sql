-- Migration 010: preserve per-side repetitions for unilateral observed sets.
ALTER TABLE app.exercise_set_logs
    ADD COLUMN IF NOT EXISTS left_side_reps integer NULL,
    ADD COLUMN IF NOT EXISTS right_side_reps integer NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_exercise_set_logs_unilateral_reps'
          AND conrelid = 'app.exercise_set_logs'::regclass
    ) THEN
        ALTER TABLE app.exercise_set_logs
            ADD CONSTRAINT ck_exercise_set_logs_unilateral_reps
            CHECK (
                (left_side_reps IS NULL AND right_side_reps IS NULL) OR
                (left_side_reps IS NOT NULL AND right_side_reps IS NOT NULL AND
                 left_side_reps BETWEEN 1 AND 1000 AND right_side_reps BETWEEN 1 AND 1000)
            );
    END IF;
END $$;
