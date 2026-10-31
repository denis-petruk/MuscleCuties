-- Run as a PostgreSQL administrator after applying the application migrations.
-- Roles are non-login groups. Provision secret-bearing login roles separately and
-- grant each service login the matching group; do not put credentials in source.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_migrator') THEN
        CREATE ROLE musclecuties_migrator NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app') THEN
        CREATE ROLE musclecuties_app NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_auth') THEN
        CREATE ROLE musclecuties_auth NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_dispatch') THEN
        CREATE ROLE musclecuties_cleanup_dispatch NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_discovery_owner') THEN
        CREATE ROLE musclecuties_cleanup_discovery_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;

GRANT musclecuties_migrator TO CURRENT_USER;
-- Stable owner membership lets the restricted migration group transfer and drop
-- the SECURITY DEFINER function without CREATEROLE or temporary grant authority.
GRANT musclecuties_cleanup_discovery_owner TO musclecuties_migrator;
DO $$
BEGIN
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO musclecuties_app, musclecuties_auth, musclecuties_migrator', current_database());
END $$;
ALTER SCHEMA app OWNER TO musclecuties_migrator;
ALTER SCHEMA catalog OWNER TO musclecuties_migrator;
ALTER SCHEMA ops OWNER TO musclecuties_migrator;
GRANT CREATE ON SCHEMA app, catalog, ops TO musclecuties_migrator;
GRANT USAGE ON SCHEMA app, catalog TO musclecuties_app;
GRANT USAGE ON SCHEMA app, catalog, ops TO musclecuties_migrator;
GRANT USAGE ON SCHEMA app TO musclecuties_auth;

DO $$
DECLARE table_row record;
BEGIN
    FOR table_row IN
        SELECT n.nspname AS schema_name, c.relname AS table_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        WHERE n.nspname IN ('app', 'catalog', 'ops') AND c.relkind IN ('r', 'p')
          -- This table has a dedicated restricted owner established by its migration.
          AND NOT (n.nspname = 'app' AND c.relname = 'photo_object_reconcile_targets')
    LOOP
        EXECUTE format('ALTER TABLE %I.%I OWNER TO musclecuties_migrator', table_row.schema_name, table_row.table_name);
    END LOOP;
END $$;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
    app.users,
    app.user_profiles,
    app.command_idempotency,
    app.user_profile_snapshots,
    app.user_quiz_responses,
    app.cycle_logs,
    app.cycle_phase_logs,
    app.symptom_logs,
    app.daily_readiness_logs,
    app.workout_injury_logs,
    app.workout_plans,
    app.workout_days,
    app.workout_day_exercises,
    app.workout_session_logs,
    app.exercise_set_logs,
    app.workout_logs,
    app.workout_exercise_logs,
    app.user_exercise_preferences,
    app.user_movement_notes,
    app.daily_nutrition_logs,
    app.food_item_entries,
    app.user_food_items,
    app.user_meal_templates,
    app.user_meal_template_entries,
    app.logged_meals,
    app.logged_meal_entries,
    app.recipes,
    app.saved_meal_templates,
    app.photo_journey_entries
TO musclecuties_app;
GRANT SELECT ON ALL TABLES IN SCHEMA catalog TO musclecuties_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON app.email_challenges TO musclecuties_auth;
ALTER FUNCTION app.lookup_user_by_email(text) OWNER TO musclecuties_migrator;
ALTER FUNCTION app.lookup_user_by_apple_id(text) OWNER TO musclecuties_migrator;
REVOKE ALL ON FUNCTION app.lookup_user_by_email(text), app.lookup_user_by_apple_id(text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION app.lookup_user_by_email(text), app.lookup_user_by_apple_id(text) TO musclecuties_auth;

-- The application role has no access to global auth challenges or sync operations.
-- Grant future catalog reads automatically; review app-table grants when migrations add tables.
ALTER DEFAULT PRIVILEGES FOR ROLE musclecuties_migrator IN SCHEMA catalog
    GRANT SELECT ON TABLES TO musclecuties_app;

GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA app, catalog, ops TO musclecuties_migrator;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA app, catalog, ops TO musclecuties_migrator;

-- The bootstrap CREATE grant is only needed to create EF's history table.
REVOKE CREATE ON SCHEMA public FROM musclecuties_migrator;

-- After creating login principals outside this file, an administrator may run:
-- GRANT musclecuties_app TO musclecuties_api_login;
-- GRANT musclecuties_auth TO musclecuties_auth_login;
-- Grant musclecuties_cleanup_dispatch only to a dedicated cleanup dispatcher login,
-- never to the general API login or any role inherited by it. Also keep
-- musclecuties_cleanup_discovery_owner ungranted to login principals: it is only
-- the SECURITY DEFINER function owner. The discovery migration fails closed if
-- musclecuties_app already inherits either restricted role.
-- GRANT musclecuties_cleanup_dispatch TO musclecuties_cleanup_dispatch_login;
-- GRANT musclecuties_migrator TO musclecuties_migration_login;
-- Run EF migrations with Options='-c role=musclecuties_migrator' so new objects
-- are created by the migration group and its default privileges remain effective.
