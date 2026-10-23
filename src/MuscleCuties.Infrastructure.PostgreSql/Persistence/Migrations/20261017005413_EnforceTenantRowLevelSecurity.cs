using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceTenantRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE tenant_table text;
                BEGIN
                    FOREACH tenant_table IN ARRAY ARRAY[
                        'user_profiles', 'user_profile_snapshots', 'user_quiz_responses',
                        'cycle_logs', 'cycle_phase_logs', 'symptom_logs', 'daily_readiness_logs',
                        'workout_injury_logs', 'workout_plans', 'workout_days', 'workout_day_exercises',
                        'workout_session_logs', 'exercise_set_logs', 'workout_logs', 'workout_exercise_logs',
                        'user_exercise_preferences', 'user_movement_notes', 'daily_nutrition_logs',
                        'food_item_entries', 'user_food_items', 'user_meal_templates', 'user_meal_template_entries',
                        'logged_meals', 'logged_meal_entries', 'recipes', 'saved_meal_templates', 'photo_journey_entries'
                    ] LOOP
                        EXECUTE format('ALTER TABLE app.%I ENABLE ROW LEVEL SECURITY', tenant_table);
                        EXECUTE format('ALTER TABLE app.%I FORCE ROW LEVEL SECURITY', tenant_table);
                        EXECUTE format(
                            'CREATE POLICY tenant_isolation ON app.%I USING (user_id = NULLIF(current_setting(''app.user_id'', true), '''')::uuid) WITH CHECK (user_id = NULLIF(current_setting(''app.user_id'', true), '''')::uuid)',
                            tenant_table);
                    END LOOP;

                    ALTER TABLE app.users ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE app.users FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON app.users
                        USING (id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                        WITH CHECK (id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                    CREATE POLICY auth_user_lookup ON app.users FOR SELECT
                        USING (current_user = 'musclecuties_migrator');
                END $$;

                CREATE FUNCTION app.lookup_user_by_email(p_email text)
                RETURNS TABLE (user_id uuid, apple_user_id text, credential_type text, password_hash text)
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, app
                AS $function$
                    SELECT u.id, u.apple_user_id, u.credential_type, u.password_hash
                    FROM app.users AS u
                    WHERE u.normalized_email = lower(btrim(p_email))
                    LIMIT 1
                $function$;
                REVOKE ALL ON FUNCTION app.lookup_user_by_email(text) FROM PUBLIC;

                CREATE FUNCTION app.lookup_user_by_apple_id(p_apple_user_id text)
                RETURNS TABLE (user_id uuid, apple_user_id text, credential_type text, password_hash text)
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = pg_catalog, app
                AS $function$
                    SELECT u.id, u.apple_user_id, u.credential_type, u.password_hash
                    FROM app.users AS u
                    WHERE u.apple_user_id = p_apple_user_id
                    LIMIT 1
                $function$;
                REVOKE ALL ON FUNCTION app.lookup_user_by_apple_id(text) FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE tenant_table text;
                BEGIN
                    FOREACH tenant_table IN ARRAY ARRAY[
                        'user_profiles', 'user_profile_snapshots', 'user_quiz_responses',
                        'cycle_logs', 'cycle_phase_logs', 'symptom_logs', 'daily_readiness_logs',
                        'workout_injury_logs', 'workout_plans', 'workout_days', 'workout_day_exercises',
                        'workout_session_logs', 'exercise_set_logs', 'workout_logs', 'workout_exercise_logs',
                        'user_exercise_preferences', 'user_movement_notes', 'daily_nutrition_logs',
                        'food_item_entries', 'user_food_items', 'user_meal_templates', 'user_meal_template_entries',
                        'logged_meals', 'logged_meal_entries', 'recipes', 'saved_meal_templates', 'photo_journey_entries'
                    ] LOOP
                        EXECUTE format('DROP POLICY tenant_isolation ON app.%I', tenant_table);
                        EXECUTE format('ALTER TABLE app.%I NO FORCE ROW LEVEL SECURITY', tenant_table);
                        EXECUTE format('ALTER TABLE app.%I DISABLE ROW LEVEL SECURITY', tenant_table);
                    END LOOP;

                    DROP FUNCTION app.lookup_user_by_apple_id(text);
                    DROP FUNCTION app.lookup_user_by_email(text);
                    DROP POLICY auth_user_lookup ON app.users;
                    DROP POLICY tenant_isolation ON app.users;
                    ALTER TABLE app.users NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE app.users DISABLE ROW LEVEL SECURITY;
                END $$;
                """);
        }
    }
}
