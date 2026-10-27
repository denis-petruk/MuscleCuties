using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027050000_AddWorkoutInjuryProvenance")]
public sealed class AddWorkoutInjuryProvenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE app.workout_injury_episodes (
            id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            injury_log_id uuid NOT NULL, site_flag integer NOT NULL, started_on date NOT NULL,
            cleared_on date NULL, clearance_time_zone_id text NULL, clearance_time_zone_version bigint NULL,
            CONSTRAINT uq_workout_injury_episodes_user_id_id UNIQUE (user_id, id)
        );
        CREATE INDEX ix_workout_injury_episodes_user_id ON app.workout_injury_episodes(user_id);
        CREATE INDEX ix_workout_injury_episodes_user_id_injury_log_id_cleared_on
            ON app.workout_injury_episodes(user_id, injury_log_id, cleared_on);
        CREATE UNIQUE INDEX ix_workout_injury_episodes_user_id_injury_log_id
            ON app.workout_injury_episodes(user_id, injury_log_id) WHERE cleared_on IS NULL;
        CREATE TABLE app.workout_injury_prescriptions (
            id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            episode_id uuid NOT NULL, workout_day_id uuid NOT NULL, original_workout_day_exercise_id uuid NOT NULL,
            original_exercise_id integer NOT NULL, original_sets integer NOT NULL, original_optional_sets integer NOT NULL, original_reps integer NOT NULL,
            original_duration_seconds integer NULL, original_is_optional boolean NOT NULL, original_is_removed_by_readiness boolean NOT NULL,
            replacement_workout_day_exercise_id uuid NULL, replacement_exercise_id integer NULL,
            CONSTRAINT uq_workout_injury_prescriptions_user_id_id UNIQUE (user_id, id)
        );
        CREATE INDEX ix_workout_injury_prescriptions_user_id ON app.workout_injury_prescriptions(user_id);
        CREATE UNIQUE INDEX ix_workout_injury_prescriptions_user_id_episode_id_original_workout_day_exercise_id
            ON app.workout_injury_prescriptions(user_id, episode_id, original_workout_day_exercise_id);
        ALTER TABLE app.workout_injury_episodes ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.workout_injury_episodes FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON app.workout_injury_episodes
            USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
            WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
        ALTER TABLE app.workout_injury_prescriptions ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.workout_injury_prescriptions FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON app.workout_injury_prescriptions
            USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
            WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("workout_injury_prescriptions", "app");
        migrationBuilder.DropTable("workout_injury_episodes", "app");
    }
}
