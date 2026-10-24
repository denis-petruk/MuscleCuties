using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261024150000_ReplaceWorkoutScheduleProfileFieldsV010")]
public sealed class ReplaceWorkoutScheduleProfileFieldsV010 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "available_workout_weekdays_json",
            schema: "app",
            table: "user_profiles",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");
        migrationBuilder.AddColumn<string>(
            name: "activity_session_durations_json",
            schema: "app",
            table: "user_profiles",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'{}'::jsonb");

        migrationBuilder.Sql("""
            WITH cleaned_profiles AS
            (
                SELECT id,
                       COALESCE(array_remove(
                           regexp_split_to_array(
                               trim(both ',' from regexp_replace(
                                   preferred_workout_activity_types,
                                   '(^|,)StrengthStyle:[^,]*',
                                   '',
                                   'gi')),
                               ','),
                           ''), ARRAY[]::text[]) AS activity_types
                FROM app.user_profiles
            )
            UPDATE app.user_profiles AS profile
            SET preferred_workout_activity_types = COALESCE(
                    (SELECT string_agg(activity_type, ',' ORDER BY activity_type)
                     FROM unnest(cleaned.activity_types) AS activities(activity_type)),
                    ''),
                available_workout_weekdays_json = to_jsonb(
                    (ARRAY['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']::text[])
                    [1: LEAST(7, GREATEST(2, profile.workout_days_per_week))]),
                activity_session_durations_json = COALESCE(
                    (SELECT jsonb_object_agg(activity_type, profile.session_duration_minutes)
                     FROM unnest(cleaned.activity_types) AS activities(activity_type)
                     WHERE activity_type ~ '^[A-Za-z]+$'),
                    '{}'::jsonb)
            FROM cleaned_profiles AS cleaned
            WHERE profile.id = cleaned.id;
            """);

        migrationBuilder.AddCheckConstraint(
            name: "ck_user_profiles_available_workout_weekdays_array",
            schema: "app",
            table: "user_profiles",
            sql: "jsonb_typeof(available_workout_weekdays_json) = 'array'");
        migrationBuilder.AddCheckConstraint(
            name: "ck_user_profiles_activity_session_durations_object",
            schema: "app",
            table: "user_profiles",
            sql: "jsonb_typeof(activity_session_durations_json) = 'object'");
        migrationBuilder.DropColumn(name: "cycle_tracking_mode", schema: "app", table: "user_profiles");
        migrationBuilder.DropColumn(name: "session_duration_minutes", schema: "app", table: "user_profiles");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "cycle_tracking_mode",
            schema: "app",
            table: "user_profiles",
            type: "text",
            nullable: false,
            defaultValue: "ManualPhaseLogging");
        migrationBuilder.AddColumn<int>(
            name: "session_duration_minutes",
            schema: "app",
            table: "user_profiles",
            type: "integer",
            nullable: false,
            defaultValue: 60);
        migrationBuilder.Sql("""
            UPDATE app.user_profiles AS profile
            SET session_duration_minutes = COALESCE(
                (SELECT value::integer
                 FROM jsonb_each_text(profile.activity_session_durations_json)
                 ORDER BY key
                 LIMIT 1),
                60);
            """);
        migrationBuilder.DropCheckConstraint(
            name: "ck_user_profiles_available_workout_weekdays_array",
            schema: "app",
            table: "user_profiles");
        migrationBuilder.DropCheckConstraint(
            name: "ck_user_profiles_activity_session_durations_object",
            schema: "app",
            table: "user_profiles");
        migrationBuilder.DropColumn(name: "available_workout_weekdays_json", schema: "app", table: "user_profiles");
        migrationBuilder.DropColumn(name: "activity_session_durations_json", schema: "app", table: "user_profiles");
    }
}
