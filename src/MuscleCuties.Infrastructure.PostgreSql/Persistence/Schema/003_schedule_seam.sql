-- Migration 003: All-session schedule data seam & activity metrics support
-- Idempotent schema evolution ensuring performance indexes and non-destructive constraints exist.

DO $$
BEGIN
    -- Performance index for multi-session schedule queries by user and date
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'app' 
        AND tablename = 'workout_days' 
        AND indexname = 'ix_workout_days_user_date_order'
    ) THEN
        CREATE INDEX ix_workout_days_user_date_order 
        ON app.workout_days USING btree (user_id, date, day_order);
    END IF;

    -- Performance index for exercise log lookups by day exercise ID
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'app' 
        AND tablename = 'workout_exercise_logs' 
        AND indexname = 'ix_workout_exercise_logs_day_exercise'
    ) THEN
        CREATE INDEX ix_workout_exercise_logs_day_exercise 
        ON app.workout_exercise_logs USING btree (user_id, workout_day_exercise_id);
    END IF;
END $$;
