-- MuscleCuties database security definition.
-- Applied by `DbMaintenance dbinit` after the EF model creates schemas and tables.
-- Idempotent: safe to re-run against an existing database.
--
-- Contents, in dependency order:
--   1. Non-login group roles and memberships
--   2. SECURITY DEFINER functions used by policies and maintenance jobs
--   3. Row level security enablement
--   4. Row level security policies
--   5. Object grants for service roles
--
-- Login roles and their credentials are provisioned outside this repository.

-- 1. Roles
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app') THEN
        CREATE ROLE musclecuties_app NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_auth') THEN
        CREATE ROLE musclecuties_auth NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_discovery_owner') THEN
        CREATE ROLE musclecuties_cleanup_discovery_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_dispatch') THEN
        CREATE ROLE musclecuties_cleanup_dispatch NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_migrator') THEN
        CREATE ROLE musclecuties_migrator NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_photo_purge_stage_owner') THEN
        CREATE ROLE musclecuties_photo_purge_stage_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_photo_reconcile') THEN
        CREATE ROLE musclecuties_photo_reconcile NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_photo_reconcile_owner') THEN
        CREATE ROLE musclecuties_photo_reconcile_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_workout_discovery_owner') THEN
        CREATE ROLE musclecuties_workout_discovery_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_workout_dispatch') THEN
        CREATE ROLE musclecuties_workout_dispatch NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;

DO $$
BEGIN
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO musclecuties_app, musclecuties_auth, musclecuties_migrator', current_database());
END $$;
GRANT musclecuties_cleanup_discovery_owner TO musclecuties_migrator;
GRANT musclecuties_photo_purge_stage_owner TO musclecuties_migrator;
GRANT musclecuties_photo_reconcile_owner TO musclecuties_migrator;
GRANT musclecuties_workout_discovery_owner TO musclecuties_migrator;

-- 2. Functions
CREATE OR REPLACE FUNCTION app.discover_due_photo_cleanup_owners(p_now_utc timestamp with time zone, p_after_owner_id uuid, p_limit integer)
 RETURNS TABLE(owner_id uuid)
 LANGUAGE sql
 STABLE SECURITY DEFINER
 SET search_path TO 'pg_catalog', 'app', 'pg_temp'
AS $function$
    WITH due_owners AS (
        SELECT DISTINCT c.user_id
        FROM app.photo_object_cleanups AS c
        WHERE c.not_before_utc <= p_now_utc
          AND (c.state IN ('Pending', 'Retryable')
               OR (c.state = 'Leased' AND c.lease_expires_at_utc <= p_now_utc))
    ),
    ordered AS (
        SELECT d.user_id, 0 AS segment
        FROM due_owners AS d
        WHERE p_after_owner_id IS NULL OR d.user_id > p_after_owner_id
        UNION ALL
        SELECT d.user_id, 1 AS segment
        FROM due_owners AS d
        WHERE p_after_owner_id IS NOT NULL AND d.user_id <= p_after_owner_id
    )
    SELECT o.user_id
    FROM ordered AS o
    ORDER BY o.segment, o.user_id
    LIMIT LEAST(GREATEST(COALESCE(p_limit, 0), 0), 100)
$function$;
CREATE OR REPLACE FUNCTION app.discover_pending_workout_owners(p_after uuid, p_limit integer)
 RETURNS TABLE(owner_id uuid)
 LANGUAGE sql
 STABLE SECURITY DEFINER
 SET search_path TO 'pg_catalog', 'app', 'pg_temp'
AS $function$
    SELECT s.user_id FROM app.workout_account_planning_states AS s
    WHERE (s.promotion_pending OR s.safety_regeneration_pending OR s.cycle_readiness_pending)
      AND (p_after IS NULL OR s.user_id > p_after)
    ORDER BY s.user_id
    LIMIT LEAST(GREATEST(COALESCE(p_limit, 0), 0), 100)
$function$;
CREATE OR REPLACE FUNCTION app.lock_workout_planning_catalog()
 RETURNS void
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO 'pg_catalog'
AS $function$
BEGIN
    LOCK TABLE catalog.workout_exercise_definitions, catalog.exercise_muscle_contributions,
        catalog.workout_muscle_groups, catalog.goal_tier_weights,
        catalog.workout_template_volume_floors IN SHARE MODE;
END;
$function$;
CREATE OR REPLACE FUNCTION app.lookup_user_by_apple_id(p_apple_user_id text)
 RETURNS TABLE(user_id uuid, apple_user_id text, credential_type text, password_hash text)
 LANGUAGE sql
 STABLE SECURITY DEFINER
 SET search_path TO 'pg_catalog', 'app'
AS $function$
    SELECT u.id, u.apple_user_id, u.credential_type, u.password_hash
    FROM app.users AS u
    WHERE u.apple_user_id = p_apple_user_id
    LIMIT 1
$function$;
CREATE OR REPLACE FUNCTION app.lookup_user_by_email(p_email text)
 RETURNS TABLE(user_id uuid, apple_user_id text, credential_type text, password_hash text)
 LANGUAGE sql
 STABLE SECURITY DEFINER
 SET search_path TO 'pg_catalog', 'app'
AS $function$
    SELECT u.id, u.apple_user_id, u.credential_type, u.password_hash
    FROM app.users AS u
    WHERE u.normalized_email = lower(btrim(p_email))
    LIMIT 1
$function$;
CREATE OR REPLACE FUNCTION app.stage_account_photo_purge(p_user_id uuid, p_target_key text, p_not_before_utc timestamp with time zone)
 RETURNS void
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO 'pg_catalog', 'app', 'pg_temp'
AS $function$
DECLARE
    v_raw_tenant text;
    v_tenant_user_id uuid;
    v_photo_segment text;
BEGIN
    v_raw_tenant := pg_catalog.btrim(COALESCE(pg_catalog.current_setting('app.user_id', true), ''));
    IF v_raw_tenant = '' THEN
        RAISE EXCEPTION 'Account photo purge staging requires an active tenant context (app.user_id).'
            USING ERRCODE = '42501';
    END IF;

    BEGIN
        v_tenant_user_id := v_raw_tenant::uuid;
    EXCEPTION WHEN invalid_text_representation THEN
        RAISE EXCEPTION 'Account photo purge staging requires a valid UUID tenant context (app.user_id).'
            USING ERRCODE = '42501';
    END;

    IF p_user_id IS NULL
       OR p_user_id = '00000000-0000-0000-0000-000000000000'::uuid
       OR v_tenant_user_id = '00000000-0000-0000-0000-000000000000'::uuid
       OR v_tenant_user_id IS DISTINCT FROM p_user_id THEN
        RAISE EXCEPTION 'Account photo purge staging owner does not match the active tenant context.'
            USING ERRCODE = '42501';
    END IF;

    IF p_not_before_utc IS NULL THEN
        RAISE EXCEPTION 'Account photo purge staging requires a non-null not_before_utc timestamp.'
            USING ERRCODE = '22004';
    END IF;

    v_photo_segment := CASE
        WHEN p_target_key IS NOT NULL AND pg_catalog.char_length(p_target_key) >= 32
            THEN pg_catalog.right(p_target_key, 32)
        ELSE ''
    END;

    IF p_target_key IS NULL
       OR pg_catalog.char_length(p_target_key) > 512
       OR v_photo_segment !~ '^[0-9a-f]{32}$'
       OR v_photo_segment = '00000000000000000000000000000000'
       OR (p_target_key <> ('photos/' || (p_user_id::text) || '/' || v_photo_segment)
           AND p_target_key <> ('published-photos/' || (p_user_id::text) || '/' || v_photo_segment)) THEN
        RAISE EXCEPTION 'Account photo purge target key must be a canonical upload or published key for the authenticated owner.'
            USING ERRCODE = '22023';
    END IF;

    INSERT INTO app.photo_object_reconcile_targets (
        id,
        target_key,
        not_before_utc,
        created_at_utc
    )
    VALUES (
        pg_catalog.gen_random_uuid(),
        p_target_key,
        p_not_before_utc,
        pg_catalog.clock_timestamp()
    )
    ON CONFLICT (target_key) DO UPDATE
    SET not_before_utc = GREATEST(
        app.photo_object_reconcile_targets.not_before_utc,
        EXCLUDED.not_before_utc
    );
END;
$function$;

-- Ownership transfer requires CREATE on the schema. Preserve an existing grant, if any,
-- and remove only the temporary grant made here.
DO $$
DECLARE
    already_has_create boolean := pg_catalog.has_schema_privilege(
        'musclecuties_photo_purge_stage_owner', 'app', 'CREATE');
BEGIN
    IF NOT already_has_create THEN
        GRANT CREATE ON SCHEMA app TO musclecuties_photo_purge_stage_owner;
    END IF;

    ALTER FUNCTION app.stage_account_photo_purge(uuid, text, timestamp with time zone)
        OWNER TO musclecuties_photo_purge_stage_owner;

    IF NOT already_has_create THEN
        REVOKE CREATE ON SCHEMA app FROM musclecuties_photo_purge_stage_owner;
    END IF;
END $$;

-- Every SECURITY DEFINER function has a deterministic, non-login owner. The drift inspector
-- verifies these declarations, so ownership cannot silently fall back to whichever login ran
-- dbinit. Ownership transfer needs CREATE on the schema; preserve existing grants and remove
-- only the temporary grant made here.
DO $$
DECLARE
    cleanup_had_create boolean := pg_catalog.has_schema_privilege(
        'musclecuties_cleanup_discovery_owner', 'app', 'CREATE');
    workout_had_create boolean := pg_catalog.has_schema_privilege(
        'musclecuties_workout_discovery_owner', 'app', 'CREATE');
    migrator_had_create boolean := pg_catalog.has_schema_privilege(
        'musclecuties_migrator', 'app', 'CREATE');
BEGIN
    IF NOT cleanup_had_create THEN
        GRANT CREATE ON SCHEMA app TO musclecuties_cleanup_discovery_owner;
    END IF;
    IF NOT workout_had_create THEN
        GRANT CREATE ON SCHEMA app TO musclecuties_workout_discovery_owner;
    END IF;
    IF NOT migrator_had_create THEN
        GRANT CREATE ON SCHEMA app TO musclecuties_migrator;
    END IF;

    ALTER FUNCTION app.discover_due_photo_cleanup_owners(timestamp with time zone, uuid, integer)
        OWNER TO musclecuties_cleanup_discovery_owner;
    ALTER FUNCTION app.discover_pending_workout_owners(uuid, integer)
        OWNER TO musclecuties_workout_discovery_owner;
    ALTER FUNCTION app.lock_workout_planning_catalog()
        OWNER TO musclecuties_migrator;
    ALTER FUNCTION app.lookup_user_by_apple_id(text)
        OWNER TO musclecuties_migrator;
    ALTER FUNCTION app.lookup_user_by_email(text)
        OWNER TO musclecuties_migrator;

    IF NOT cleanup_had_create THEN
        REVOKE CREATE ON SCHEMA app FROM musclecuties_cleanup_discovery_owner;
    END IF;
    IF NOT workout_had_create THEN
        REVOKE CREATE ON SCHEMA app FROM musclecuties_workout_discovery_owner;
    END IF;
    IF NOT migrator_had_create THEN
        REVOKE CREATE ON SCHEMA app FROM musclecuties_migrator;
    END IF;
END $$;

-- 3. Row level security
ALTER TABLE app.command_idempotency ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.command_idempotency FORCE ROW LEVEL SECURITY;
ALTER TABLE app.cycle_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.cycle_logs FORCE ROW LEVEL SECURITY;
ALTER TABLE app.cycle_phase_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.cycle_phase_logs FORCE ROW LEVEL SECURITY;
ALTER TABLE app.daily_nutrition_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.daily_nutrition_logs FORCE ROW LEVEL SECURITY;
ALTER TABLE app.daily_readiness_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.daily_readiness_logs FORCE ROW LEVEL SECURITY;
ALTER TABLE app.exercise_set_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.exercise_set_logs FORCE ROW LEVEL SECURITY;
ALTER TABLE app.food_item_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.food_item_entries FORCE ROW LEVEL SECURITY;
ALTER TABLE app.hiit_evidence_capture_challenges ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.hiit_evidence_capture_challenges FORCE ROW LEVEL SECURITY;
ALTER TABLE app.logged_meal_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.logged_meal_entries FORCE ROW LEVEL SECURITY;
ALTER TABLE app.logged_meals ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.logged_meals FORCE ROW LEVEL SECURITY;
ALTER TABLE app.nutrition_diary_command_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.nutrition_diary_command_events FORCE ROW LEVEL SECURITY;
ALTER TABLE app.nutrition_diary_event_cursors ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.nutrition_diary_event_cursors FORCE ROW LEVEL SECURITY;
ALTER TABLE app.photo_cleanup_dispatch_state ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.photo_cleanup_dispatch_state FORCE ROW LEVEL SECURITY;
ALTER TABLE app.photo_journey_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.photo_journey_entries FORCE ROW LEVEL SECURITY;
ALTER TABLE app.photo_object_cleanups ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.photo_object_cleanups FORCE ROW LEVEL SECURITY;
ALTER TABLE app.photo_object_reconcile_targets ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.photo_object_reconcile_targets FORCE ROW LEVEL SECURITY;
ALTER TABLE app.photo_upload_intents ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.photo_upload_intents FORCE ROW LEVEL SECURITY;
ALTER TABLE app.recipes ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.recipes FORCE ROW LEVEL SECURITY;
ALTER TABLE app.saved_meal_templates ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.saved_meal_templates FORCE ROW LEVEL SECURITY;
ALTER TABLE app.strength_style_offers ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.strength_style_offers FORCE ROW LEVEL SECURITY;
ALTER TABLE app.symptom_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.symptom_logs FORCE ROW LEVEL SECURITY;
ALTER TABLE app.user_exercise_preferences ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.user_exercise_preferences FORCE ROW LEVEL SECURITY;
ALTER TABLE app.user_food_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.user_food_items FORCE ROW LEVEL SECURITY;
ALTER TABLE app.user_meal_template_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.user_meal_template_entries FORCE ROW LEVEL SECURITY;
ALTER TABLE app.user_meal_templates ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.user_meal_templates FORCE ROW LEVEL SECURITY;
ALTER TABLE app.user_movement_notes ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.user_movement_notes FORCE ROW LEVEL SECURITY;
ALTER TABLE app.user_profile_snapshots ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.user_profile_snapshots FORCE ROW LEVEL SECURITY;
ALTER TABLE app.user_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.user_profiles FORCE ROW LEVEL SECURITY;
ALTER TABLE app.user_quiz_responses ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.user_quiz_responses FORCE ROW LEVEL SECURITY;
ALTER TABLE app.users ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.users FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_account_planning_states ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_account_planning_states FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_completion_day_credits ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_completion_day_credits FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_cycle_readiness_occurrences ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_cycle_readiness_occurrences FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_day_exercises ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_day_exercises FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_days ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_days FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_exercise_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_exercise_logs FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_injury_episodes ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_injury_episodes FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_injury_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_injury_logs FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_injury_omitted_slots ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_injury_omitted_slots FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_injury_prescription_links ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_injury_prescription_links FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_injury_prescriptions ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_injury_prescriptions FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_logs FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_plan_proposals_v02 ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_plan_proposals_v02 FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_plan_revisions_v02 ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_plan_revisions_v02 FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_plans ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_plans FORCE ROW LEVEL SECURITY;
ALTER TABLE app.workout_session_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.workout_session_logs FORCE ROW LEVEL SECURITY;

-- 4. Policies
DROP POLICY IF EXISTS command_idempotency_tenant_policy ON app.command_idempotency;
CREATE POLICY command_idempotency_tenant_policy ON app.command_idempotency USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.cycle_logs;
CREATE POLICY tenant_isolation ON app.cycle_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.cycle_phase_logs;
CREATE POLICY tenant_isolation ON app.cycle_phase_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.daily_nutrition_logs;
CREATE POLICY tenant_isolation ON app.daily_nutrition_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.daily_readiness_logs;
CREATE POLICY tenant_isolation ON app.daily_readiness_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.exercise_set_logs;
CREATE POLICY tenant_isolation ON app.exercise_set_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.food_item_entries;
CREATE POLICY tenant_isolation ON app.food_item_entries USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.hiit_evidence_capture_challenges;
CREATE POLICY tenant_isolation ON app.hiit_evidence_capture_challenges USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.logged_meal_entries;
CREATE POLICY tenant_isolation ON app.logged_meal_entries USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.logged_meals;
CREATE POLICY tenant_isolation ON app.logged_meals USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.nutrition_diary_command_events;
CREATE POLICY tenant_isolation ON app.nutrition_diary_command_events USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.nutrition_diary_event_cursors;
CREATE POLICY tenant_isolation ON app.nutrition_diary_event_cursors USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS cleanup_dispatch_cursor_access ON app.photo_cleanup_dispatch_state;
CREATE POLICY cleanup_dispatch_cursor_access ON app.photo_cleanup_dispatch_state TO musclecuties_cleanup_dispatch USING (true) WITH CHECK (true);
DROP POLICY IF EXISTS tenant_isolation ON app.photo_journey_entries;
CREATE POLICY tenant_isolation ON app.photo_journey_entries USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS cleanup_due_owner_discovery ON app.photo_object_cleanups;
CREATE POLICY cleanup_due_owner_discovery ON app.photo_object_cleanups FOR SELECT TO musclecuties_cleanup_discovery_owner USING (true);
DROP POLICY IF EXISTS tenant_isolation ON app.photo_object_cleanups;
CREATE POLICY tenant_isolation ON app.photo_object_cleanups USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS photo_object_reconcile_maintenance ON app.photo_object_reconcile_targets;
CREATE POLICY photo_object_reconcile_maintenance ON app.photo_object_reconcile_targets TO musclecuties_photo_reconcile USING (true) WITH CHECK (true);
DROP POLICY IF EXISTS photo_object_reconcile_owner_access ON app.photo_object_reconcile_targets;
CREATE POLICY photo_object_reconcile_owner_access ON app.photo_object_reconcile_targets TO musclecuties_photo_reconcile_owner USING (true) WITH CHECK (true);
DROP POLICY IF EXISTS photo_object_reconcile_stage_purge ON app.photo_object_reconcile_targets;
CREATE POLICY photo_object_reconcile_stage_purge ON app.photo_object_reconcile_targets TO musclecuties_photo_purge_stage_owner USING (true) WITH CHECK (true);
DROP POLICY IF EXISTS tenant_isolation ON app.photo_upload_intents;
CREATE POLICY tenant_isolation ON app.photo_upload_intents USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.recipes;
CREATE POLICY tenant_isolation ON app.recipes USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.saved_meal_templates;
CREATE POLICY tenant_isolation ON app.saved_meal_templates USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.strength_style_offers;
CREATE POLICY tenant_isolation ON app.strength_style_offers USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.symptom_logs;
CREATE POLICY tenant_isolation ON app.symptom_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.user_exercise_preferences;
CREATE POLICY tenant_isolation ON app.user_exercise_preferences USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.user_food_items;
CREATE POLICY tenant_isolation ON app.user_food_items USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.user_meal_template_entries;
CREATE POLICY tenant_isolation ON app.user_meal_template_entries USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.user_meal_templates;
CREATE POLICY tenant_isolation ON app.user_meal_templates USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.user_movement_notes;
CREATE POLICY tenant_isolation ON app.user_movement_notes USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.user_profile_snapshots;
CREATE POLICY tenant_isolation ON app.user_profile_snapshots USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.user_profiles;
CREATE POLICY tenant_isolation ON app.user_profiles USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.user_quiz_responses;
CREATE POLICY tenant_isolation ON app.user_quiz_responses USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS auth_user_lookup ON app.users;
CREATE POLICY auth_user_lookup ON app.users FOR SELECT USING ((CURRENT_USER = 'musclecuties_migrator'::name));
DROP POLICY IF EXISTS tenant_isolation ON app.users;
CREATE POLICY tenant_isolation ON app.users USING ((id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_account_planning_states;
CREATE POLICY tenant_isolation ON app.workout_account_planning_states USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS workout_pending_discovery ON app.workout_account_planning_states;
CREATE POLICY workout_pending_discovery ON app.workout_account_planning_states FOR SELECT TO musclecuties_workout_discovery_owner USING (true);
DROP POLICY IF EXISTS tenant_isolation ON app.workout_completion_day_credits;
CREATE POLICY tenant_isolation ON app.workout_completion_day_credits USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_cycle_readiness_occurrences;
CREATE POLICY tenant_isolation ON app.workout_cycle_readiness_occurrences USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_day_exercises;
CREATE POLICY tenant_isolation ON app.workout_day_exercises USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_days;
CREATE POLICY tenant_isolation ON app.workout_days USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_exercise_logs;
CREATE POLICY tenant_isolation ON app.workout_exercise_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_injury_episodes;
CREATE POLICY tenant_isolation ON app.workout_injury_episodes USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_injury_logs;
CREATE POLICY tenant_isolation ON app.workout_injury_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_injury_omitted_slots;
CREATE POLICY tenant_isolation ON app.workout_injury_omitted_slots USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_injury_prescription_links;
CREATE POLICY tenant_isolation ON app.workout_injury_prescription_links USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_injury_prescriptions;
CREATE POLICY tenant_isolation ON app.workout_injury_prescriptions USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_logs;
CREATE POLICY tenant_isolation ON app.workout_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_plan_proposals_v02;
CREATE POLICY tenant_isolation ON app.workout_plan_proposals_v02 USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_plan_revisions_v02;
CREATE POLICY tenant_isolation ON app.workout_plan_revisions_v02 USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_plans;
CREATE POLICY tenant_isolation ON app.workout_plans USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));
DROP POLICY IF EXISTS tenant_isolation ON app.workout_session_logs;
CREATE POLICY tenant_isolation ON app.workout_session_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));

-- 5. Grants
GRANT ALL ON FUNCTION app.discover_due_photo_cleanup_owners(p_now_utc timestamp with time zone, p_after_owner_id uuid, p_limit integer) TO musclecuties_cleanup_dispatch;
GRANT ALL ON FUNCTION app.discover_pending_workout_owners(p_after uuid, p_limit integer) TO musclecuties_workout_dispatch;
GRANT ALL ON FUNCTION app.lock_workout_planning_catalog() TO musclecuties_app;
GRANT ALL ON FUNCTION app.stage_account_photo_purge(p_user_id uuid, p_target_key text, p_not_before_utc timestamp with time zone) TO musclecuties_app;
GRANT ALL ON FUNCTION app.stage_account_photo_purge(p_user_id uuid, p_target_key text, p_not_before_utc timestamp with time zone) TO musclecuties_migrator;
GRANT CREATE ON SCHEMA catalog TO musclecuties_migrator;
GRANT INSERT(created_at_utc) ON TABLE app.photo_object_reconcile_targets TO musclecuties_photo_purge_stage_owner;
GRANT INSERT(id) ON TABLE app.photo_object_reconcile_targets TO musclecuties_photo_purge_stage_owner;
GRANT SELECT ON TABLE app.users TO musclecuties_migrator;
GRANT SELECT ON TABLE catalog.exercise_muscle_contributions TO musclecuties_migrator;
GRANT SELECT ON TABLE catalog.goal_tier_weights TO musclecuties_migrator;
GRANT SELECT ON TABLE catalog.workout_exercise_definitions TO musclecuties_migrator;
GRANT SELECT ON TABLE catalog.workout_muscle_groups TO musclecuties_migrator;
GRANT SELECT ON TABLE catalog.workout_template_volume_floors TO musclecuties_app;
GRANT SELECT ON TABLE catalog.workout_template_volume_floors TO musclecuties_migrator;
-- SHARE mode locks the planning catalog against concurrent writes and PostgreSQL requires
-- UPDATE (or DELETE/TRUNCATE) for that lock on top of the SELECT used by the function body.
GRANT UPDATE ON TABLE catalog.exercise_muscle_contributions, catalog.goal_tier_weights,
    catalog.workout_exercise_definitions, catalog.workout_muscle_groups,
    catalog.workout_template_volume_floors TO musclecuties_migrator;
GRANT SELECT(cycle_readiness_pending) ON TABLE app.workout_account_planning_states TO musclecuties_workout_discovery_owner;
GRANT SELECT(lease_expires_at_utc) ON TABLE app.photo_object_cleanups TO musclecuties_cleanup_discovery_owner;
GRANT SELECT(not_before_utc) ON TABLE app.photo_object_cleanups TO musclecuties_cleanup_discovery_owner;
GRANT SELECT(not_before_utc),INSERT(not_before_utc),UPDATE(not_before_utc) ON TABLE app.photo_object_reconcile_targets TO musclecuties_photo_purge_stage_owner;
GRANT SELECT(promotion_pending) ON TABLE app.workout_account_planning_states TO musclecuties_workout_discovery_owner;
GRANT SELECT(safety_regeneration_pending) ON TABLE app.workout_account_planning_states TO musclecuties_workout_discovery_owner;
GRANT SELECT(state) ON TABLE app.photo_object_cleanups TO musclecuties_cleanup_discovery_owner;
GRANT SELECT(target_key),INSERT(target_key) ON TABLE app.photo_object_reconcile_targets TO musclecuties_photo_purge_stage_owner;
GRANT SELECT(user_id) ON TABLE app.photo_object_cleanups TO musclecuties_cleanup_discovery_owner;
GRANT SELECT(user_id) ON TABLE app.workout_account_planning_states TO musclecuties_workout_discovery_owner;
GRANT SELECT,INSERT,DELETE ON TABLE app.workout_injury_prescription_links TO musclecuties_app;
GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE app.hiit_evidence_capture_challenges TO musclecuties_app;
GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE app.strength_style_offers TO musclecuties_app;
GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE app.workout_cycle_readiness_occurrences TO musclecuties_app;
GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE app.workout_injury_episodes TO musclecuties_app;
GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE app.workout_injury_omitted_slots TO musclecuties_app;
GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE app.workout_injury_prescriptions TO musclecuties_app;
GRANT SELECT,INSERT,UPDATE ON TABLE app.photo_object_reconcile_targets TO musclecuties_photo_reconcile;
GRANT SELECT,UPDATE ON TABLE app.photo_cleanup_dispatch_state TO musclecuties_cleanup_dispatch;
GRANT SELECT,UPDATE ON TABLE app.workout_reconciliation_cursor TO musclecuties_workout_dispatch;
GRANT USAGE ON SCHEMA app TO musclecuties_app;
GRANT USAGE ON SCHEMA app TO musclecuties_cleanup_discovery_owner;
GRANT USAGE ON SCHEMA app TO musclecuties_cleanup_dispatch;
GRANT USAGE ON SCHEMA app TO musclecuties_photo_purge_stage_owner;
GRANT USAGE ON SCHEMA app TO musclecuties_photo_reconcile;
GRANT USAGE ON SCHEMA app TO musclecuties_workout_discovery_owner;
GRANT USAGE ON SCHEMA app TO musclecuties_workout_dispatch;
REVOKE ALL ON FUNCTION app.discover_due_photo_cleanup_owners(p_now_utc timestamp with time zone, p_after_owner_id uuid, p_limit integer) FROM PUBLIC;
REVOKE ALL ON FUNCTION app.discover_pending_workout_owners(p_after uuid, p_limit integer) FROM PUBLIC;
REVOKE ALL ON FUNCTION app.lock_workout_planning_catalog() FROM PUBLIC;
REVOKE ALL ON FUNCTION app.lookup_user_by_apple_id(p_apple_user_id text) FROM PUBLIC;
REVOKE ALL ON FUNCTION app.lookup_user_by_email(p_email text) FROM PUBLIC;
REVOKE ALL ON FUNCTION app.stage_account_photo_purge(p_user_id uuid, p_target_key text, p_not_before_utc timestamp with time zone) FROM PUBLIC;

-- Schema usage for the external tables created by schema_supplement.sql; all idempotent.
GRANT USAGE, CREATE ON SCHEMA catalog TO musclecuties_migrator;
GRANT USAGE ON SCHEMA catalog TO musclecuties_app, musclecuties_auth;
GRANT USAGE ON SCHEMA ops TO musclecuties_app, musclecuties_auth;
