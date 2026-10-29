using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Npgsql;

return await TempDbResetProgram.RunAsync(args);

internal static class TempDbResetProgram
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__PostgreSql";
    private const string BaselineMigrationId = "20261017003443_InitialPostgreSqlApplication";
    private const string EfProductVersion = "10.0.4";
    private const string RecreateSwitch = "--recreate";
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;

    public static async Task<int> RunAsync(string[] args)
    {
        var recreateFromScratch = args.Contains(RecreateSwitch, StringComparer.OrdinalIgnoreCase);
        var connectionString = ResolveConnectionString(args);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine(
                "Error: PostgreSQL connection string not provided. Set ConnectionStrings__PostgreSql or pass it as the first argument.");
            return FailureExitCode;
        }

        try
        {
            var connectionBuilder = BuildConnection(connectionString);
            var options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(connectionBuilder.ConnectionString, provider =>
                    provider.MigrationsAssembly(typeof(PostgresDbContext).Assembly.GetName().Name))
                .Options;

            await using var context = new PostgresDbContext(options);

            Console.WriteLine("1. Ensuring database roles and schema privileges...");
            await context.Database.ExecuteSqlRawAsync(EnsureRolesAndSchemaSql);

            if (recreateFromScratch || !await HasExistingSchemaAsync(context))
            {
                Console.WriteLine("2. Recreating schemas and applying consolidated baseline migration in-place...");
                await context.Database.ExecuteSqlRawAsync(DropAllSchemasSql);
                await context.Database.ExecuteSqlRawAsync(EnsureRolesAndSchemaSql);
                await context.Database.MigrateAsync();
            }
            else
            {
                Console.WriteLine("2. Purging all users, profiles, and auth/tenant records...");
                await context.Database.ExecuteSqlRawAsync(PurgeAllUsersAndTenantDataSql);

                Console.WriteLine("3. Reconciling database schema in-place to consolidated baseline...");
                await context.Database.ExecuteSqlRawAsync(InPlaceSchemaReconciliationSql);

                Console.WriteLine("4. Resetting __EFMigrationsHistory to single consolidated baseline migration...");
                await context.Database.ExecuteSqlRawAsync(
                    $"""
                    CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory" (
                        "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
                        "ProductVersion" character varying(32) NOT NULL
                    );
                    TRUNCATE TABLE public."__EFMigrationsHistory";
                    INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ('{BaselineMigrationId}', '{EfProductVersion}');
                    """);
            }

            await context.Database.ExecuteSqlRawAsync(GrantRuntimePrivilegesSql);

            var manifestPath = ResolveManifestPath();
            if (manifestPath is not null)
            {
                Console.WriteLine($"5. Seeding/reconciling canonical catalog from {manifestPath}...");
                var summary = await PostgresCanonicalCatalogSeeder.IngestFromManifestAsync(context, manifestPath);
                Console.WriteLine($"   Canonical catalog verified: {summary.TotalManifestRows} rows (SHA-256 {summary.ManifestIntegritySha256}).");
            }

            var userCount = await context.Users.IgnoreQueryFilters().CountAsync();
            var profileCount = await context.UserProfiles.IgnoreQueryFilters().CountAsync();
            var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToArray();

            Console.WriteLine($"Completed: Users={userCount}, Profiles={profileCount}, AppliedMigrations=[{string.Join(", ", appliedMigrations)}], PendingMigrations={pendingMigrations.Length}.");
            Console.WriteLine("You can now safely delete the 'temp-db-reset' folder.");
            return SuccessExitCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Reset failed ({ex.GetType().Name}): {ex.Message}");
            return FailureExitCode;
        }
    }

    private static string? ResolveConnectionString(string[] args)
    {
        var positional = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(positional))
            return positional;

        return Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
    }

    private static NpgsqlConnectionStringBuilder BuildConnection(string rawConnectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(rawConnectionString);
        var isLoopback = builder.Host is "127.0.0.1" or "localhost";
        if (!isLoopback)
        {
            var certCandidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "certs", "global-bundle.pem"),
                Path.Combine(AppContext.BaseDirectory, "certs", "global-bundle.pem"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "certs", "global-bundle.pem")
            };
            var foundCert = certCandidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(builder.RootCertificate) && foundCert is not null)
                builder.RootCertificate = foundCert;
        }

        return builder;
    }

    private static async Task<bool> HasExistingSchemaAsync(PostgresDbContext context)
    {
        await using var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'app' AND table_name = 'users')",
            connection);
        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }

    private static string? ResolveManifestPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "MuscleCuties.DbMaintenance", "Seed", "CanonicalCatalogManifest.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "src", "MuscleCuties.DbMaintenance", "Seed", "CanonicalCatalogManifest.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "MuscleCuties.DbMaintenance", "Seed", "CanonicalCatalogManifest.json")
        };
        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }

    private const string DropAllSchemasSql = """
        DROP SCHEMA IF EXISTS app CASCADE;
        DROP SCHEMA IF EXISTS catalog CASCADE;
        DROP SCHEMA IF EXISTS ops CASCADE;
        DROP TABLE IF EXISTS public."__EFMigrationsHistory" CASCADE;
        """;

    private const string EnsureRolesAndSchemaSql = """
        DO $$
        DECLARE r text;
        BEGIN
            FOREACH r IN ARRAY ARRAY[
                'musclecuties_migrator',
                'musclecuties_app',
                'musclecuties_auth',
                'musclecuties_cleanup_discovery_owner',
                'musclecuties_cleanup_dispatch',
                'musclecuties_photo_reconcile_owner',
                'musclecuties_photo_reconcile',
                'musclecuties_photo_purge_stage_owner',
                'musclecuties_workout_discovery_owner',
                'musclecuties_workout_dispatch'
            ] LOOP
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
                    EXECUTE format('CREATE ROLE %I NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE', r);
                END IF;
            END LOOP;

            IF NOT pg_has_role(current_user, 'musclecuties_photo_purge_stage_owner', 'MEMBER') THEN
                EXECUTE format('GRANT musclecuties_photo_purge_stage_owner TO %I', current_user);
            END IF;
            IF NOT pg_has_role(current_user, 'musclecuties_photo_reconcile_owner', 'MEMBER') THEN
                EXECUTE format('GRANT musclecuties_photo_reconcile_owner TO %I', current_user);
            END IF;
            GRANT musclecuties_photo_purge_stage_owner TO musclecuties_migrator;
            GRANT musclecuties_photo_reconcile_owner TO musclecuties_migrator;
        END $$;
        CREATE SCHEMA IF NOT EXISTS app;
        CREATE SCHEMA IF NOT EXISTS catalog;
        CREATE SCHEMA IF NOT EXISTS ops;
        GRANT USAGE, CREATE ON SCHEMA catalog TO musclecuties_migrator;
        """;

    private const string PurgeAllUsersAndTenantDataSql = """
        DO $$
        DECLARE
            t text;
        BEGIN
            FOREACH t IN ARRAY ARRAY[
                'workout_injury_prescription_links',
                'workout_injury_prescriptions',
                'workout_injury_omitted_slots',
                'workout_injury_episodes',
                'workout_cycle_readiness_occurrences',
                'workout_exercise_logs',
                'workout_logs',
                'workout_day_exercises',
                'workout_days',
                'workout_plans',
                'workout_plan_revisions_v02',
                'workout_plan_proposals_v02',
                'workout_completion_day_credits',
                'workout_account_planning_states',
                'exercise_set_logs',
                'workout_session_logs',
                'workout_injury_logs',
                'strength_style_offers',
                'hiit_evidence_capture_challenges',
                'user_exercise_preferences',
                'user_movement_notes',
                'logged_meal_entries',
                'logged_meals',
                'user_meal_template_entries',
                'user_meal_templates',
                'food_item_entries',
                'daily_nutrition_logs',
                'user_food_items',
                'nutrition_diary_command_events',
                'nutrition_diary_event_cursors',
                'recipes',
                'saved_meal_templates',
                'symptom_logs',
                'cycle_phase_logs',
                'cycle_logs',
                'daily_readiness_logs',
                'photo_journey_entries',
                'photo_object_cleanups',
                'photo_upload_intents',
                'user_quiz_responses',
                'user_profile_snapshots',
                'user_profiles',
                'command_idempotency',
                'source_account_mappings',
                'source_account_claim_nonces',
                'revoked_access_tokens',
                'refresh_sessions',
                'email_challenge_sessions',
                'email_challenges',
                'apple_sign_in_nonces',
                'password_login_attempts',
                'photo_object_reconcile_targets',
                'users'
            ] LOOP
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'app' AND table_name = t) THEN
                    EXECUTE format('DELETE FROM app.%I', t);
                END IF;
            END LOOP;

            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'app' AND table_name = 'photo_cleanup_dispatch_state') THEN
                UPDATE app.photo_cleanup_dispatch_state SET after_owner_id = NULL;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'app' AND table_name = 'workout_reconciliation_cursor') THEN
                UPDATE app.workout_reconciliation_cursor SET after_owner_id = NULL;
            END IF;
        END $$;
        """;

    private const string InPlaceSchemaReconciliationSql = """
        ALTER TABLE app.users ADD COLUMN IF NOT EXISTS privacy_notice_accepted_at_utc timestamp with time zone NULL;
        ALTER TABLE app.users ADD COLUMN IF NOT EXISTS privacy_notice_version character varying(64) NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS uq_users_normalized_username
            ON app.users (lower(btrim(username)))
            WHERE username IS NOT NULL AND btrim(username) <> '';

        ALTER TABLE app.user_profiles ADD COLUMN IF NOT EXISTS concurrency_version integer NOT NULL DEFAULT 1;
        ALTER TABLE app.user_profiles ADD COLUMN IF NOT EXISTS available_workout_weekdays_json jsonb NOT NULL DEFAULT '[]'::jsonb;
        ALTER TABLE app.user_profiles ADD COLUMN IF NOT EXISTS activity_session_durations_json jsonb NOT NULL DEFAULT '{}'::jsonb;
        ALTER TABLE app.user_profiles ADD COLUMN IF NOT EXISTS requested_activity_sessions_per_week_json jsonb NOT NULL DEFAULT '{}'::jsonb;
        ALTER TABLE app.user_profiles ADD COLUMN IF NOT EXISTS excluded_strength_exercise_ids_json jsonb NOT NULL DEFAULT '[]'::jsonb;
        ALTER TABLE app.user_profiles DROP COLUMN IF EXISTS cycle_tracking_mode;
        ALTER TABLE app.user_profiles DROP COLUMN IF EXISTS session_duration_minutes;

        ALTER TABLE app.workout_injury_logs ADD COLUMN IF NOT EXISTS cleared_date date NULL;
        ALTER TABLE app.workout_logs ADD COLUMN IF NOT EXISTS planned_session_id uuid NULL;

        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS day_order integer NULL;
        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS is_optional boolean NOT NULL DEFAULT false;
        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS is_easier_alternative boolean NOT NULL DEFAULT false;
        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS is_removed_by_readiness boolean NOT NULL DEFAULT false;
        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS is_removed_by_user_adjustment boolean NOT NULL DEFAULT false;
        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS is_removed_by_safety boolean NOT NULL DEFAULT false;
        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS safety_injury_id uuid NULL;
        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS safety_activity_type_id integer NULL;
        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS activity_type_id integer NULL;
        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS planned_main_duration_minutes numeric NULL;
        ALTER TABLE app.workout_days ADD COLUMN IF NOT EXISTS planned_activity_prescription_json jsonb NULL;

        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS origin_workout_day_exercise_id uuid NULL;
        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS planning_template_id text NULL;
        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS planning_template_version text NULL;
        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS planning_session_index integer NULL;
        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS planning_slot_id text NULL;
        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS optional_sets integer NOT NULL DEFAULT 0;
        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS target_repetitions_in_reserve integer NULL;
        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS is_optional boolean NOT NULL DEFAULT false;
        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS is_removed_by_readiness boolean NOT NULL DEFAULT false;
        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS injury_original_exercise_id integer NULL;
        ALTER TABLE app.workout_day_exercises ADD COLUMN IF NOT EXISTS injury_restoration_required_from_date date NULL;

        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS planned_workout_day_id uuid NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS planned_time_zone_id text NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS planned_time_zone_version bigint NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS planned_local_date date NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS planned_prescription_snapshot_json jsonb NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS unplanned_activity_type integer NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS unplanned_completion_local_date date NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS unplanned_completion_time_zone_id text NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS unplanned_completion_time_zone_version bigint NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS confirmed_active_seconds integer NOT NULL DEFAULT 0;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS timing_interval_id uuid NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS timing_interval_started_at_utc timestamp with time zone NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS timing_interval_last_confirmed_at_utc timestamp with time zone NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN IF NOT EXISTS timing_interval_confirmed_seconds integer NOT NULL DEFAULT 0;

        CREATE TABLE IF NOT EXISTS catalog.workout_template_volume_floors (
            template_id text PRIMARY KEY CHECK (length(trim(template_id)) > 0),
            template_version text NOT NULL CHECK (length(trim(template_version)) > 0),
            minimum_working_sets integer NOT NULL CHECK (minimum_working_sets > 0),
            minimum_exercises integer NOT NULL CHECK (minimum_exercises > 0),
            slot_floors jsonb NOT NULL CHECK (jsonb_typeof(slot_floors) = 'object'),
            reviewer text NULL,
            evidence_reference text NULL,
            evidence_sha256 text NULL,
            reviewed_on date NULL
        );
        ALTER TABLE catalog.workout_template_volume_floors
            DROP CONSTRAINT IF EXISTS workout_template_volume_floors_reviewer_check,
            DROP CONSTRAINT IF EXISTS workout_template_volume_floors_evidence_reference_check,
            DROP CONSTRAINT IF EXISTS workout_template_volume_floors_evidence_sha256_check,
            ALTER COLUMN reviewer DROP NOT NULL,
            ALTER COLUMN evidence_reference DROP NOT NULL,
            ALTER COLUMN evidence_sha256 DROP NOT NULL,
            ALTER COLUMN reviewed_on DROP NOT NULL;

        CREATE TABLE IF NOT EXISTS app.workout_account_planning_states (
            id uuid PRIMARY KEY,
            user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            time_zone_id text NULL,
            time_zone_version bigint NOT NULL,
            baseline_experience integer NOT NULL,
            effective_experience integer NOT NULL,
            experience_version bigint NOT NULL,
            promotion_pending boolean NOT NULL,
            answer_version bigint NOT NULL DEFAULT 0,
            answers_json jsonb NULL,
            safety_regeneration_pending boolean NOT NULL DEFAULT false,
            cycle_readiness_pending boolean NOT NULL DEFAULT false,
            cycle_readiness_pain_review_date date NULL,
            deload_evidence_through_exclusive date NULL,
            deload_start_date date NULL,
            deload_end_exclusive date NULL,
            CONSTRAINT uq_workout_account_planning_states_user_id_id UNIQUE(user_id, id)
        );
        ALTER TABLE app.workout_account_planning_states ADD COLUMN IF NOT EXISTS answer_version bigint NOT NULL DEFAULT 0;
        ALTER TABLE app.workout_account_planning_states ADD COLUMN IF NOT EXISTS answers_json jsonb NULL;
        ALTER TABLE app.workout_account_planning_states ADD COLUMN IF NOT EXISTS safety_regeneration_pending boolean NOT NULL DEFAULT false;
        ALTER TABLE app.workout_account_planning_states ADD COLUMN IF NOT EXISTS cycle_readiness_pending boolean NOT NULL DEFAULT false;
        ALTER TABLE app.workout_account_planning_states ADD COLUMN IF NOT EXISTS cycle_readiness_pain_review_date date NULL;
        ALTER TABLE app.workout_account_planning_states ADD COLUMN IF NOT EXISTS deload_evidence_through_exclusive date NULL;
        ALTER TABLE app.workout_account_planning_states ADD COLUMN IF NOT EXISTS deload_start_date date NULL;
        ALTER TABLE app.workout_account_planning_states ADD COLUMN IF NOT EXISTS deload_end_exclusive date NULL;

        CREATE TABLE IF NOT EXISTS app.workout_cycle_readiness_occurrences (
            id uuid PRIMARY KEY,
            user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            workout_day_id uuid NOT NULL,
            local_date date NOT NULL,
            input_fingerprint text NOT NULL,
            prescription_json jsonb NOT NULL,
            updated_at_utc timestamp with time zone NOT NULL,
            CONSTRAINT uq_workout_cycle_readiness_occurrences_user_id_id UNIQUE(user_id, id),
            CONSTRAINT fk_workout_cycle_readiness_occurrences_day FOREIGN KEY (user_id, workout_day_id)
                REFERENCES app.workout_days(user_id, id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS ix_workout_cycle_readiness_occurrences_user_id ON app.workout_cycle_readiness_occurrences(user_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_workout_cycle_readiness_occurrences_user_id_workout_day_id
            ON app.workout_cycle_readiness_occurrences(user_id, workout_day_id);
        ALTER TABLE app.workout_cycle_readiness_occurrences ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.workout_cycle_readiness_occurrences FORCE ROW LEVEL SECURITY;
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'app' AND tablename = 'workout_cycle_readiness_occurrences' AND policyname = 'tenant_isolation') THEN
                CREATE POLICY tenant_isolation ON app.workout_cycle_readiness_occurrences
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
            END IF;
        END $$;

        CREATE TABLE IF NOT EXISTS app.workout_injury_omitted_slots (
            id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            plan_id uuid NOT NULL, scheduled_date date NOT NULL, template_id text NOT NULL,
            template_version text NOT NULL, session_index integer NOT NULL, slot_id text NOT NULL,
            episode_id uuid NOT NULL, origin_workout_day_exercise_id uuid NOT NULL,
            original_exercise_id integer NOT NULL, original_sets integer NOT NULL,
            original_optional_sets integer NOT NULL, original_reps integer NOT NULL,
            original_duration_seconds integer NULL, original_is_optional boolean NOT NULL,
            original_is_removed_by_readiness boolean NOT NULL,
            CONSTRAINT uq_workout_injury_omitted_slots_user_id_id UNIQUE (user_id, id)
        );
        CREATE INDEX IF NOT EXISTS ix_workout_injury_omitted_slots_user_id ON app.workout_injury_omitted_slots(user_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_workout_injury_omitted_slot_identity ON app.workout_injury_omitted_slots
            (user_id, plan_id, scheduled_date, template_id, template_version, session_index, slot_id, episode_id);
        ALTER TABLE app.workout_injury_omitted_slots ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.workout_injury_omitted_slots FORCE ROW LEVEL SECURITY;
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'app' AND tablename = 'workout_injury_omitted_slots' AND policyname = 'tenant_isolation') THEN
                CREATE POLICY tenant_isolation ON app.workout_injury_omitted_slots
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
            END IF;
        END $$;

        CREATE TABLE IF NOT EXISTS app.strength_style_offers (
            id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            plan_id uuid NOT NULL, revision_id uuid NOT NULL, current_style_id integer NOT NULL,
            alternative_style_ids integer[] NOT NULL, dismissed_at_utc timestamp with time zone NULL,
            CONSTRAINT uq_strength_style_offers_user_id_id UNIQUE (user_id, id)
        );
        CREATE INDEX IF NOT EXISTS ix_strength_style_offers_user_id ON app.strength_style_offers(user_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_strength_style_offers_user_id_revision_id ON app.strength_style_offers(user_id, revision_id);
        ALTER TABLE app.strength_style_offers ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.strength_style_offers FORCE ROW LEVEL SECURITY;
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'app' AND tablename = 'strength_style_offers' AND policyname = 'tenant_isolation') THEN
                CREATE POLICY tenant_isolation ON app.strength_style_offers
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
            END IF;
        END $$;

        CREATE TABLE IF NOT EXISTS app.hiit_evidence_capture_challenges (
            id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            issued_at_utc timestamp with time zone NOT NULL, expires_at_utc timestamp with time zone NOT NULL,
            issued_local_date date NOT NULL, time_zone_version bigint NOT NULL, consumed_at_utc timestamp with time zone NULL,
            consumed_request_hash text NULL, result_answer_version bigint NULL, result_safety_plan_changed boolean NULL,
            CONSTRAINT uq_hiit_evidence_capture_challenges_user_id_id UNIQUE (user_id, id)
        );
        CREATE INDEX IF NOT EXISTS ix_hiit_evidence_capture_challenges_user_id ON app.hiit_evidence_capture_challenges(user_id);
        CREATE INDEX IF NOT EXISTS ix_hiit_evidence_capture_challenges_user_id_expires_at_utc ON app.hiit_evidence_capture_challenges(user_id, expires_at_utc);
        ALTER TABLE app.hiit_evidence_capture_challenges ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.hiit_evidence_capture_challenges FORCE ROW LEVEL SECURITY;
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'app' AND tablename = 'hiit_evidence_capture_challenges' AND policyname = 'tenant_isolation') THEN
                CREATE POLICY tenant_isolation ON app.hiit_evidence_capture_challenges
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
            END IF;
        END $$;

        GRANT SELECT (user_id, promotion_pending, safety_regeneration_pending, cycle_readiness_pending)
            ON app.workout_account_planning_states TO musclecuties_workout_discovery_owner;
        CREATE OR REPLACE FUNCTION app.discover_pending_workout_owners(p_after uuid, p_limit integer)
        RETURNS TABLE (owner_id uuid)
        LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, app, pg_temp AS $function$
            SELECT s.user_id FROM app.workout_account_planning_states AS s
            WHERE (s.promotion_pending OR s.safety_regeneration_pending OR s.cycle_readiness_pending)
              AND (p_after IS NULL OR s.user_id > p_after)
            ORDER BY s.user_id
            LIMIT LEAST(GREATEST(COALESCE(p_limit, 0), 0), 100)
        $function$;
        GRANT CREATE ON SCHEMA app TO musclecuties_workout_discovery_owner;
        ALTER FUNCTION app.discover_pending_workout_owners(uuid, integer) OWNER TO musclecuties_workout_discovery_owner;
        REVOKE CREATE ON SCHEMA app FROM musclecuties_workout_discovery_owner;
        REVOKE ALL ON FUNCTION app.discover_pending_workout_owners(uuid, integer) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION app.discover_pending_workout_owners(uuid, integer) TO musclecuties_workout_dispatch;
        """;

    private const string GrantRuntimePrivilegesSql = """
        GRANT USAGE ON SCHEMA app, catalog TO musclecuties_migrator, musclecuties_app, musclecuties_auth;
        GRANT ALL ON ALL TABLES IN SCHEMA catalog TO musclecuties_migrator;
        GRANT SELECT ON ALL TABLES IN SCHEMA catalog TO musclecuties_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.workout_injury_episodes TO musclecuties_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.workout_injury_prescriptions TO musclecuties_app;
        GRANT SELECT, INSERT, DELETE ON app.workout_injury_prescription_links TO musclecuties_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.hiit_evidence_capture_challenges TO musclecuties_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.strength_style_offers TO musclecuties_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.workout_injury_omitted_slots TO musclecuties_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.workout_cycle_readiness_occurrences TO musclecuties_app;
        """;
}
