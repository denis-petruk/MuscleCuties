-- Migration 006: Persist the planning-catalog identity of each reviewed exercise definition.
-- A day exercise refers to catalog.exercises where code = ENGINE_<planningId>, while swaps,
-- candidates and saved preferences use catalog.workout_exercise_definitions.id. The reviewed
-- catalog import writes this mapping; a NULL row is never translated or offered for a swap.

ALTER TABLE catalog.workout_exercise_definitions
    ADD COLUMN IF NOT EXISTS source_exercise_id integer;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'catalog'
          AND tablename = 'workout_exercise_definitions'
          AND indexname = 'IX_workout_exercise_definitions_source_exercise_id'
    ) THEN
        CREATE UNIQUE INDEX "IX_workout_exercise_definitions_source_exercise_id"
            ON catalog.workout_exercise_definitions (source_exercise_id);
    END IF;
END $$;
