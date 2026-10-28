using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261028190000_AddWorkoutCycleReadinessOccurrences")]
public sealed class AddWorkoutCycleReadinessOccurrences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE app.workout_account_planning_states ADD COLUMN cycle_readiness_pending boolean NOT NULL DEFAULT false;
        ALTER TABLE app.workout_account_planning_states ADD COLUMN cycle_readiness_pain_review_date date NULL;
        CREATE TABLE app.workout_cycle_readiness_occurrences (
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
        CREATE INDEX ix_workout_cycle_readiness_occurrences_user_id ON app.workout_cycle_readiness_occurrences(user_id);
        CREATE UNIQUE INDEX ix_workout_cycle_readiness_occurrences_user_id_workout_day_id
            ON app.workout_cycle_readiness_occurrences(user_id, workout_day_id);
        ALTER TABLE app.workout_cycle_readiness_occurrences ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.workout_cycle_readiness_occurrences FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON app.workout_cycle_readiness_occurrences
            USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
            WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app') THEN
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.workout_cycle_readiness_occurrences TO musclecuties_app;
            END IF;
        END $$;
        GRANT SELECT (cycle_readiness_pending) ON app.workout_account_planning_states TO musclecuties_workout_discovery_owner;
        CREATE OR REPLACE FUNCTION app.discover_pending_workout_owners(p_after uuid, p_limit integer)
        RETURNS TABLE (owner_id uuid)
        LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, app, pg_temp AS $function$
            SELECT s.user_id FROM app.workout_account_planning_states AS s
            WHERE (s.promotion_pending OR s.safety_regeneration_pending OR s.cycle_readiness_pending)
              AND (p_after IS NULL OR s.user_id > p_after)
            ORDER BY s.user_id
            LIMIT LEAST(GREATEST(COALESCE(p_limit, 0), 0), 100)
        $function$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION app.discover_pending_workout_owners(p_after uuid, p_limit integer)
        RETURNS TABLE (owner_id uuid)
        LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, app, pg_temp AS $function$
            SELECT s.user_id FROM app.workout_account_planning_states AS s
            WHERE (s.promotion_pending OR s.safety_regeneration_pending)
              AND (p_after IS NULL OR s.user_id > p_after)
            ORDER BY s.user_id LIMIT LEAST(GREATEST(COALESCE(p_limit, 0), 0), 100)
        $function$;
        REVOKE SELECT (cycle_readiness_pending) ON app.workout_account_planning_states FROM musclecuties_workout_discovery_owner;
        DROP TABLE app.workout_cycle_readiness_occurrences;
        ALTER TABLE app.workout_account_planning_states DROP COLUMN cycle_readiness_pending;
        ALTER TABLE app.workout_account_planning_states DROP COLUMN cycle_readiness_pain_review_date;
        """);
}
