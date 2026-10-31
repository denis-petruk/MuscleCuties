-- MuscleCuties application schema (no EF migration runner).
--
-- Generated from the reviewed EF migration chain against PostgreSQL 17.
-- Apply after bootstrap.sql and as musclecuties_migrator; install.sql does both.
-- This file creates application objects only. It intentionally omits
-- public.__EFMigrationsHistory: fresh databases are provisioned by schema.sql.
-- Regenerate from a disposable database after the reviewed migration chain
-- changes; see services/postgresql/README.md.

--
-- PostgreSQL database dump
--


-- Dumped from database version 17.11
-- Dumped by pg_dump version 17.11

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: app; Type: SCHEMA; Schema: -; Owner: -
--



--
-- Name: catalog; Type: SCHEMA; Schema: -; Owner: -
--



--
-- Name: ops; Type: SCHEMA; Schema: -; Owner: -
--



--
-- Name: discover_due_photo_cleanup_owners(timestamp with time zone, uuid, integer); Type: FUNCTION; Schema: app; Owner: -
--

CREATE FUNCTION app.discover_due_photo_cleanup_owners(p_now_utc timestamp with time zone, p_after_owner_id uuid, p_limit integer) RETURNS TABLE(owner_id uuid)
    LANGUAGE sql STABLE SECURITY DEFINER
    SET search_path TO 'pg_catalog', 'app', 'pg_temp'
    AS $$
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
$$;


--
-- Name: lookup_user_by_apple_id(text); Type: FUNCTION; Schema: app; Owner: -
--

CREATE FUNCTION app.lookup_user_by_apple_id(p_apple_user_id text) RETURNS TABLE(user_id uuid, apple_user_id text, credential_type text, password_hash text)
    LANGUAGE sql STABLE SECURITY DEFINER
    SET search_path TO 'pg_catalog', 'app'
    AS $$
    SELECT u.id, u.apple_user_id, u.credential_type, u.password_hash
    FROM app.users AS u
    WHERE u.apple_user_id = p_apple_user_id
    LIMIT 1
$$;


--
-- Name: lookup_user_by_email(text); Type: FUNCTION; Schema: app; Owner: -
--

CREATE FUNCTION app.lookup_user_by_email(p_email text) RETURNS TABLE(user_id uuid, apple_user_id text, credential_type text, password_hash text)
    LANGUAGE sql STABLE SECURITY DEFINER
    SET search_path TO 'pg_catalog', 'app'
    AS $$
    SELECT u.id, u.apple_user_id, u.credential_type, u.password_hash
    FROM app.users AS u
    WHERE u.normalized_email = lower(btrim(p_email))
    LIMIT 1
$$;


--
-- Name: stage_account_photo_purge(uuid, text, timestamp with time zone); Type: FUNCTION; Schema: app; Owner: -
--

CREATE FUNCTION app.stage_account_photo_purge(p_user_id uuid, p_target_key text, p_not_before_utc timestamp with time zone) RETURNS void
    LANGUAGE plpgsql SECURITY DEFINER
    SET search_path TO 'pg_catalog', 'app', 'pg_temp'
    AS $_$
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
$_$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: apple_sign_in_nonces; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.apple_sign_in_nonces (
    id uuid NOT NULL,
    nonce_hash character varying(64) NOT NULL,
    expires_at_utc timestamp with time zone NOT NULL,
    redeemed_at_utc timestamp with time zone
);


--
-- Name: command_idempotency; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.command_idempotency (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    command_key character varying(128) NOT NULL,
    request_hash character varying(64) NOT NULL,
    response_json jsonb,
    completed_at timestamp with time zone,
    CONSTRAINT ck_command_idempotency_completion_pair CHECK ((((response_json IS NULL) AND (completed_at IS NULL)) OR ((response_json IS NOT NULL) AND (completed_at IS NOT NULL))))
);

ALTER TABLE ONLY app.command_idempotency FORCE ROW LEVEL SECURITY;


--
-- Name: cycle_logs; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.cycle_logs (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    start_date date NOT NULL,
    end_date date,
    cycle_length integer NOT NULL,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.cycle_logs FORCE ROW LEVEL SECURITY;


--
-- Name: cycle_phase_logs; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.cycle_phase_logs (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    cycle_log_id uuid,
    phase text NOT NULL,
    flow_intensity text,
    logged_at timestamp with time zone NOT NULL,
    note text,
    created_at timestamp with time zone NOT NULL,
    local_date date NOT NULL
);

ALTER TABLE ONLY app.cycle_phase_logs FORCE ROW LEVEL SECURITY;


--
-- Name: daily_nutrition_logs; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.daily_nutrition_logs (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    local_date date NOT NULL,
    water_intake_ml integer NOT NULL,
    target_calories numeric NOT NULL,
    target_protein numeric NOT NULL,
    target_carbs numeric NOT NULL,
    target_fat numeric NOT NULL,
    total_calories numeric NOT NULL,
    total_protein numeric NOT NULL,
    total_carbs numeric NOT NULL,
    total_fat numeric NOT NULL,
    notes text,
    is_deleted boolean NOT NULL,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.daily_nutrition_logs FORCE ROW LEVEL SECURITY;


--
-- Name: daily_readiness_logs; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.daily_readiness_logs (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    date date NOT NULL,
    sleep_hours numeric,
    sleep3d_avg numeric,
    steps_yesterday integer,
    steps7d_avg integer,
    energy integer,
    pain integer,
    bloating boolean,
    weight_kg numeric,
    sleep_quality integer,
    soreness integer,
    previous_day_load numeric,
    readiness_score integer NOT NULL,
    readiness_tier text NOT NULL,
    phase text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    planning_sleep_quality integer,
    planning_soreness integer,
    planning_stress integer,
    planning_time_zone_id text,
    planning_recorded_at_utc timestamp with time zone,
    planning_revision bigint DEFAULT 0 NOT NULL,
    CONSTRAINT ck_daily_readiness_logs_planning_revision CHECK ((planning_revision >= 0)),
    CONSTRAINT ck_daily_readiness_logs_planning_scales CHECK ((((planning_sleep_quality IS NULL) OR ((planning_sleep_quality >= 1) AND (planning_sleep_quality <= 5))) AND ((planning_soreness IS NULL) OR ((planning_soreness >= 1) AND (planning_soreness <= 5))) AND ((planning_stress IS NULL) OR ((planning_stress >= 1) AND (planning_stress <= 5)))))
);

ALTER TABLE ONLY app.daily_readiness_logs FORCE ROW LEVEL SECURITY;


--
-- Name: email_challenge_sessions; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.email_challenge_sessions (
    id uuid NOT NULL,
    normalized_email character varying(320) NOT NULL,
    purpose integer NOT NULL,
    hmac_key_id character varying(64) NOT NULL,
    code_mac character varying(64) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    expires_at_utc timestamp with time zone NOT NULL,
    resend_available_at_utc timestamp with time zone NOT NULL,
    consumed_at_utc timestamp with time zone,
    superseded_at_utc timestamp with time zone,
    locked_at_utc timestamp with time zone,
    failed_attempts integer DEFAULT 0 NOT NULL,
    receipt_id character varying(128),
    privacy_notice_accepted_at_utc timestamp with time zone,
    privacy_notice_version character varying(64),
    CONSTRAINT ck_email_challenge_session_attempts CHECK ((failed_attempts >= 0)),
    CONSTRAINT ck_email_challenge_session_expiry CHECK ((expires_at_utc > created_at_utc)),
    CONSTRAINT ck_email_challenge_session_purpose CHECK ((purpose = ANY (ARRAY[1, 2])))
);


--
-- Name: email_challenges; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.email_challenges (
    id character varying(128) NOT NULL,
    purpose text NOT NULL,
    redeemed_at_utc timestamp with time zone NOT NULL
);


--
-- Name: exercise_set_logs; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.exercise_set_logs (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    workout_session_log_id uuid NOT NULL,
    exercise_id text,
    exercise_name text NOT NULL,
    set_number integer NOT NULL,
    weight_kg numeric NOT NULL,
    reps_completed integer NOT NULL,
    rpe numeric,
    is_warmup boolean NOT NULL,
    rest_time_seconds integer NOT NULL,
    is_deleted boolean NOT NULL,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.exercise_set_logs FORCE ROW LEVEL SECURITY;


--
-- Name: food_item_entries; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.food_item_entries (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    daily_log_id uuid NOT NULL,
    local_date date NOT NULL,
    meal_type text NOT NULL,
    name text NOT NULL,
    serving_size numeric NOT NULL,
    serving_unit text NOT NULL,
    servings_count numeric NOT NULL,
    calories numeric NOT NULL,
    protein numeric NOT NULL,
    carbs numeric NOT NULL,
    fat numeric NOT NULL,
    logged_at_timestamp timestamp with time zone NOT NULL,
    is_deleted boolean NOT NULL,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.food_item_entries FORCE ROW LEVEL SECURITY;


--
-- Name: logged_meal_entries; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.logged_meal_entries (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    logged_meal_id uuid NOT NULL,
    catalog_food_item_id integer,
    user_food_item_id uuid,
    grams numeric NOT NULL,
    CONSTRAINT ck_logged_meal_entries_exactly_one_food_ref CHECK ((((catalog_food_item_id IS NOT NULL) AND (user_food_item_id IS NULL)) OR ((catalog_food_item_id IS NULL) AND (user_food_item_id IS NOT NULL))))
);

ALTER TABLE ONLY app.logged_meal_entries FORCE ROW LEVEL SECURITY;


--
-- Name: logged_meals; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.logged_meals (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    date date NOT NULL,
    logged_at timestamp with time zone NOT NULL,
    meal_type text NOT NULL,
    accepted_suggestion_name text,
    accepted_suggestion_phase text,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.logged_meals FORCE ROW LEVEL SECURITY;


--
-- Name: nutrition_diary_command_events; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.nutrition_diary_command_events (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    cursor bigint NOT NULL,
    diary_date date NOT NULL,
    command_id character varying(128) NOT NULL
);

ALTER TABLE ONLY app.nutrition_diary_command_events FORCE ROW LEVEL SECURITY;


--
-- Name: nutrition_diary_event_cursors; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.nutrition_diary_event_cursors (
    user_id uuid NOT NULL,
    id uuid NOT NULL,
    last_cursor bigint NOT NULL
);

ALTER TABLE ONLY app.nutrition_diary_event_cursors FORCE ROW LEVEL SECURITY;


--
-- Name: password_login_attempts; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.password_login_attempts (
    id uuid NOT NULL,
    scope_key character varying(64) NOT NULL,
    attempted_at_utc timestamp with time zone NOT NULL
);


--
-- Name: photo_cleanup_dispatch_state; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.photo_cleanup_dispatch_state (
    state_id boolean DEFAULT true NOT NULL,
    after_owner_id uuid,
    CONSTRAINT photo_cleanup_dispatch_state_state_id_check CHECK (state_id)
);

ALTER TABLE ONLY app.photo_cleanup_dispatch_state FORCE ROW LEVEL SECURITY;


--
-- Name: photo_journey_entries; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.photo_journey_entries (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    date date NOT NULL,
    image_path text NOT NULL,
    weight_kg numeric,
    notes text NOT NULL,
    phase text NOT NULL,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.photo_journey_entries FORCE ROW LEVEL SECURITY;


--
-- Name: photo_object_cleanups; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.photo_object_cleanups (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    photo_id uuid NOT NULL,
    s3_key character varying(512) NOT NULL,
    reason character varying(32) NOT NULL,
    not_before_utc timestamp with time zone NOT NULL,
    state character varying(16) NOT NULL,
    attempt_count integer NOT NULL,
    lease_token uuid,
    lease_generation bigint NOT NULL,
    lease_expires_at_utc timestamp with time zone,
    last_failure_category character varying(32),
    last_attempt_at_utc timestamp with time zone,
    created_at_utc timestamp with time zone NOT NULL,
    completed_at_utc timestamp with time zone,
    CONSTRAINT ck_photo_object_cleanups_attempt_count CHECK ((attempt_count >= 0)),
    CONSTRAINT ck_photo_object_cleanups_failure_category CHECK (((last_failure_category IS NULL) OR ((last_failure_category)::text = ANY ((ARRAY['Transient'::character varying, 'Permission'::character varying, 'Configuration'::character varying, 'Provider'::character varying, 'Unknown'::character varying])::text[])))),
    CONSTRAINT ck_photo_object_cleanups_lease CHECK (((((state)::text = 'Leased'::text) AND (lease_token IS NOT NULL) AND (lease_expires_at_utc IS NOT NULL)) OR (((state)::text <> 'Leased'::text) AND (lease_token IS NULL) AND (lease_expires_at_utc IS NULL)))),
    CONSTRAINT ck_photo_object_cleanups_reason CHECK (((reason)::text = ANY ((ARRAY['CancelledUpload'::character varying, 'ExpiredUpload'::character varying, 'CommittedPhoto'::character varying])::text[]))),
    CONSTRAINT ck_photo_object_cleanups_state CHECK (((state)::text = ANY ((ARRAY['Pending'::character varying, 'Leased'::character varying, 'Retryable'::character varying, 'Completed'::character varying])::text[])))
);

ALTER TABLE ONLY app.photo_object_cleanups FORCE ROW LEVEL SECURITY;


--
-- Name: photo_object_reconcile_targets; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.photo_object_reconcile_targets (
    id uuid NOT NULL,
    target_key character varying(512) NOT NULL,
    not_before_utc timestamp with time zone NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    lease_token uuid,
    lease_generation bigint DEFAULT 0 NOT NULL,
    lease_expires_at_utc timestamp with time zone,
    CONSTRAINT ck_photo_object_reconcile_targets_lease CHECK ((((lease_token IS NULL) AND (lease_expires_at_utc IS NULL)) OR ((lease_token IS NOT NULL) AND (lease_expires_at_utc IS NOT NULL))))
);

ALTER TABLE ONLY app.photo_object_reconcile_targets FORCE ROW LEVEL SECURITY;


--
-- Name: photo_upload_intents; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.photo_upload_intents (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    idempotency_key character varying(128) NOT NULL,
    s3_key character varying(512) NOT NULL,
    content_type character varying(128) NOT NULL,
    declared_size_bytes bigint NOT NULL,
    sha256_checksum character varying(64) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    expires_at_utc timestamp with time zone NOT NULL,
    state character varying(16) NOT NULL,
    CONSTRAINT ck_photo_upload_intents_expiry CHECK ((expires_at_utc > created_at_utc)),
    CONSTRAINT ck_photo_upload_intents_size_positive CHECK ((declared_size_bytes > 0)),
    CONSTRAINT ck_photo_upload_intents_state CHECK (((state)::text = ANY ((ARRAY['Pending'::character varying, 'Committed'::character varying, 'Cancelled'::character varying, 'Expired'::character varying, 'Deleted'::character varying])::text[])))
);

ALTER TABLE ONLY app.photo_upload_intents FORCE ROW LEVEL SECURITY;


--
-- Name: recipes; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.recipes (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    name text NOT NULL,
    snapshot_json jsonb NOT NULL,
    created_at_utc timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.recipes FORCE ROW LEVEL SECURITY;


--
-- Name: refresh_sessions; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.refresh_sessions (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    family_id uuid NOT NULL,
    token_hash character varying(64) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    expires_at_utc timestamp with time zone NOT NULL,
    revoked_at_utc timestamp with time zone,
    replaced_at_utc timestamp with time zone,
    CONSTRAINT ck_refresh_sessions_expiry CHECK ((expires_at_utc > created_at_utc))
);


--
-- Name: revoked_access_tokens; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.revoked_access_tokens (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    token_id character varying(128) NOT NULL,
    expires_at_utc timestamp with time zone NOT NULL,
    revoked_at_utc timestamp with time zone NOT NULL
);


--
-- Name: saved_meal_templates; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.saved_meal_templates (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    name text NOT NULL,
    meal_type text NOT NULL,
    snapshot_json jsonb NOT NULL,
    created_at_utc timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.saved_meal_templates FORCE ROW LEVEL SECURITY;


--
-- Name: source_account_claim_nonces; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.source_account_claim_nonces (
    id uuid NOT NULL,
    destination_user_id uuid NOT NULL,
    source_installation_key_hash character varying(64) NOT NULL,
    source_account_key_hash character varying(64) NOT NULL,
    nonce_hash character varying(64) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    expires_at_utc timestamp with time zone NOT NULL,
    redeemed_at_utc timestamp with time zone,
    CONSTRAINT ck_source_account_claim_expiry CHECK ((expires_at_utc > created_at_utc))
);


--
-- Name: source_account_mappings; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.source_account_mappings (
    id uuid NOT NULL,
    claim_id uuid NOT NULL,
    destination_user_id uuid NOT NULL,
    source_installation_key_hash character varying(64) NOT NULL,
    source_account_key_hash character varying(64) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL
);


--
-- Name: symptom_logs; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.symptom_logs (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    cycle_log_id uuid NOT NULL,
    date date NOT NULL,
    symptom_type text NOT NULL,
    severity integer NOT NULL,
    notes text,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.symptom_logs FORCE ROW LEVEL SECURITY;


--
-- Name: user_exercise_preferences; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.user_exercise_preferences (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    original_exercise_id integer NOT NULL,
    preferred_exercise_id integer NOT NULL,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.user_exercise_preferences FORCE ROW LEVEL SECURITY;


--
-- Name: user_food_items; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.user_food_items (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    name text NOT NULL,
    calories numeric,
    protein numeric,
    carbs numeric,
    fats numeric,
    fiber numeric,
    iron numeric,
    vitamin_b12 numeric,
    vitamin_c numeric,
    vitamin_d numeric,
    vitamin_a numeric,
    vitamin_b6 numeric,
    folate numeric,
    calcium numeric,
    magnesium numeric,
    zinc numeric,
    potassium numeric,
    fdc_id integer,
    data_type text,
    brand_owner text,
    brand_name text,
    gtin_upc text,
    ingredients text,
    serving_size numeric,
    serving_size_unit text,
    serving_options_json jsonb,
    last_synced_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.user_food_items FORCE ROW LEVEL SECURITY;


--
-- Name: user_meal_template_entries; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.user_meal_template_entries (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    meal_template_id uuid NOT NULL,
    catalog_food_item_id integer,
    user_food_item_id uuid,
    grams numeric NOT NULL,
    CONSTRAINT ck_user_meal_template_entries_exactly_one_food_ref CHECK ((((catalog_food_item_id IS NOT NULL) AND (user_food_item_id IS NULL)) OR ((catalog_food_item_id IS NULL) AND (user_food_item_id IS NOT NULL))))
);

ALTER TABLE ONLY app.user_meal_template_entries FORCE ROW LEVEL SECURITY;


--
-- Name: user_meal_templates; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.user_meal_templates (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    name text NOT NULL,
    description text,
    meal_type text NOT NULL,
    dietary_tags text NOT NULL,
    phase_tags text NOT NULL,
    sort_order integer NOT NULL,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.user_meal_templates FORCE ROW LEVEL SECURITY;


--
-- Name: user_movement_notes; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.user_movement_notes (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    exercise_id integer NOT NULL,
    exercise_name text NOT NULL,
    notes text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.user_movement_notes FORCE ROW LEVEL SECURITY;


--
-- Name: user_profile_snapshots; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.user_profile_snapshots (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    snapshot_reason text NOT NULL,
    profile_json jsonb NOT NULL,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.user_profile_snapshots FORCE ROW LEVEL SECURITY;


--
-- Name: user_profiles; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.user_profiles (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    name text NOT NULL,
    date_of_birth date,
    height numeric NOT NULL,
    weight numeric NOT NULL,
    goal text NOT NULL,
    weight_goal_pace text NOT NULL,
    training_experience_level text NOT NULL,
    cycle_tracking_mode text NOT NULL,
    current_cycle_phase text,
    workout_days_per_week integer NOT NULL,
    cycle_length integer NOT NULL,
    dietary_tags text NOT NULL,
    session_duration_minutes integer NOT NULL,
    equipment_level text NOT NULL,
    phase_baselines_json jsonb NOT NULL,
    preferred_workout_activity_types text NOT NULL,
    unit_system text NOT NULL,
    body_weight_unit text NOT NULL,
    food_mass_unit text NOT NULL,
    height_unit text NOT NULL,
    distance_unit text NOT NULL,
    energy_unit text NOT NULL,
    nutrition_goals_json jsonb NOT NULL,
    reproductive_context_json jsonb NOT NULL,
    profile_image_path text NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    concurrency_version integer DEFAULT 1 NOT NULL
);

ALTER TABLE ONLY app.user_profiles FORCE ROW LEVEL SECURITY;


--
-- Name: user_quiz_responses; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.user_quiz_responses (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    quiz_question_id integer NOT NULL,
    quiz_answer_id integer NOT NULL,
    answered_at timestamp with time zone NOT NULL,
    user_profile_snapshot_id uuid
);

ALTER TABLE ONLY app.user_quiz_responses FORCE ROW LEVEL SECURITY;


--
-- Name: users; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.users (
    id uuid NOT NULL,
    username text,
    email text NOT NULL COLLATE pg_catalog."C",
    normalized_email text GENERATED ALWAYS AS (lower(btrim(email))) STORED NOT NULL,
    apple_user_id text,
    credential_type text NOT NULL,
    password_hash text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    is_onboarding_complete boolean NOT NULL,
    privacy_notice_accepted_at_utc timestamp with time zone,
    privacy_notice_version character varying(64)
);

ALTER TABLE ONLY app.users FORCE ROW LEVEL SECURITY;


--
-- Name: workout_day_exercises; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.workout_day_exercises (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    workout_day_id uuid NOT NULL,
    exercise_id integer NOT NULL,
    sets integer NOT NULL,
    reps integer NOT NULL,
    duration_seconds integer
);

ALTER TABLE ONLY app.workout_day_exercises FORCE ROW LEVEL SECURITY;


--
-- Name: workout_days; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.workout_days (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    workout_plan_id uuid NOT NULL,
    day_of_week integer NOT NULL,
    workout_type text NOT NULL,
    name text NOT NULL,
    week_index integer NOT NULL,
    date date
);

ALTER TABLE ONLY app.workout_days FORCE ROW LEVEL SECURITY;


--
-- Name: workout_exercise_logs; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.workout_exercise_logs (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    workout_log_id uuid NOT NULL,
    workout_day_exercise_id uuid NOT NULL,
    exercise_id integer NOT NULL,
    completed_sets integer NOT NULL,
    completed_reps integer NOT NULL,
    weight_kg numeric,
    completed_duration_seconds integer,
    distance_km numeric,
    average_heart_rate_bpm integer,
    pace_seconds_per_km integer,
    power_watts integer,
    cadence_rpm integer,
    effort_rating integer,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.workout_exercise_logs FORCE ROW LEVEL SECURITY;


--
-- Name: workout_injury_logs; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.workout_injury_logs (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    site_flag integer NOT NULL,
    status text NOT NULL,
    since date NOT NULL,
    pain integer NOT NULL,
    date date NOT NULL,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.workout_injury_logs FORCE ROW LEVEL SECURITY;


--
-- Name: workout_logs; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.workout_logs (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    workout_day_id uuid NOT NULL,
    date date NOT NULL,
    completion_percent integer NOT NULL,
    notes text,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.workout_logs FORCE ROW LEVEL SECURITY;


--
-- Name: workout_plans; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.workout_plans (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    name text NOT NULL,
    is_active boolean NOT NULL,
    cycle_phase_target text,
    created_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.workout_plans FORCE ROW LEVEL SECURITY;


--
-- Name: workout_session_logs; Type: TABLE; Schema: app; Owner: -
--

CREATE TABLE app.workout_session_logs (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    workout_name text NOT NULL,
    started_at timestamp with time zone NOT NULL,
    completed_at timestamp with time zone,
    total_duration_seconds integer NOT NULL,
    total_volume_kg numeric NOT NULL,
    notes text,
    rpe_rating integer,
    is_deleted boolean NOT NULL,
    deleted_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY app.workout_session_logs FORCE ROW LEVEL SECURITY;


--
-- Name: exercise_muscle_contributions; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.exercise_muscle_contributions (
    id integer NOT NULL,
    seed_code text NOT NULL,
    exercise_id integer NOT NULL,
    muscle_group_id integer NOT NULL,
    fraction numeric NOT NULL
);


--
-- Name: exercise_muscle_contributions_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.exercise_muscle_contributions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.exercise_muscle_contributions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: exercises; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.exercises (
    id integer NOT NULL,
    seed_code text NOT NULL,
    code character varying(80) NOT NULL,
    name character varying(160) NOT NULL,
    description character varying(2000) NOT NULL,
    image_url character varying(1000),
    video_url character varying(1000),
    technique_notes character varying(2000),
    primary_muscle text NOT NULL,
    secondary_muscles character varying(500),
    joint_areas character varying(500) NOT NULL,
    is_injury_friendly boolean NOT NULL
);


--
-- Name: exercises_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.exercises ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.exercises_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: food_item_versions; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.food_item_versions (
    id integer NOT NULL,
    food_item_id integer NOT NULL,
    nutrient_snapshot jsonb NOT NULL,
    versioned_at timestamp with time zone NOT NULL,
    change_source text NOT NULL
);


--
-- Name: food_item_versions_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.food_item_versions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.food_item_versions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: food_items; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.food_items (
    id integer NOT NULL,
    seed_code text NOT NULL,
    name character varying(240) NOT NULL,
    calories numeric,
    protein numeric,
    carbs numeric,
    fats numeric,
    fiber numeric,
    iron numeric,
    vitamin_b12 numeric,
    vitamin_c numeric,
    vitamin_d numeric,
    vitamin_a numeric,
    vitamin_b6 numeric,
    folate numeric,
    calcium numeric,
    magnesium numeric,
    zinc numeric,
    potassium numeric,
    fdc_id integer,
    data_type character varying(40),
    brand_owner character varying(240),
    brand_name character varying(240),
    gtin_upc character varying(40),
    ingredients character varying(4000),
    serving_size numeric,
    serving_size_unit character varying(40),
    serving_options_json jsonb,
    last_synced_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT ck_food_items_fdc_seed_code_matches_id CHECK (((seed_code !~~ 'fdc:%'::text) OR ((fdc_id IS NOT NULL) AND (seed_code = ('fdc:'::text || (fdc_id)::text)))))
);


--
-- Name: food_items_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.food_items ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.food_items_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: goal_tier_weights; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.goal_tier_weights (
    id integer NOT NULL,
    seed_code text NOT NULL,
    goal text NOT NULL,
    muscle_group_id integer NOT NULL,
    tier text NOT NULL
);


--
-- Name: goal_tier_weights_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.goal_tier_weights ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.goal_tier_weights_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: meal_template_entries; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.meal_template_entries (
    id integer NOT NULL,
    seed_code text NOT NULL,
    meal_template_id integer NOT NULL,
    food_item_id integer NOT NULL,
    grams numeric NOT NULL
);


--
-- Name: meal_template_entries_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.meal_template_entries ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.meal_template_entries_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: meal_templates; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.meal_templates (
    id integer NOT NULL,
    seed_code text NOT NULL,
    name character varying(160) NOT NULL,
    description character varying(1000),
    meal_type text NOT NULL,
    dietary_tags character varying(250) NOT NULL,
    phase_tags character varying(250) NOT NULL,
    sort_order integer NOT NULL,
    is_system boolean NOT NULL,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: meal_templates_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.meal_templates ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.meal_templates_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: quiz_answers; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.quiz_answers (
    id integer NOT NULL,
    seed_code text NOT NULL,
    question_id integer NOT NULL,
    text character varying(160) NOT NULL,
    order_index integer NOT NULL,
    mapped_value integer NOT NULL
);


--
-- Name: quiz_answers_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.quiz_answers ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.quiz_answers_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: quiz_questions; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.quiz_questions (
    id integer NOT NULL,
    seed_code text NOT NULL,
    question character varying(250) NOT NULL,
    order_index integer NOT NULL,
    question_type text NOT NULL
);


--
-- Name: quiz_questions_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.quiz_questions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.quiz_questions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: session_archetypes; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.session_archetypes (
    id integer NOT NULL,
    seed_code text NOT NULL,
    code character varying(10) NOT NULL,
    display_name character varying(60) NOT NULL,
    is_lower_dominant boolean NOT NULL,
    contains_heavy_hinge boolean NOT NULL
);


--
-- Name: session_archetypes_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.session_archetypes ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.session_archetypes_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: slot_templates; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.slot_templates (
    id integer NOT NULL,
    seed_code text NOT NULL,
    archetype_id integer NOT NULL,
    "order" smallint NOT NULL,
    block text NOT NULL,
    allowed_patterns_json character varying(500) NOT NULL,
    primary_muscle_id integer NOT NULL,
    sets_min smallint NOT NULL,
    sets_max smallint NOT NULL,
    reps_min smallint NOT NULL,
    reps_max smallint NOT NULL,
    target_rir smallint NOT NULL,
    droppable boolean NOT NULL,
    superset_group smallint NOT NULL
);


--
-- Name: slot_templates_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.slot_templates ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.slot_templates_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: volume_budget_rows; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.volume_budget_rows (
    id integer NOT NULL,
    seed_code text NOT NULL,
    days_per_week integer NOT NULL,
    muscle_group_id integer NOT NULL,
    fractional_sets numeric NOT NULL
);


--
-- Name: volume_budget_rows_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.volume_budget_rows ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.volume_budget_rows_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: week_templates; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.week_templates (
    id integer NOT NULL,
    seed_code text NOT NULL,
    days_per_week integer NOT NULL,
    archetype_sequence_json character varying(100) NOT NULL
);


--
-- Name: week_templates_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.week_templates ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.week_templates_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workout_exercise_definitions; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.workout_exercise_definitions (
    id integer NOT NULL,
    seed_code text NOT NULL,
    name character varying(120) NOT NULL,
    pattern text NOT NULL,
    required_equipment text NOT NULL,
    long_length_loaded smallint NOT NULL,
    fatigue_cost smallint NOT NULL,
    skill_demand smallint NOT NULL,
    setup_seconds integer NOT NULL,
    seconds_per_rep smallint NOT NULL,
    eligible_for_hi_block boolean NOT NULL,
    unilateral_doubles_time boolean NOT NULL,
    is_bodyweight boolean NOT NULL,
    contraindications text NOT NULL,
    preferred_for text NOT NULL,
    supine_or_prone smallint NOT NULL,
    valsalva_demand smallint NOT NULL,
    substitute_group_id integer
);


--
-- Name: workout_exercise_definitions_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.workout_exercise_definitions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.workout_exercise_definitions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workout_muscle_groups; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.workout_muscle_groups (
    id integer NOT NULL,
    seed_code text NOT NULL,
    name character varying(40) NOT NULL
);


--
-- Name: workout_muscle_groups_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.workout_muscle_groups ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.workout_muscle_groups_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workout_planning_config_entries; Type: TABLE; Schema: catalog; Owner: -
--

CREATE TABLE catalog.workout_planning_config_entries (
    id integer NOT NULL,
    seed_code text NOT NULL,
    section character varying(30) NOT NULL,
    key character varying(60) NOT NULL,
    value character varying(500) NOT NULL
);


--
-- Name: workout_planning_config_entries_id_seq; Type: SEQUENCE; Schema: catalog; Owner: -
--

ALTER TABLE catalog.workout_planning_config_entries ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME catalog.workout_planning_config_entries_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: food_sync_logs; Type: TABLE; Schema: ops; Owner: -
--

CREATE TABLE ops.food_sync_logs (
    id integer NOT NULL,
    started_at timestamp with time zone NOT NULL,
    completed_at timestamp with time zone,
    items_upserted integer NOT NULL,
    items_failed integer NOT NULL,
    status text NOT NULL,
    error_details text
);


--
-- Name: food_sync_logs_id_seq; Type: SEQUENCE; Schema: ops; Owner: -
--

ALTER TABLE ops.food_sync_logs ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME ops.food_sync_logs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: command_idempotency PK_command_idempotency; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.command_idempotency
    ADD CONSTRAINT "PK_command_idempotency" PRIMARY KEY (id);


--
-- Name: cycle_logs PK_cycle_logs; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.cycle_logs
    ADD CONSTRAINT "PK_cycle_logs" PRIMARY KEY (id);


--
-- Name: cycle_phase_logs PK_cycle_phase_logs; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.cycle_phase_logs
    ADD CONSTRAINT "PK_cycle_phase_logs" PRIMARY KEY (id);


--
-- Name: daily_nutrition_logs PK_daily_nutrition_logs; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.daily_nutrition_logs
    ADD CONSTRAINT "PK_daily_nutrition_logs" PRIMARY KEY (id);


--
-- Name: daily_readiness_logs PK_daily_readiness_logs; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.daily_readiness_logs
    ADD CONSTRAINT "PK_daily_readiness_logs" PRIMARY KEY (id);


--
-- Name: email_challenges PK_email_challenges; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.email_challenges
    ADD CONSTRAINT "PK_email_challenges" PRIMARY KEY (id);


--
-- Name: exercise_set_logs PK_exercise_set_logs; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.exercise_set_logs
    ADD CONSTRAINT "PK_exercise_set_logs" PRIMARY KEY (id);


--
-- Name: food_item_entries PK_food_item_entries; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.food_item_entries
    ADD CONSTRAINT "PK_food_item_entries" PRIMARY KEY (id);


--
-- Name: logged_meal_entries PK_logged_meal_entries; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.logged_meal_entries
    ADD CONSTRAINT "PK_logged_meal_entries" PRIMARY KEY (id);


--
-- Name: logged_meals PK_logged_meals; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.logged_meals
    ADD CONSTRAINT "PK_logged_meals" PRIMARY KEY (id);


--
-- Name: nutrition_diary_command_events PK_nutrition_diary_command_events; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.nutrition_diary_command_events
    ADD CONSTRAINT "PK_nutrition_diary_command_events" PRIMARY KEY (id);


--
-- Name: nutrition_diary_event_cursors PK_nutrition_diary_event_cursors; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.nutrition_diary_event_cursors
    ADD CONSTRAINT "PK_nutrition_diary_event_cursors" PRIMARY KEY (user_id);


--
-- Name: photo_journey_entries PK_photo_journey_entries; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_journey_entries
    ADD CONSTRAINT "PK_photo_journey_entries" PRIMARY KEY (id);


--
-- Name: photo_object_cleanups PK_photo_object_cleanups; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_object_cleanups
    ADD CONSTRAINT "PK_photo_object_cleanups" PRIMARY KEY (id);


--
-- Name: photo_upload_intents PK_photo_upload_intents; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_upload_intents
    ADD CONSTRAINT "PK_photo_upload_intents" PRIMARY KEY (id);


--
-- Name: recipes PK_recipes; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.recipes
    ADD CONSTRAINT "PK_recipes" PRIMARY KEY (id);


--
-- Name: saved_meal_templates PK_saved_meal_templates; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.saved_meal_templates
    ADD CONSTRAINT "PK_saved_meal_templates" PRIMARY KEY (id);


--
-- Name: symptom_logs PK_symptom_logs; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.symptom_logs
    ADD CONSTRAINT "PK_symptom_logs" PRIMARY KEY (id);


--
-- Name: user_exercise_preferences PK_user_exercise_preferences; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_exercise_preferences
    ADD CONSTRAINT "PK_user_exercise_preferences" PRIMARY KEY (id);


--
-- Name: user_food_items PK_user_food_items; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_food_items
    ADD CONSTRAINT "PK_user_food_items" PRIMARY KEY (id);


--
-- Name: user_meal_template_entries PK_user_meal_template_entries; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_meal_template_entries
    ADD CONSTRAINT "PK_user_meal_template_entries" PRIMARY KEY (id);


--
-- Name: user_meal_templates PK_user_meal_templates; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_meal_templates
    ADD CONSTRAINT "PK_user_meal_templates" PRIMARY KEY (id);


--
-- Name: user_movement_notes PK_user_movement_notes; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_movement_notes
    ADD CONSTRAINT "PK_user_movement_notes" PRIMARY KEY (id);


--
-- Name: user_profile_snapshots PK_user_profile_snapshots; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_profile_snapshots
    ADD CONSTRAINT "PK_user_profile_snapshots" PRIMARY KEY (id);


--
-- Name: user_profiles PK_user_profiles; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_profiles
    ADD CONSTRAINT "PK_user_profiles" PRIMARY KEY (id);


--
-- Name: user_quiz_responses PK_user_quiz_responses; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_quiz_responses
    ADD CONSTRAINT "PK_user_quiz_responses" PRIMARY KEY (id);


--
-- Name: users PK_users; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.users
    ADD CONSTRAINT "PK_users" PRIMARY KEY (id);


--
-- Name: workout_day_exercises PK_workout_day_exercises; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_day_exercises
    ADD CONSTRAINT "PK_workout_day_exercises" PRIMARY KEY (id);


--
-- Name: workout_days PK_workout_days; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_days
    ADD CONSTRAINT "PK_workout_days" PRIMARY KEY (id);


--
-- Name: workout_exercise_logs PK_workout_exercise_logs; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_exercise_logs
    ADD CONSTRAINT "PK_workout_exercise_logs" PRIMARY KEY (id);


--
-- Name: workout_injury_logs PK_workout_injury_logs; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_injury_logs
    ADD CONSTRAINT "PK_workout_injury_logs" PRIMARY KEY (id);


--
-- Name: workout_logs PK_workout_logs; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_logs
    ADD CONSTRAINT "PK_workout_logs" PRIMARY KEY (id);


--
-- Name: workout_plans PK_workout_plans; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_plans
    ADD CONSTRAINT "PK_workout_plans" PRIMARY KEY (id);


--
-- Name: workout_session_logs PK_workout_session_logs; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_session_logs
    ADD CONSTRAINT "PK_workout_session_logs" PRIMARY KEY (id);


--
-- Name: apple_sign_in_nonces apple_sign_in_nonces_nonce_hash_key; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.apple_sign_in_nonces
    ADD CONSTRAINT apple_sign_in_nonces_nonce_hash_key UNIQUE (nonce_hash);


--
-- Name: apple_sign_in_nonces apple_sign_in_nonces_pkey; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.apple_sign_in_nonces
    ADD CONSTRAINT apple_sign_in_nonces_pkey PRIMARY KEY (id);


--
-- Name: email_challenge_sessions email_challenge_sessions_pkey; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.email_challenge_sessions
    ADD CONSTRAINT email_challenge_sessions_pkey PRIMARY KEY (id);


--
-- Name: password_login_attempts password_login_attempts_pkey; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.password_login_attempts
    ADD CONSTRAINT password_login_attempts_pkey PRIMARY KEY (id);


--
-- Name: photo_cleanup_dispatch_state photo_cleanup_dispatch_state_pkey; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_cleanup_dispatch_state
    ADD CONSTRAINT photo_cleanup_dispatch_state_pkey PRIMARY KEY (state_id);


--
-- Name: photo_object_reconcile_targets photo_object_reconcile_targets_pkey; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_object_reconcile_targets
    ADD CONSTRAINT photo_object_reconcile_targets_pkey PRIMARY KEY (id);


--
-- Name: photo_object_reconcile_targets photo_object_reconcile_targets_target_key_key; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_object_reconcile_targets
    ADD CONSTRAINT photo_object_reconcile_targets_target_key_key UNIQUE (target_key);


--
-- Name: refresh_sessions refresh_sessions_pkey; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.refresh_sessions
    ADD CONSTRAINT refresh_sessions_pkey PRIMARY KEY (id);


--
-- Name: revoked_access_tokens revoked_access_tokens_pkey; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.revoked_access_tokens
    ADD CONSTRAINT revoked_access_tokens_pkey PRIMARY KEY (id);


--
-- Name: revoked_access_tokens revoked_access_tokens_token_id_key; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.revoked_access_tokens
    ADD CONSTRAINT revoked_access_tokens_token_id_key UNIQUE (token_id);


--
-- Name: source_account_claim_nonces source_account_claim_nonces_nonce_hash_key; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.source_account_claim_nonces
    ADD CONSTRAINT source_account_claim_nonces_nonce_hash_key UNIQUE (nonce_hash);


--
-- Name: source_account_claim_nonces source_account_claim_nonces_pkey; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.source_account_claim_nonces
    ADD CONSTRAINT source_account_claim_nonces_pkey PRIMARY KEY (id);


--
-- Name: source_account_mappings source_account_mappings_pkey; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.source_account_mappings
    ADD CONSTRAINT source_account_mappings_pkey PRIMARY KEY (id);


--
-- Name: command_idempotency uq_command_idempotency_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.command_idempotency
    ADD CONSTRAINT uq_command_idempotency_user_id_id UNIQUE (user_id, id);


--
-- Name: cycle_logs uq_cycle_logs_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.cycle_logs
    ADD CONSTRAINT uq_cycle_logs_user_id_id UNIQUE (user_id, id);


--
-- Name: cycle_phase_logs uq_cycle_phase_logs_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.cycle_phase_logs
    ADD CONSTRAINT uq_cycle_phase_logs_user_id_id UNIQUE (user_id, id);


--
-- Name: daily_nutrition_logs uq_daily_nutrition_logs_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.daily_nutrition_logs
    ADD CONSTRAINT uq_daily_nutrition_logs_user_id_id UNIQUE (user_id, id);


--
-- Name: daily_readiness_logs uq_daily_readiness_logs_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.daily_readiness_logs
    ADD CONSTRAINT uq_daily_readiness_logs_user_id_id UNIQUE (user_id, id);


--
-- Name: exercise_set_logs uq_exercise_set_logs_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.exercise_set_logs
    ADD CONSTRAINT uq_exercise_set_logs_user_id_id UNIQUE (user_id, id);


--
-- Name: food_item_entries uq_food_item_entries_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.food_item_entries
    ADD CONSTRAINT uq_food_item_entries_user_id_id UNIQUE (user_id, id);


--
-- Name: logged_meal_entries uq_logged_meal_entries_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.logged_meal_entries
    ADD CONSTRAINT uq_logged_meal_entries_user_id_id UNIQUE (user_id, id);


--
-- Name: logged_meals uq_logged_meals_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.logged_meals
    ADD CONSTRAINT uq_logged_meals_user_id_id UNIQUE (user_id, id);


--
-- Name: nutrition_diary_command_events uq_nutrition_diary_command_events_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.nutrition_diary_command_events
    ADD CONSTRAINT uq_nutrition_diary_command_events_user_id_id UNIQUE (user_id, id);


--
-- Name: photo_journey_entries uq_photo_journey_entries_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_journey_entries
    ADD CONSTRAINT uq_photo_journey_entries_user_id_id UNIQUE (user_id, id);


--
-- Name: photo_object_cleanups uq_photo_object_cleanups_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_object_cleanups
    ADD CONSTRAINT uq_photo_object_cleanups_user_id_id UNIQUE (user_id, id);


--
-- Name: photo_upload_intents uq_photo_upload_intents_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_upload_intents
    ADD CONSTRAINT uq_photo_upload_intents_user_id_id UNIQUE (user_id, id);


--
-- Name: recipes uq_recipes_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.recipes
    ADD CONSTRAINT uq_recipes_user_id_id UNIQUE (user_id, id);


--
-- Name: saved_meal_templates uq_saved_meal_templates_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.saved_meal_templates
    ADD CONSTRAINT uq_saved_meal_templates_user_id_id UNIQUE (user_id, id);


--
-- Name: symptom_logs uq_symptom_logs_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.symptom_logs
    ADD CONSTRAINT uq_symptom_logs_user_id_id UNIQUE (user_id, id);


--
-- Name: user_exercise_preferences uq_user_exercise_preferences_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_exercise_preferences
    ADD CONSTRAINT uq_user_exercise_preferences_user_id_id UNIQUE (user_id, id);


--
-- Name: user_food_items uq_user_food_items_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_food_items
    ADD CONSTRAINT uq_user_food_items_user_id_id UNIQUE (user_id, id);


--
-- Name: user_meal_template_entries uq_user_meal_template_entries_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_meal_template_entries
    ADD CONSTRAINT uq_user_meal_template_entries_user_id_id UNIQUE (user_id, id);


--
-- Name: user_meal_templates uq_user_meal_templates_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_meal_templates
    ADD CONSTRAINT uq_user_meal_templates_user_id_id UNIQUE (user_id, id);


--
-- Name: user_movement_notes uq_user_movement_notes_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_movement_notes
    ADD CONSTRAINT uq_user_movement_notes_user_id_id UNIQUE (user_id, id);


--
-- Name: user_profile_snapshots uq_user_profile_snapshots_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_profile_snapshots
    ADD CONSTRAINT uq_user_profile_snapshots_user_id_id UNIQUE (user_id, id);


--
-- Name: user_profiles uq_user_profiles_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_profiles
    ADD CONSTRAINT uq_user_profiles_user_id_id UNIQUE (user_id, id);


--
-- Name: user_quiz_responses uq_user_quiz_responses_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_quiz_responses
    ADD CONSTRAINT uq_user_quiz_responses_user_id_id UNIQUE (user_id, id);


--
-- Name: workout_day_exercises uq_workout_day_exercises_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_day_exercises
    ADD CONSTRAINT uq_workout_day_exercises_user_id_id UNIQUE (user_id, id);


--
-- Name: workout_days uq_workout_days_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_days
    ADD CONSTRAINT uq_workout_days_user_id_id UNIQUE (user_id, id);


--
-- Name: workout_exercise_logs uq_workout_exercise_logs_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_exercise_logs
    ADD CONSTRAINT uq_workout_exercise_logs_user_id_id UNIQUE (user_id, id);


--
-- Name: workout_injury_logs uq_workout_injury_logs_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_injury_logs
    ADD CONSTRAINT uq_workout_injury_logs_user_id_id UNIQUE (user_id, id);


--
-- Name: workout_logs uq_workout_logs_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_logs
    ADD CONSTRAINT uq_workout_logs_user_id_id UNIQUE (user_id, id);


--
-- Name: workout_plans uq_workout_plans_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_plans
    ADD CONSTRAINT uq_workout_plans_user_id_id UNIQUE (user_id, id);


--
-- Name: workout_session_logs uq_workout_session_logs_user_id_id; Type: CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_session_logs
    ADD CONSTRAINT uq_workout_session_logs_user_id_id UNIQUE (user_id, id);


--
-- Name: exercise_muscle_contributions PK_exercise_muscle_contributions; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.exercise_muscle_contributions
    ADD CONSTRAINT "PK_exercise_muscle_contributions" PRIMARY KEY (id);


--
-- Name: exercises PK_exercises; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.exercises
    ADD CONSTRAINT "PK_exercises" PRIMARY KEY (id);


--
-- Name: food_item_versions PK_food_item_versions; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.food_item_versions
    ADD CONSTRAINT "PK_food_item_versions" PRIMARY KEY (id);


--
-- Name: food_items PK_food_items; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.food_items
    ADD CONSTRAINT "PK_food_items" PRIMARY KEY (id);


--
-- Name: goal_tier_weights PK_goal_tier_weights; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.goal_tier_weights
    ADD CONSTRAINT "PK_goal_tier_weights" PRIMARY KEY (id);


--
-- Name: meal_template_entries PK_meal_template_entries; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.meal_template_entries
    ADD CONSTRAINT "PK_meal_template_entries" PRIMARY KEY (id);


--
-- Name: meal_templates PK_meal_templates; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.meal_templates
    ADD CONSTRAINT "PK_meal_templates" PRIMARY KEY (id);


--
-- Name: quiz_answers PK_quiz_answers; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.quiz_answers
    ADD CONSTRAINT "PK_quiz_answers" PRIMARY KEY (id);


--
-- Name: quiz_questions PK_quiz_questions; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.quiz_questions
    ADD CONSTRAINT "PK_quiz_questions" PRIMARY KEY (id);


--
-- Name: session_archetypes PK_session_archetypes; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.session_archetypes
    ADD CONSTRAINT "PK_session_archetypes" PRIMARY KEY (id);


--
-- Name: slot_templates PK_slot_templates; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.slot_templates
    ADD CONSTRAINT "PK_slot_templates" PRIMARY KEY (id);


--
-- Name: volume_budget_rows PK_volume_budget_rows; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.volume_budget_rows
    ADD CONSTRAINT "PK_volume_budget_rows" PRIMARY KEY (id);


--
-- Name: week_templates PK_week_templates; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.week_templates
    ADD CONSTRAINT "PK_week_templates" PRIMARY KEY (id);


--
-- Name: workout_exercise_definitions PK_workout_exercise_definitions; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.workout_exercise_definitions
    ADD CONSTRAINT "PK_workout_exercise_definitions" PRIMARY KEY (id);


--
-- Name: workout_muscle_groups PK_workout_muscle_groups; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.workout_muscle_groups
    ADD CONSTRAINT "PK_workout_muscle_groups" PRIMARY KEY (id);


--
-- Name: workout_planning_config_entries PK_workout_planning_config_entries; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.workout_planning_config_entries
    ADD CONSTRAINT "PK_workout_planning_config_entries" PRIMARY KEY (id);


--
-- Name: quiz_answers uq_quiz_answers_id_question_id; Type: CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.quiz_answers
    ADD CONSTRAINT uq_quiz_answers_id_question_id UNIQUE (id, question_id);


--
-- Name: food_sync_logs PK_food_sync_logs; Type: CONSTRAINT; Schema: ops; Owner: -
--

ALTER TABLE ONLY ops.food_sync_logs
    ADD CONSTRAINT "PK_food_sync_logs" PRIMARY KEY (id);


--
-- Name: IX_cycle_phase_logs_user_id_cycle_log_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_cycle_phase_logs_user_id_cycle_log_id" ON app.cycle_phase_logs USING btree (user_id, cycle_log_id);


--
-- Name: IX_exercise_set_logs_user_id_workout_session_log_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_exercise_set_logs_user_id_workout_session_log_id" ON app.exercise_set_logs USING btree (user_id, workout_session_log_id);


--
-- Name: IX_food_item_entries_user_id_daily_log_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_food_item_entries_user_id_daily_log_id" ON app.food_item_entries USING btree (user_id, daily_log_id);


--
-- Name: IX_logged_meal_entries_catalog_food_item_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_logged_meal_entries_catalog_food_item_id" ON app.logged_meal_entries USING btree (catalog_food_item_id);


--
-- Name: IX_logged_meal_entries_user_id_logged_meal_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_logged_meal_entries_user_id_logged_meal_id" ON app.logged_meal_entries USING btree (user_id, logged_meal_id);


--
-- Name: IX_logged_meal_entries_user_id_user_food_item_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_logged_meal_entries_user_id_user_food_item_id" ON app.logged_meal_entries USING btree (user_id, user_food_item_id);


--
-- Name: IX_symptom_logs_user_id_cycle_log_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_symptom_logs_user_id_cycle_log_id" ON app.symptom_logs USING btree (user_id, cycle_log_id);


--
-- Name: IX_user_exercise_preferences_original_exercise_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_user_exercise_preferences_original_exercise_id" ON app.user_exercise_preferences USING btree (original_exercise_id);


--
-- Name: IX_user_exercise_preferences_preferred_exercise_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_user_exercise_preferences_preferred_exercise_id" ON app.user_exercise_preferences USING btree (preferred_exercise_id);


--
-- Name: IX_user_meal_template_entries_catalog_food_item_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_user_meal_template_entries_catalog_food_item_id" ON app.user_meal_template_entries USING btree (catalog_food_item_id);


--
-- Name: IX_user_meal_template_entries_user_id_meal_template_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_user_meal_template_entries_user_id_meal_template_id" ON app.user_meal_template_entries USING btree (user_id, meal_template_id);


--
-- Name: IX_user_meal_template_entries_user_id_user_food_item_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_user_meal_template_entries_user_id_user_food_item_id" ON app.user_meal_template_entries USING btree (user_id, user_food_item_id);


--
-- Name: IX_user_movement_notes_exercise_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_user_movement_notes_exercise_id" ON app.user_movement_notes USING btree (exercise_id);


--
-- Name: IX_user_quiz_responses_quiz_answer_id_quiz_question_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_user_quiz_responses_quiz_answer_id_quiz_question_id" ON app.user_quiz_responses USING btree (quiz_answer_id, quiz_question_id);


--
-- Name: IX_user_quiz_responses_user_id_user_profile_snapshot_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_user_quiz_responses_user_id_user_profile_snapshot_id" ON app.user_quiz_responses USING btree (user_id, user_profile_snapshot_id);


--
-- Name: IX_workout_day_exercises_exercise_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_workout_day_exercises_exercise_id" ON app.workout_day_exercises USING btree (exercise_id);


--
-- Name: IX_workout_day_exercises_user_id_workout_day_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_workout_day_exercises_user_id_workout_day_id" ON app.workout_day_exercises USING btree (user_id, workout_day_id);


--
-- Name: IX_workout_days_user_id_workout_plan_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_workout_days_user_id_workout_plan_id" ON app.workout_days USING btree (user_id, workout_plan_id);


--
-- Name: IX_workout_exercise_logs_exercise_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_workout_exercise_logs_exercise_id" ON app.workout_exercise_logs USING btree (exercise_id);


--
-- Name: IX_workout_exercise_logs_user_id_workout_day_exercise_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_workout_exercise_logs_user_id_workout_day_exercise_id" ON app.workout_exercise_logs USING btree (user_id, workout_day_exercise_id);


--
-- Name: IX_workout_exercise_logs_user_id_workout_log_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_workout_exercise_logs_user_id_workout_log_id" ON app.workout_exercise_logs USING btree (user_id, workout_log_id);


--
-- Name: IX_workout_logs_user_id_workout_day_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX "IX_workout_logs_user_id_workout_day_id" ON app.workout_logs USING btree (user_id, workout_day_id);


--
-- Name: ix_apple_sign_in_nonces_expiry; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_apple_sign_in_nonces_expiry ON app.apple_sign_in_nonces USING btree (expires_at_utc);


--
-- Name: ix_command_idempotency_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_command_idempotency_user_id ON app.command_idempotency USING btree (user_id);


--
-- Name: ix_cycle_logs_owner_start_date_desc; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_cycle_logs_owner_start_date_desc ON app.cycle_logs USING btree (user_id, start_date DESC);


--
-- Name: ix_cycle_logs_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_cycle_logs_user_id ON app.cycle_logs USING btree (user_id);


--
-- Name: ix_cycle_phase_logs_owner_logged_at_desc; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_cycle_phase_logs_owner_logged_at_desc ON app.cycle_phase_logs USING btree (user_id, logged_at DESC);


--
-- Name: ix_cycle_phase_logs_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_cycle_phase_logs_user_id ON app.cycle_phase_logs USING btree (user_id);


--
-- Name: ix_daily_nutrition_logs_owner_local_date_desc; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_daily_nutrition_logs_owner_local_date_desc ON app.daily_nutrition_logs USING btree (user_id, local_date DESC);


--
-- Name: ix_daily_nutrition_logs_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_daily_nutrition_logs_user_id ON app.daily_nutrition_logs USING btree (user_id);


--
-- Name: ix_daily_readiness_logs_owner_date_desc; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_daily_readiness_logs_owner_date_desc ON app.daily_readiness_logs USING btree (user_id, date DESC);


--
-- Name: ix_daily_readiness_logs_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_daily_readiness_logs_user_id ON app.daily_readiness_logs USING btree (user_id);


--
-- Name: ix_email_challenge_sessions_email_created; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_email_challenge_sessions_email_created ON app.email_challenge_sessions USING btree (normalized_email, created_at_utc DESC);


--
-- Name: ix_email_challenge_sessions_expiry; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_email_challenge_sessions_expiry ON app.email_challenge_sessions USING btree (expires_at_utc);


--
-- Name: ix_exercise_set_logs_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_exercise_set_logs_user_id ON app.exercise_set_logs USING btree (user_id);


--
-- Name: ix_food_item_entries_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_food_item_entries_user_id ON app.food_item_entries USING btree (user_id);


--
-- Name: ix_logged_meal_entries_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_logged_meal_entries_user_id ON app.logged_meal_entries USING btree (user_id);


--
-- Name: ix_logged_meals_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_logged_meals_user_id ON app.logged_meals USING btree (user_id);


--
-- Name: ix_nutrition_diary_command_events_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_nutrition_diary_command_events_user_id ON app.nutrition_diary_command_events USING btree (user_id);


--
-- Name: ix_password_login_attempts_cleanup; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_password_login_attempts_cleanup ON app.password_login_attempts USING btree (attempted_at_utc);


--
-- Name: ix_password_login_attempts_scope_time; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_password_login_attempts_scope_time ON app.password_login_attempts USING btree (scope_key, attempted_at_utc DESC);


--
-- Name: ix_photo_journey_entries_owner_date_desc; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_photo_journey_entries_owner_date_desc ON app.photo_journey_entries USING btree (user_id, date DESC);


--
-- Name: ix_photo_journey_entries_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_photo_journey_entries_user_id ON app.photo_journey_entries USING btree (user_id);


--
-- Name: ix_photo_object_cleanups_due; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_photo_object_cleanups_due ON app.photo_object_cleanups USING btree (not_before_utc, created_at_utc);


--
-- Name: ix_photo_object_cleanups_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_photo_object_cleanups_user_id ON app.photo_object_cleanups USING btree (user_id);


--
-- Name: ix_photo_object_reconcile_targets_due; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_photo_object_reconcile_targets_due ON app.photo_object_reconcile_targets USING btree (not_before_utc, id);


--
-- Name: ix_photo_upload_intents_owner_expiry; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_photo_upload_intents_owner_expiry ON app.photo_upload_intents USING btree (user_id, expires_at_utc);


--
-- Name: ix_photo_upload_intents_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_photo_upload_intents_user_id ON app.photo_upload_intents USING btree (user_id);


--
-- Name: ix_recipes_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_recipes_user_id ON app.recipes USING btree (user_id);


--
-- Name: ix_refresh_sessions_family_revoked; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_refresh_sessions_family_revoked ON app.refresh_sessions USING btree (family_id, revoked_at_utc);


--
-- Name: ix_revoked_access_tokens_expiry; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_revoked_access_tokens_expiry ON app.revoked_access_tokens USING btree (expires_at_utc);


--
-- Name: ix_saved_meal_templates_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_saved_meal_templates_user_id ON app.saved_meal_templates USING btree (user_id);


--
-- Name: ix_source_account_claim_nonces_expiry; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_source_account_claim_nonces_expiry ON app.source_account_claim_nonces USING btree (expires_at_utc);


--
-- Name: ix_source_account_mappings_destination_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_source_account_mappings_destination_user_id ON app.source_account_mappings USING btree (destination_user_id);


--
-- Name: ix_symptom_logs_owner_date_desc; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_symptom_logs_owner_date_desc ON app.symptom_logs USING btree (user_id, date DESC);


--
-- Name: ix_symptom_logs_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_symptom_logs_user_id ON app.symptom_logs USING btree (user_id);


--
-- Name: ix_user_exercise_preferences_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_user_exercise_preferences_user_id ON app.user_exercise_preferences USING btree (user_id);


--
-- Name: ix_user_food_items_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_user_food_items_user_id ON app.user_food_items USING btree (user_id);


--
-- Name: ix_user_meal_template_entries_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_user_meal_template_entries_user_id ON app.user_meal_template_entries USING btree (user_id);


--
-- Name: ix_user_meal_templates_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_user_meal_templates_user_id ON app.user_meal_templates USING btree (user_id);


--
-- Name: ix_user_movement_notes_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_user_movement_notes_user_id ON app.user_movement_notes USING btree (user_id);


--
-- Name: ix_user_profile_snapshots_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_user_profile_snapshots_user_id ON app.user_profile_snapshots USING btree (user_id);


--
-- Name: ix_user_quiz_responses_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_user_quiz_responses_user_id ON app.user_quiz_responses USING btree (user_id);


--
-- Name: ix_workout_day_exercises_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_workout_day_exercises_user_id ON app.workout_day_exercises USING btree (user_id);


--
-- Name: ix_workout_days_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_workout_days_user_id ON app.workout_days USING btree (user_id);


--
-- Name: ix_workout_exercise_logs_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_workout_exercise_logs_user_id ON app.workout_exercise_logs USING btree (user_id);


--
-- Name: ix_workout_injury_logs_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_workout_injury_logs_user_id ON app.workout_injury_logs USING btree (user_id);


--
-- Name: ix_workout_logs_owner_date_desc; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_workout_logs_owner_date_desc ON app.workout_logs USING btree (user_id, date DESC);


--
-- Name: ix_workout_logs_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_workout_logs_user_id ON app.workout_logs USING btree (user_id);


--
-- Name: ix_workout_plans_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_workout_plans_user_id ON app.workout_plans USING btree (user_id);


--
-- Name: ix_workout_session_logs_owner_started_at_desc; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_workout_session_logs_owner_started_at_desc ON app.workout_session_logs USING btree (user_id, started_at DESC);


--
-- Name: ix_workout_session_logs_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE INDEX ix_workout_session_logs_user_id ON app.workout_session_logs USING btree (user_id);


--
-- Name: uq_command_idempotency_user_key; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_command_idempotency_user_key ON app.command_idempotency USING btree (user_id, command_key);


--
-- Name: uq_cycle_phase_logs_owner_local_date; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_cycle_phase_logs_owner_local_date ON app.cycle_phase_logs USING btree (user_id, local_date);


--
-- Name: uq_email_challenge_sessions_receipt_id; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_email_challenge_sessions_receipt_id ON app.email_challenge_sessions USING btree (receipt_id) WHERE (receipt_id IS NOT NULL);


--
-- Name: uq_nutrition_diary_command_events_owner_cursor; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_nutrition_diary_command_events_owner_cursor ON app.nutrition_diary_command_events USING btree (user_id, cursor);


--
-- Name: uq_photo_object_cleanups_owner_photo_reason; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_photo_object_cleanups_owner_photo_reason ON app.photo_object_cleanups USING btree (user_id, photo_id, reason);


--
-- Name: uq_photo_upload_intents_owner_idempotency; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_photo_upload_intents_owner_idempotency ON app.photo_upload_intents USING btree (user_id, idempotency_key);


--
-- Name: uq_photo_upload_intents_s3_key; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_photo_upload_intents_s3_key ON app.photo_upload_intents USING btree (s3_key);


--
-- Name: uq_refresh_sessions_token_hash; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_refresh_sessions_token_hash ON app.refresh_sessions USING btree (token_hash);


--
-- Name: uq_source_account_mappings_claim_id; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_source_account_mappings_claim_id ON app.source_account_mappings USING btree (claim_id);


--
-- Name: uq_source_account_mappings_source; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_source_account_mappings_source ON app.source_account_mappings USING btree (source_installation_key_hash, source_account_key_hash);


--
-- Name: uq_user_exercise_preferences_owner_original; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_user_exercise_preferences_owner_original ON app.user_exercise_preferences USING btree (user_id, original_exercise_id);


--
-- Name: uq_user_profiles_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_user_profiles_user_id ON app.user_profiles USING btree (user_id);


--
-- Name: uq_users_apple_user_id; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_users_apple_user_id ON app.users USING btree (apple_user_id) WHERE (apple_user_id IS NOT NULL);


--
-- Name: uq_users_normalized_email; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_users_normalized_email ON app.users USING btree (normalized_email);


--
-- Name: uq_users_normalized_username; Type: INDEX; Schema: app; Owner: -
--

CREATE UNIQUE INDEX uq_users_normalized_username ON app.users USING btree (lower(btrim(username))) WHERE ((username IS NOT NULL) AND (btrim(username) <> ''::text));


--
-- Name: IX_exercise_muscle_contributions_exercise_id_muscle_group_id; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_exercise_muscle_contributions_exercise_id_muscle_group_id" ON catalog.exercise_muscle_contributions USING btree (exercise_id, muscle_group_id);


--
-- Name: IX_exercise_muscle_contributions_muscle_group_id; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_exercise_muscle_contributions_muscle_group_id" ON catalog.exercise_muscle_contributions USING btree (muscle_group_id);


--
-- Name: IX_exercise_muscle_contributions_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_exercise_muscle_contributions_seed_code" ON catalog.exercise_muscle_contributions USING btree (seed_code);


--
-- Name: IX_exercises_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_exercises_code" ON catalog.exercises USING btree (code);


--
-- Name: IX_exercises_name; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_exercises_name" ON catalog.exercises USING btree (name);


--
-- Name: IX_exercises_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_exercises_seed_code" ON catalog.exercises USING btree (seed_code);


--
-- Name: IX_food_item_versions_food_item_id_versioned_at; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_food_item_versions_food_item_id_versioned_at" ON catalog.food_item_versions USING btree (food_item_id, versioned_at);


--
-- Name: IX_food_items_brand_name; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_food_items_brand_name" ON catalog.food_items USING btree (brand_name);


--
-- Name: IX_food_items_brand_owner; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_food_items_brand_owner" ON catalog.food_items USING btree (brand_owner);


--
-- Name: IX_food_items_data_type_name; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_food_items_data_type_name" ON catalog.food_items USING btree (data_type, name);


--
-- Name: IX_food_items_fdc_id; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_food_items_fdc_id" ON catalog.food_items USING btree (fdc_id);


--
-- Name: IX_food_items_gtin_upc; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_food_items_gtin_upc" ON catalog.food_items USING btree (gtin_upc);


--
-- Name: IX_food_items_name; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_food_items_name" ON catalog.food_items USING btree (name);


--
-- Name: IX_food_items_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_food_items_seed_code" ON catalog.food_items USING btree (seed_code);


--
-- Name: IX_goal_tier_weights_goal_muscle_group_id; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_goal_tier_weights_goal_muscle_group_id" ON catalog.goal_tier_weights USING btree (goal, muscle_group_id);


--
-- Name: IX_goal_tier_weights_muscle_group_id; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_goal_tier_weights_muscle_group_id" ON catalog.goal_tier_weights USING btree (muscle_group_id);


--
-- Name: IX_goal_tier_weights_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_goal_tier_weights_seed_code" ON catalog.goal_tier_weights USING btree (seed_code);


--
-- Name: IX_meal_template_entries_food_item_id; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_meal_template_entries_food_item_id" ON catalog.meal_template_entries USING btree (food_item_id);


--
-- Name: IX_meal_template_entries_meal_template_id; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_meal_template_entries_meal_template_id" ON catalog.meal_template_entries USING btree (meal_template_id);


--
-- Name: IX_meal_template_entries_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_meal_template_entries_seed_code" ON catalog.meal_template_entries USING btree (seed_code);


--
-- Name: IX_meal_templates_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_meal_templates_seed_code" ON catalog.meal_templates USING btree (seed_code);


--
-- Name: IX_quiz_answers_question_id_order_index; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_quiz_answers_question_id_order_index" ON catalog.quiz_answers USING btree (question_id, order_index);


--
-- Name: IX_quiz_answers_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_quiz_answers_seed_code" ON catalog.quiz_answers USING btree (seed_code);


--
-- Name: IX_quiz_questions_order_index; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_quiz_questions_order_index" ON catalog.quiz_questions USING btree (order_index);


--
-- Name: IX_quiz_questions_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_quiz_questions_seed_code" ON catalog.quiz_questions USING btree (seed_code);


--
-- Name: IX_session_archetypes_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_session_archetypes_code" ON catalog.session_archetypes USING btree (code);


--
-- Name: IX_session_archetypes_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_session_archetypes_seed_code" ON catalog.session_archetypes USING btree (seed_code);


--
-- Name: IX_slot_templates_archetype_id_order; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_slot_templates_archetype_id_order" ON catalog.slot_templates USING btree (archetype_id, "order");


--
-- Name: IX_slot_templates_primary_muscle_id; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_slot_templates_primary_muscle_id" ON catalog.slot_templates USING btree (primary_muscle_id);


--
-- Name: IX_slot_templates_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_slot_templates_seed_code" ON catalog.slot_templates USING btree (seed_code);


--
-- Name: IX_volume_budget_rows_days_per_week_muscle_group_id; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_volume_budget_rows_days_per_week_muscle_group_id" ON catalog.volume_budget_rows USING btree (days_per_week, muscle_group_id);


--
-- Name: IX_volume_budget_rows_muscle_group_id; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_volume_budget_rows_muscle_group_id" ON catalog.volume_budget_rows USING btree (muscle_group_id);


--
-- Name: IX_volume_budget_rows_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_volume_budget_rows_seed_code" ON catalog.volume_budget_rows USING btree (seed_code);


--
-- Name: IX_week_templates_days_per_week; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_week_templates_days_per_week" ON catalog.week_templates USING btree (days_per_week);


--
-- Name: IX_week_templates_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_week_templates_seed_code" ON catalog.week_templates USING btree (seed_code);


--
-- Name: IX_workout_exercise_definitions_name; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_workout_exercise_definitions_name" ON catalog.workout_exercise_definitions USING btree (name);


--
-- Name: IX_workout_exercise_definitions_pattern; Type: INDEX; Schema: catalog; Owner: -
--

CREATE INDEX "IX_workout_exercise_definitions_pattern" ON catalog.workout_exercise_definitions USING btree (pattern);


--
-- Name: IX_workout_exercise_definitions_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_workout_exercise_definitions_seed_code" ON catalog.workout_exercise_definitions USING btree (seed_code);


--
-- Name: IX_workout_muscle_groups_name; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_workout_muscle_groups_name" ON catalog.workout_muscle_groups USING btree (name);


--
-- Name: IX_workout_muscle_groups_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_workout_muscle_groups_seed_code" ON catalog.workout_muscle_groups USING btree (seed_code);


--
-- Name: IX_workout_planning_config_entries_section_key; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_workout_planning_config_entries_section_key" ON catalog.workout_planning_config_entries USING btree (section, key);


--
-- Name: IX_workout_planning_config_entries_seed_code; Type: INDEX; Schema: catalog; Owner: -
--

CREATE UNIQUE INDEX "IX_workout_planning_config_entries_seed_code" ON catalog.workout_planning_config_entries USING btree (seed_code);


--
-- Name: IX_food_sync_logs_started_at; Type: INDEX; Schema: ops; Owner: -
--

CREATE INDEX "IX_food_sync_logs_started_at" ON ops.food_sync_logs USING btree (started_at);


--
-- Name: command_idempotency FK_command_idempotency_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.command_idempotency
    ADD CONSTRAINT "FK_command_idempotency_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: cycle_logs FK_cycle_logs_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.cycle_logs
    ADD CONSTRAINT "FK_cycle_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: cycle_phase_logs FK_cycle_phase_logs_cycle_logs_user_id_cycle_log_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.cycle_phase_logs
    ADD CONSTRAINT "FK_cycle_phase_logs_cycle_logs_user_id_cycle_log_id" FOREIGN KEY (user_id, cycle_log_id) REFERENCES app.cycle_logs(user_id, id) ON DELETE RESTRICT;


--
-- Name: cycle_phase_logs FK_cycle_phase_logs_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.cycle_phase_logs
    ADD CONSTRAINT "FK_cycle_phase_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: daily_nutrition_logs FK_daily_nutrition_logs_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.daily_nutrition_logs
    ADD CONSTRAINT "FK_daily_nutrition_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: daily_readiness_logs FK_daily_readiness_logs_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.daily_readiness_logs
    ADD CONSTRAINT "FK_daily_readiness_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: exercise_set_logs FK_exercise_set_logs_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.exercise_set_logs
    ADD CONSTRAINT "FK_exercise_set_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: exercise_set_logs FK_exercise_set_logs_workout_session_logs_user_id_workout_sess~; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.exercise_set_logs
    ADD CONSTRAINT "FK_exercise_set_logs_workout_session_logs_user_id_workout_sess~" FOREIGN KEY (user_id, workout_session_log_id) REFERENCES app.workout_session_logs(user_id, id) ON DELETE CASCADE;


--
-- Name: food_item_entries FK_food_item_entries_daily_nutrition_logs_user_id_daily_log_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.food_item_entries
    ADD CONSTRAINT "FK_food_item_entries_daily_nutrition_logs_user_id_daily_log_id" FOREIGN KEY (user_id, daily_log_id) REFERENCES app.daily_nutrition_logs(user_id, id) ON DELETE CASCADE;


--
-- Name: food_item_entries FK_food_item_entries_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.food_item_entries
    ADD CONSTRAINT "FK_food_item_entries_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: logged_meal_entries FK_logged_meal_entries_food_items_catalog_food_item_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.logged_meal_entries
    ADD CONSTRAINT "FK_logged_meal_entries_food_items_catalog_food_item_id" FOREIGN KEY (catalog_food_item_id) REFERENCES catalog.food_items(id) ON DELETE RESTRICT;


--
-- Name: logged_meal_entries FK_logged_meal_entries_logged_meals_user_id_logged_meal_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.logged_meal_entries
    ADD CONSTRAINT "FK_logged_meal_entries_logged_meals_user_id_logged_meal_id" FOREIGN KEY (user_id, logged_meal_id) REFERENCES app.logged_meals(user_id, id) ON DELETE CASCADE;


--
-- Name: logged_meal_entries FK_logged_meal_entries_user_food_items_user_id_user_food_item_~; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.logged_meal_entries
    ADD CONSTRAINT "FK_logged_meal_entries_user_food_items_user_id_user_food_item_~" FOREIGN KEY (user_id, user_food_item_id) REFERENCES app.user_food_items(user_id, id) ON DELETE RESTRICT;


--
-- Name: logged_meal_entries FK_logged_meal_entries_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.logged_meal_entries
    ADD CONSTRAINT "FK_logged_meal_entries_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: logged_meals FK_logged_meals_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.logged_meals
    ADD CONSTRAINT "FK_logged_meals_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: nutrition_diary_command_events FK_nutrition_diary_command_events_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.nutrition_diary_command_events
    ADD CONSTRAINT "FK_nutrition_diary_command_events_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: nutrition_diary_event_cursors FK_nutrition_diary_event_cursors_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.nutrition_diary_event_cursors
    ADD CONSTRAINT "FK_nutrition_diary_event_cursors_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: photo_journey_entries FK_photo_journey_entries_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_journey_entries
    ADD CONSTRAINT "FK_photo_journey_entries_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: photo_object_cleanups FK_photo_object_cleanups_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_object_cleanups
    ADD CONSTRAINT "FK_photo_object_cleanups_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: photo_upload_intents FK_photo_upload_intents_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.photo_upload_intents
    ADD CONSTRAINT "FK_photo_upload_intents_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: recipes FK_recipes_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.recipes
    ADD CONSTRAINT "FK_recipes_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: saved_meal_templates FK_saved_meal_templates_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.saved_meal_templates
    ADD CONSTRAINT "FK_saved_meal_templates_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: symptom_logs FK_symptom_logs_cycle_logs_user_id_cycle_log_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.symptom_logs
    ADD CONSTRAINT "FK_symptom_logs_cycle_logs_user_id_cycle_log_id" FOREIGN KEY (user_id, cycle_log_id) REFERENCES app.cycle_logs(user_id, id) ON DELETE RESTRICT;


--
-- Name: symptom_logs FK_symptom_logs_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.symptom_logs
    ADD CONSTRAINT "FK_symptom_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: user_exercise_preferences FK_user_exercise_preferences_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_exercise_preferences
    ADD CONSTRAINT "FK_user_exercise_preferences_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: user_exercise_preferences FK_user_exercise_preferences_workout_exercise_definitions_orig~; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_exercise_preferences
    ADD CONSTRAINT "FK_user_exercise_preferences_workout_exercise_definitions_orig~" FOREIGN KEY (original_exercise_id) REFERENCES catalog.workout_exercise_definitions(id) ON DELETE RESTRICT;


--
-- Name: user_exercise_preferences FK_user_exercise_preferences_workout_exercise_definitions_pref~; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_exercise_preferences
    ADD CONSTRAINT "FK_user_exercise_preferences_workout_exercise_definitions_pref~" FOREIGN KEY (preferred_exercise_id) REFERENCES catalog.workout_exercise_definitions(id) ON DELETE RESTRICT;


--
-- Name: user_food_items FK_user_food_items_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_food_items
    ADD CONSTRAINT "FK_user_food_items_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: user_meal_template_entries FK_user_meal_template_entries_food_items_catalog_food_item_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_meal_template_entries
    ADD CONSTRAINT "FK_user_meal_template_entries_food_items_catalog_food_item_id" FOREIGN KEY (catalog_food_item_id) REFERENCES catalog.food_items(id) ON DELETE RESTRICT;


--
-- Name: user_meal_template_entries FK_user_meal_template_entries_user_food_items_user_id_user_foo~; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_meal_template_entries
    ADD CONSTRAINT "FK_user_meal_template_entries_user_food_items_user_id_user_foo~" FOREIGN KEY (user_id, user_food_item_id) REFERENCES app.user_food_items(user_id, id) ON DELETE RESTRICT;


--
-- Name: user_meal_template_entries FK_user_meal_template_entries_user_meal_templates_user_id_meal~; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_meal_template_entries
    ADD CONSTRAINT "FK_user_meal_template_entries_user_meal_templates_user_id_meal~" FOREIGN KEY (user_id, meal_template_id) REFERENCES app.user_meal_templates(user_id, id) ON DELETE CASCADE;


--
-- Name: user_meal_template_entries FK_user_meal_template_entries_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_meal_template_entries
    ADD CONSTRAINT "FK_user_meal_template_entries_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: user_meal_templates FK_user_meal_templates_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_meal_templates
    ADD CONSTRAINT "FK_user_meal_templates_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: user_movement_notes FK_user_movement_notes_exercises_exercise_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_movement_notes
    ADD CONSTRAINT "FK_user_movement_notes_exercises_exercise_id" FOREIGN KEY (exercise_id) REFERENCES catalog.exercises(id) ON DELETE RESTRICT;


--
-- Name: user_movement_notes FK_user_movement_notes_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_movement_notes
    ADD CONSTRAINT "FK_user_movement_notes_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: user_profile_snapshots FK_user_profile_snapshots_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_profile_snapshots
    ADD CONSTRAINT "FK_user_profile_snapshots_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: user_profiles FK_user_profiles_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_profiles
    ADD CONSTRAINT "FK_user_profiles_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: user_quiz_responses FK_user_quiz_responses_quiz_answers_quiz_answer_id_quiz_questi~; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_quiz_responses
    ADD CONSTRAINT "FK_user_quiz_responses_quiz_answers_quiz_answer_id_quiz_questi~" FOREIGN KEY (quiz_answer_id, quiz_question_id) REFERENCES catalog.quiz_answers(id, question_id) ON DELETE RESTRICT;


--
-- Name: user_quiz_responses FK_user_quiz_responses_user_profile_snapshots_user_id_user_pro~; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_quiz_responses
    ADD CONSTRAINT "FK_user_quiz_responses_user_profile_snapshots_user_id_user_pro~" FOREIGN KEY (user_id, user_profile_snapshot_id) REFERENCES app.user_profile_snapshots(user_id, id) ON DELETE RESTRICT;


--
-- Name: user_quiz_responses FK_user_quiz_responses_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.user_quiz_responses
    ADD CONSTRAINT "FK_user_quiz_responses_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: workout_day_exercises FK_workout_day_exercises_exercises_exercise_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_day_exercises
    ADD CONSTRAINT "FK_workout_day_exercises_exercises_exercise_id" FOREIGN KEY (exercise_id) REFERENCES catalog.exercises(id) ON DELETE RESTRICT;


--
-- Name: workout_day_exercises FK_workout_day_exercises_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_day_exercises
    ADD CONSTRAINT "FK_workout_day_exercises_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: workout_day_exercises FK_workout_day_exercises_workout_days_user_id_workout_day_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_day_exercises
    ADD CONSTRAINT "FK_workout_day_exercises_workout_days_user_id_workout_day_id" FOREIGN KEY (user_id, workout_day_id) REFERENCES app.workout_days(user_id, id) ON DELETE CASCADE;


--
-- Name: workout_days FK_workout_days_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_days
    ADD CONSTRAINT "FK_workout_days_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: workout_days FK_workout_days_workout_plans_user_id_workout_plan_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_days
    ADD CONSTRAINT "FK_workout_days_workout_plans_user_id_workout_plan_id" FOREIGN KEY (user_id, workout_plan_id) REFERENCES app.workout_plans(user_id, id) ON DELETE CASCADE;


--
-- Name: workout_exercise_logs FK_workout_exercise_logs_exercises_exercise_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_exercise_logs
    ADD CONSTRAINT "FK_workout_exercise_logs_exercises_exercise_id" FOREIGN KEY (exercise_id) REFERENCES catalog.exercises(id) ON DELETE RESTRICT;


--
-- Name: workout_exercise_logs FK_workout_exercise_logs_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_exercise_logs
    ADD CONSTRAINT "FK_workout_exercise_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: workout_exercise_logs FK_workout_exercise_logs_workout_day_exercises_user_id_workout~; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_exercise_logs
    ADD CONSTRAINT "FK_workout_exercise_logs_workout_day_exercises_user_id_workout~" FOREIGN KEY (user_id, workout_day_exercise_id) REFERENCES app.workout_day_exercises(user_id, id) ON DELETE RESTRICT;


--
-- Name: workout_exercise_logs FK_workout_exercise_logs_workout_logs_user_id_workout_log_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_exercise_logs
    ADD CONSTRAINT "FK_workout_exercise_logs_workout_logs_user_id_workout_log_id" FOREIGN KEY (user_id, workout_log_id) REFERENCES app.workout_logs(user_id, id) ON DELETE CASCADE;


--
-- Name: workout_injury_logs FK_workout_injury_logs_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_injury_logs
    ADD CONSTRAINT "FK_workout_injury_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: workout_logs FK_workout_logs_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_logs
    ADD CONSTRAINT "FK_workout_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: workout_logs FK_workout_logs_workout_days_user_id_workout_day_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_logs
    ADD CONSTRAINT "FK_workout_logs_workout_days_user_id_workout_day_id" FOREIGN KEY (user_id, workout_day_id) REFERENCES app.workout_days(user_id, id) ON DELETE RESTRICT;


--
-- Name: workout_plans FK_workout_plans_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_plans
    ADD CONSTRAINT "FK_workout_plans_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: workout_session_logs FK_workout_session_logs_users_user_id; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.workout_session_logs
    ADD CONSTRAINT "FK_workout_session_logs_users_user_id" FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: refresh_sessions refresh_sessions_user_id_fkey; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.refresh_sessions
    ADD CONSTRAINT refresh_sessions_user_id_fkey FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: revoked_access_tokens revoked_access_tokens_user_id_fkey; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.revoked_access_tokens
    ADD CONSTRAINT revoked_access_tokens_user_id_fkey FOREIGN KEY (user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: source_account_claim_nonces source_account_claim_nonces_destination_user_id_fkey; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.source_account_claim_nonces
    ADD CONSTRAINT source_account_claim_nonces_destination_user_id_fkey FOREIGN KEY (destination_user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: source_account_mappings source_account_mappings_claim_id_fkey; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.source_account_mappings
    ADD CONSTRAINT source_account_mappings_claim_id_fkey FOREIGN KEY (claim_id) REFERENCES app.source_account_claim_nonces(id) ON DELETE RESTRICT;


--
-- Name: source_account_mappings source_account_mappings_destination_user_id_fkey; Type: FK CONSTRAINT; Schema: app; Owner: -
--

ALTER TABLE ONLY app.source_account_mappings
    ADD CONSTRAINT source_account_mappings_destination_user_id_fkey FOREIGN KEY (destination_user_id) REFERENCES app.users(id) ON DELETE CASCADE;


--
-- Name: exercise_muscle_contributions FK_exercise_muscle_contributions_workout_exercise_definitions_~; Type: FK CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.exercise_muscle_contributions
    ADD CONSTRAINT "FK_exercise_muscle_contributions_workout_exercise_definitions_~" FOREIGN KEY (exercise_id) REFERENCES catalog.workout_exercise_definitions(id) ON DELETE RESTRICT;


--
-- Name: exercise_muscle_contributions FK_exercise_muscle_contributions_workout_muscle_groups_muscle_~; Type: FK CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.exercise_muscle_contributions
    ADD CONSTRAINT "FK_exercise_muscle_contributions_workout_muscle_groups_muscle_~" FOREIGN KEY (muscle_group_id) REFERENCES catalog.workout_muscle_groups(id) ON DELETE RESTRICT;


--
-- Name: food_item_versions FK_food_item_versions_food_items_food_item_id; Type: FK CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.food_item_versions
    ADD CONSTRAINT "FK_food_item_versions_food_items_food_item_id" FOREIGN KEY (food_item_id) REFERENCES catalog.food_items(id) ON DELETE RESTRICT;


--
-- Name: goal_tier_weights FK_goal_tier_weights_workout_muscle_groups_muscle_group_id; Type: FK CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.goal_tier_weights
    ADD CONSTRAINT "FK_goal_tier_weights_workout_muscle_groups_muscle_group_id" FOREIGN KEY (muscle_group_id) REFERENCES catalog.workout_muscle_groups(id) ON DELETE RESTRICT;


--
-- Name: meal_template_entries FK_meal_template_entries_food_items_food_item_id; Type: FK CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.meal_template_entries
    ADD CONSTRAINT "FK_meal_template_entries_food_items_food_item_id" FOREIGN KEY (food_item_id) REFERENCES catalog.food_items(id) ON DELETE RESTRICT;


--
-- Name: meal_template_entries FK_meal_template_entries_meal_templates_meal_template_id; Type: FK CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.meal_template_entries
    ADD CONSTRAINT "FK_meal_template_entries_meal_templates_meal_template_id" FOREIGN KEY (meal_template_id) REFERENCES catalog.meal_templates(id) ON DELETE RESTRICT;


--
-- Name: quiz_answers FK_quiz_answers_quiz_questions_question_id; Type: FK CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.quiz_answers
    ADD CONSTRAINT "FK_quiz_answers_quiz_questions_question_id" FOREIGN KEY (question_id) REFERENCES catalog.quiz_questions(id) ON DELETE RESTRICT;


--
-- Name: slot_templates FK_slot_templates_session_archetypes_archetype_id; Type: FK CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.slot_templates
    ADD CONSTRAINT "FK_slot_templates_session_archetypes_archetype_id" FOREIGN KEY (archetype_id) REFERENCES catalog.session_archetypes(id) ON DELETE RESTRICT;


--
-- Name: slot_templates FK_slot_templates_workout_muscle_groups_primary_muscle_id; Type: FK CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.slot_templates
    ADD CONSTRAINT "FK_slot_templates_workout_muscle_groups_primary_muscle_id" FOREIGN KEY (primary_muscle_id) REFERENCES catalog.workout_muscle_groups(id) ON DELETE RESTRICT;


--
-- Name: volume_budget_rows FK_volume_budget_rows_workout_muscle_groups_muscle_group_id; Type: FK CONSTRAINT; Schema: catalog; Owner: -
--

ALTER TABLE ONLY catalog.volume_budget_rows
    ADD CONSTRAINT "FK_volume_budget_rows_workout_muscle_groups_muscle_group_id" FOREIGN KEY (muscle_group_id) REFERENCES catalog.workout_muscle_groups(id) ON DELETE RESTRICT;


--
-- Name: users auth_user_lookup; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY auth_user_lookup ON app.users FOR SELECT USING ((CURRENT_USER = 'musclecuties_migrator'::name));


--
-- Name: photo_cleanup_dispatch_state cleanup_dispatch_cursor_access; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY cleanup_dispatch_cursor_access ON app.photo_cleanup_dispatch_state TO musclecuties_cleanup_dispatch USING (true) WITH CHECK (true);


--
-- Name: photo_object_cleanups cleanup_due_owner_discovery; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY cleanup_due_owner_discovery ON app.photo_object_cleanups FOR SELECT TO musclecuties_cleanup_discovery_owner USING (true);


--
-- Name: command_idempotency; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.command_idempotency ENABLE ROW LEVEL SECURITY;

--
-- Name: command_idempotency command_idempotency_tenant_policy; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY command_idempotency_tenant_policy ON app.command_idempotency USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: cycle_logs; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.cycle_logs ENABLE ROW LEVEL SECURITY;

--
-- Name: cycle_phase_logs; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.cycle_phase_logs ENABLE ROW LEVEL SECURITY;

--
-- Name: daily_nutrition_logs; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.daily_nutrition_logs ENABLE ROW LEVEL SECURITY;

--
-- Name: daily_readiness_logs; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.daily_readiness_logs ENABLE ROW LEVEL SECURITY;

--
-- Name: exercise_set_logs; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.exercise_set_logs ENABLE ROW LEVEL SECURITY;

--
-- Name: food_item_entries; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.food_item_entries ENABLE ROW LEVEL SECURITY;

--
-- Name: logged_meal_entries; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.logged_meal_entries ENABLE ROW LEVEL SECURITY;

--
-- Name: logged_meals; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.logged_meals ENABLE ROW LEVEL SECURITY;

--
-- Name: nutrition_diary_command_events; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.nutrition_diary_command_events ENABLE ROW LEVEL SECURITY;

--
-- Name: nutrition_diary_event_cursors; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.nutrition_diary_event_cursors ENABLE ROW LEVEL SECURITY;

--
-- Name: photo_cleanup_dispatch_state; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.photo_cleanup_dispatch_state ENABLE ROW LEVEL SECURITY;

--
-- Name: photo_journey_entries; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.photo_journey_entries ENABLE ROW LEVEL SECURITY;

--
-- Name: photo_object_cleanups; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.photo_object_cleanups ENABLE ROW LEVEL SECURITY;

--
-- Name: photo_object_reconcile_targets photo_object_reconcile_maintenance; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY photo_object_reconcile_maintenance ON app.photo_object_reconcile_targets TO musclecuties_photo_reconcile USING (true) WITH CHECK (true);


--
-- Name: photo_object_reconcile_targets photo_object_reconcile_owner_access; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY photo_object_reconcile_owner_access ON app.photo_object_reconcile_targets TO musclecuties_photo_reconcile_owner USING (true) WITH CHECK (true);


--
-- Name: photo_object_reconcile_targets photo_object_reconcile_stage_purge; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY photo_object_reconcile_stage_purge ON app.photo_object_reconcile_targets TO musclecuties_photo_purge_stage_owner USING (true) WITH CHECK (true);


--
-- Name: photo_object_reconcile_targets; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.photo_object_reconcile_targets ENABLE ROW LEVEL SECURITY;

--
-- Name: photo_upload_intents; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.photo_upload_intents ENABLE ROW LEVEL SECURITY;

--
-- Name: recipes; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.recipes ENABLE ROW LEVEL SECURITY;

--
-- Name: saved_meal_templates; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.saved_meal_templates ENABLE ROW LEVEL SECURITY;

--
-- Name: symptom_logs; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.symptom_logs ENABLE ROW LEVEL SECURITY;

--
-- Name: cycle_logs tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.cycle_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: cycle_phase_logs tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.cycle_phase_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: daily_nutrition_logs tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.daily_nutrition_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: daily_readiness_logs tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.daily_readiness_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: exercise_set_logs tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.exercise_set_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: food_item_entries tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.food_item_entries USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: logged_meal_entries tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.logged_meal_entries USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: logged_meals tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.logged_meals USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: nutrition_diary_command_events tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.nutrition_diary_command_events USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: nutrition_diary_event_cursors tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.nutrition_diary_event_cursors USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: photo_journey_entries tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.photo_journey_entries USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: photo_object_cleanups tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.photo_object_cleanups USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: photo_upload_intents tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.photo_upload_intents USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: recipes tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.recipes USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: saved_meal_templates tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.saved_meal_templates USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: symptom_logs tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.symptom_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: user_exercise_preferences tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.user_exercise_preferences USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: user_food_items tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.user_food_items USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: user_meal_template_entries tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.user_meal_template_entries USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: user_meal_templates tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.user_meal_templates USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: user_movement_notes tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.user_movement_notes USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: user_profile_snapshots tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.user_profile_snapshots USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: user_profiles tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.user_profiles USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: user_quiz_responses tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.user_quiz_responses USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: users tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.users USING ((id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: workout_day_exercises tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.workout_day_exercises USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: workout_days tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.workout_days USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: workout_exercise_logs tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.workout_exercise_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: workout_injury_logs tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.workout_injury_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: workout_logs tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.workout_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: workout_plans tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.workout_plans USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: workout_session_logs tenant_isolation; Type: POLICY; Schema: app; Owner: -
--

CREATE POLICY tenant_isolation ON app.workout_session_logs USING ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));


--
-- Name: user_exercise_preferences; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.user_exercise_preferences ENABLE ROW LEVEL SECURITY;

--
-- Name: user_food_items; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.user_food_items ENABLE ROW LEVEL SECURITY;

--
-- Name: user_meal_template_entries; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.user_meal_template_entries ENABLE ROW LEVEL SECURITY;

--
-- Name: user_meal_templates; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.user_meal_templates ENABLE ROW LEVEL SECURITY;

--
-- Name: user_movement_notes; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.user_movement_notes ENABLE ROW LEVEL SECURITY;

--
-- Name: user_profile_snapshots; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.user_profile_snapshots ENABLE ROW LEVEL SECURITY;

--
-- Name: user_profiles; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.user_profiles ENABLE ROW LEVEL SECURITY;

--
-- Name: user_quiz_responses; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.user_quiz_responses ENABLE ROW LEVEL SECURITY;

--
-- Name: users; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.users ENABLE ROW LEVEL SECURITY;

--
-- Name: workout_day_exercises; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.workout_day_exercises ENABLE ROW LEVEL SECURITY;

--
-- Name: workout_days; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.workout_days ENABLE ROW LEVEL SECURITY;

--
-- Name: workout_exercise_logs; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.workout_exercise_logs ENABLE ROW LEVEL SECURITY;

--
-- Name: workout_injury_logs; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.workout_injury_logs ENABLE ROW LEVEL SECURITY;

--
-- Name: workout_logs; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.workout_logs ENABLE ROW LEVEL SECURITY;

--
-- Name: workout_plans; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.workout_plans ENABLE ROW LEVEL SECURITY;

--
-- Name: workout_session_logs; Type: ROW SECURITY; Schema: app; Owner: -
--

ALTER TABLE app.workout_session_logs ENABLE ROW LEVEL SECURITY;

--
-- PostgreSQL database dump complete
--