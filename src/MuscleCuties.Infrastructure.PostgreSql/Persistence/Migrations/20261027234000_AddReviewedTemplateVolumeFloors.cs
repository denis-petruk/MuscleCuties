using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027234000_AddReviewedTemplateVolumeFloors")]
public sealed class AddReviewedTemplateVolumeFloors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE catalog.workout_template_volume_floors (
            template_id text PRIMARY KEY CHECK (length(trim(template_id)) > 0),
            template_version text NOT NULL CHECK (length(trim(template_version)) > 0),
            minimum_working_sets integer NOT NULL CHECK (minimum_working_sets > 0),
            minimum_exercises integer NOT NULL CHECK (minimum_exercises > 0),
            slot_floors jsonb NOT NULL CHECK (jsonb_typeof(slot_floors) = 'object'),
            reviewer text NOT NULL CHECK (length(trim(reviewer)) > 0),
            evidence_reference text NOT NULL CHECK (length(trim(evidence_reference)) > 0),
            evidence_sha256 text NOT NULL CHECK (evidence_sha256 ~ '^[0-9a-fA-F]{64}$'),
            reviewed_on date NOT NULL
        );
        REVOKE ALL ON catalog.workout_template_volume_floors FROM PUBLIC;
        GRANT SELECT ON catalog.workout_template_volume_floors TO musclecuties_app;
        CREATE OR REPLACE FUNCTION app.lock_workout_planning_catalog() RETURNS void
        LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
        BEGIN
            LOCK TABLE catalog.workout_exercise_definitions, catalog.exercise_muscle_contributions,
                catalog.workout_muscle_groups, catalog.goal_tier_weights,
                catalog.workout_template_volume_floors IN SHARE MODE;
        END;
        $function$;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION app.lock_workout_planning_catalog() RETURNS void
        LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
        BEGIN
            LOCK TABLE catalog.workout_exercise_definitions, catalog.exercise_muscle_contributions,
                catalog.workout_muscle_groups, catalog.goal_tier_weights IN SHARE MODE;
        END;
        $function$;
        DROP TABLE catalog.workout_template_volume_floors;
        """);
}
