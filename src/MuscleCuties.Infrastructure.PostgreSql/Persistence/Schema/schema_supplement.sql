-- Schema objects the EF model cannot express: model-external tables and index expressions.
-- A model-first database must still contain them, so they are created here when absent.
-- Idempotent, and role-free: privileges live in security.sql, which runs after this file.

-- app.password_login_attempts
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'app' AND tablename = 'password_login_attempts') THEN
        CREATE TABLE app.password_login_attempts (
            id uuid NOT NULL,
            scope_key character varying(64) NOT NULL,
            attempted_at_utc timestamp with time zone NOT NULL
        );
        ALTER TABLE ONLY app.password_login_attempts
            ADD CONSTRAINT password_login_attempts_pkey PRIMARY KEY (id);
        CREATE INDEX ix_password_login_attempts_cleanup ON app.password_login_attempts USING btree (attempted_at_utc);
        CREATE INDEX ix_password_login_attempts_scope_time ON app.password_login_attempts USING btree (scope_key, attempted_at_utc DESC);
    END IF;
END $$;

-- app.photo_cleanup_dispatch_state
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'app' AND tablename = 'photo_cleanup_dispatch_state') THEN
        CREATE TABLE app.photo_cleanup_dispatch_state (
            state_id boolean DEFAULT true NOT NULL,
            after_owner_id uuid,
            CONSTRAINT photo_cleanup_dispatch_state_state_id_check CHECK (state_id)
        );
        ALTER TABLE ONLY app.photo_cleanup_dispatch_state
            ADD CONSTRAINT photo_cleanup_dispatch_state_pkey PRIMARY KEY (state_id);
    END IF;
END $$;

-- app.workout_reconciliation_cursor
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'app' AND tablename = 'workout_reconciliation_cursor') THEN
        CREATE TABLE app.workout_reconciliation_cursor (
            singleton boolean DEFAULT true NOT NULL,
            after_owner_id uuid,
            CONSTRAINT workout_reconciliation_cursor_singleton_check CHECK (singleton)
        );
        ALTER TABLE ONLY app.workout_reconciliation_cursor
            ADD CONSTRAINT workout_reconciliation_cursor_pkey PRIMARY KEY (singleton);
    END IF;
END $$;

-- catalog.workout_template_volume_floors
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'catalog' AND tablename = 'workout_template_volume_floors') THEN
        CREATE TABLE catalog.workout_template_volume_floors (
            template_id text NOT NULL,
            template_version text NOT NULL,
            minimum_working_sets integer NOT NULL,
            minimum_exercises integer NOT NULL,
            slot_floors jsonb NOT NULL,
            reviewer text,
            evidence_reference text,
            evidence_sha256 text,
            reviewed_on date,
            CONSTRAINT workout_template_volume_floors_minimum_exercises_check CHECK ((minimum_exercises > 0)),
            CONSTRAINT workout_template_volume_floors_minimum_working_sets_check CHECK ((minimum_working_sets > 0)),
            CONSTRAINT workout_template_volume_floors_slot_floors_check CHECK ((jsonb_typeof(slot_floors) = 'object'::text)),
            CONSTRAINT workout_template_volume_floors_template_id_check CHECK ((length(TRIM(BOTH FROM template_id)) > 0)),
            CONSTRAINT workout_template_volume_floors_template_version_check CHECK ((length(TRIM(BOTH FROM template_version)) > 0))
        );
        ALTER TABLE ONLY catalog.workout_template_volume_floors
            ADD CONSTRAINT workout_template_volume_floors_pkey PRIMARY KEY (template_id);
    END IF;
END $$;


-- Expression index that the EF model cannot declare: a partial unique index over a normalised value.
CREATE UNIQUE INDEX IF NOT EXISTS uq_users_normalized_username
    ON app.users (lower(btrim(username)))
    WHERE username IS NOT NULL AND btrim(username) <> '';

-- Deliberately role-free: security.sql creates the roles and owns every schema privilege, grant and
-- policy, and it runs after this script. Naming a role here would fail on a fresh database.

-- Column defaults the model leaves to SQL. Kept here so inserts that omit a value keep the
-- pre-existing behaviour instead of failing on NOT NULL.
ALTER TABLE app.email_challenge_sessions ALTER COLUMN failed_attempts SET DEFAULT 0;
ALTER TABLE app.photo_object_reconcile_targets ALTER COLUMN lease_generation SET DEFAULT 0;
ALTER TABLE app.user_profiles ALTER COLUMN activity_session_durations_json SET DEFAULT '{}'::jsonb;
ALTER TABLE app.user_profiles ALTER COLUMN available_workout_weekdays_json SET DEFAULT '[]'::jsonb;
ALTER TABLE app.workout_account_planning_states ALTER COLUMN answer_version SET DEFAULT 0;
ALTER TABLE app.workout_account_planning_states ALTER COLUMN cycle_readiness_pending SET DEFAULT false;
ALTER TABLE app.workout_account_planning_states ALTER COLUMN safety_regeneration_pending SET DEFAULT false;
ALTER TABLE app.workout_days ALTER COLUMN is_removed_by_safety SET DEFAULT false;
ALTER TABLE app.workout_plan_proposals_v02 ALTER COLUMN lease_fence SET DEFAULT 0;
ALTER TABLE app.workout_session_logs ALTER COLUMN confirmed_active_seconds SET DEFAULT 0;
ALTER TABLE app.workout_session_logs ALTER COLUMN timing_interval_confirmed_seconds SET DEFAULT 0;

-- The reconciliation host reads this row FOR UPDATE, so a single cursor row must exist from init.
INSERT INTO app.workout_reconciliation_cursor (singleton)
SELECT true
WHERE NOT EXISTS (SELECT 1 FROM app.workout_reconciliation_cursor);
