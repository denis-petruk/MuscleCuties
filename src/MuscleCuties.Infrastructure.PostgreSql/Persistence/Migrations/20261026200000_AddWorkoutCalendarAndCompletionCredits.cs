using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261026200000_AddWorkoutCalendarAndCompletionCredits")]
public sealed class AddWorkoutCalendarAndCompletionCredits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE app.workout_account_planning_states (
                id uuid PRIMARY KEY,
                user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
                time_zone_id text NULL,
                time_zone_version bigint NOT NULL,
                baseline_experience integer NOT NULL,
                effective_experience integer NOT NULL,
                experience_version bigint NOT NULL,
                promotion_pending boolean NOT NULL,
                CONSTRAINT uq_workout_account_planning_states_user_id_id UNIQUE(user_id, id)
            );
            CREATE UNIQUE INDEX ix_workout_account_planning_states_user_id
                ON app.workout_account_planning_states(user_id);
            CREATE TABLE app.workout_completion_day_credits (
                id uuid PRIMARY KEY,
                user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
                local_date date NOT NULL,
                source_workout_log_id uuid NULL,
                source_session_id uuid NULL,
                accepted_at_utc timestamp with time zone NOT NULL,
                time_zone_id text NOT NULL,
                time_zone_version bigint NOT NULL,
                CONSTRAINT uq_workout_completion_day_credits_user_id_id UNIQUE(user_id, id)
            );
            CREATE INDEX ix_workout_completion_day_credits_user_id ON app.workout_completion_day_credits(user_id);
            CREATE UNIQUE INDEX ix_workout_completion_day_credits_user_id_local_date
                ON app.workout_completion_day_credits(user_id, local_date);
            ALTER TABLE app.workout_account_planning_states ENABLE ROW LEVEL SECURITY;
            ALTER TABLE app.workout_account_planning_states FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON app.workout_account_planning_states
                USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
            ALTER TABLE app.workout_completion_day_credits ENABLE ROW LEVEL SECURITY;
            ALTER TABLE app.workout_completion_day_credits FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON app.workout_completion_day_credits
                USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
            """);
        // No historical backfill: existing logs lack accepted calendar provenance.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("workout_completion_day_credits", "app");
        migrationBuilder.DropTable("workout_account_planning_states", "app");
    }
}
