using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261028140000_AddWorkoutInjuryOmittedSlots")]
public sealed class AddWorkoutInjuryOmittedSlots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE app.workout_injury_omitted_slots (
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
        CREATE INDEX ix_workout_injury_omitted_slots_user_id ON app.workout_injury_omitted_slots(user_id);
        CREATE UNIQUE INDEX ix_workout_injury_omitted_slot_identity ON app.workout_injury_omitted_slots
            (user_id, plan_id, scheduled_date, template_id, template_version, session_index, slot_id, episode_id);
        ALTER TABLE app.workout_injury_omitted_slots ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.workout_injury_omitted_slots FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON app.workout_injury_omitted_slots
            USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
            WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.workout_injury_omitted_slots TO musclecuties_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("workout_injury_omitted_slots", "app");
}
