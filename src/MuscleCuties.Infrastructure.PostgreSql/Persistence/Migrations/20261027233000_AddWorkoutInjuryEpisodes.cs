using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027233000_AddWorkoutInjuryEpisodes")]
public sealed class AddWorkoutInjuryEpisodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE app.workout_injury_episodes (
            id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            source_injury_id uuid NOT NULL, site_flag integer NOT NULL,
            recorded_at_utc timestamp with time zone NOT NULL, cleared_date date NULL,
            source_deleted boolean NOT NULL,
            CONSTRAINT uq_workout_injury_episodes_user_id_id UNIQUE (user_id, id)
        );
        CREATE INDEX ix_workout_injury_episodes_user_id ON app.workout_injury_episodes(user_id);
        CREATE INDEX ix_injury_episodes_source_state
            ON app.workout_injury_episodes(user_id, source_injury_id, cleared_date, source_deleted);
        CREATE TABLE app.workout_injury_prescription_links (
            id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            workout_day_exercise_id uuid NOT NULL, injury_episode_id uuid NOT NULL,
            original_exercise_id integer NOT NULL,
            CONSTRAINT uq_workout_injury_prescription_links_user_id_id UNIQUE (user_id, id),
            FOREIGN KEY (user_id, workout_day_exercise_id) REFERENCES app.workout_day_exercises(user_id, id) ON DELETE CASCADE,
            FOREIGN KEY (user_id, injury_episode_id) REFERENCES app.workout_injury_episodes(user_id, id) ON DELETE RESTRICT
        );
        CREATE INDEX ix_workout_injury_prescription_links_user_id ON app.workout_injury_prescription_links(user_id);
        CREATE INDEX ix_injury_links_episode ON app.workout_injury_prescription_links(user_id, injury_episode_id);
        CREATE UNIQUE INDEX ux_injury_links_prescription_episode
            ON app.workout_injury_prescription_links(user_id, workout_day_exercise_id, injury_episode_id);
        ALTER TABLE app.workout_injury_episodes ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.workout_injury_episodes FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON app.workout_injury_episodes
            USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
            WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
        ALTER TABLE app.workout_injury_prescription_links ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.workout_injury_prescription_links FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON app.workout_injury_prescription_links
            USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
            WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("workout_injury_prescription_links", "app");
        migrationBuilder.DropTable("workout_injury_episodes", "app");
    }
}
