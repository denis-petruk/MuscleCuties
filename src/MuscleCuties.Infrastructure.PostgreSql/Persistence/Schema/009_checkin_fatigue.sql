-- Migration 009: persist the user's distinct daily fatigue rating.
-- Existing check-ins remain unknown; fatigue is nullable so no historic answer is fabricated.

ALTER TABLE app.daily_readiness_logs
    ADD COLUMN IF NOT EXISTS planning_fatigue integer;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_daily_readiness_logs_planning_fatigue'
          AND conrelid = 'app.daily_readiness_logs'::regclass
    ) THEN
        ALTER TABLE app.daily_readiness_logs
            ADD CONSTRAINT ck_daily_readiness_logs_planning_fatigue
            CHECK (planning_fatigue IS NULL OR planning_fatigue BETWEEN 1 AND 5);
    END IF;
END $$;
