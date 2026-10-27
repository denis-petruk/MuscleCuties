using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261026230000_LockWorkoutPlanningCatalog")]
public sealed class LockWorkoutPlanningCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION app.lock_workout_planning_catalog() RETURNS void
        LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog
        AS $function$
        BEGIN
            LOCK TABLE catalog.workout_exercise_definitions, catalog.exercise_muscle_contributions,
                catalog.workout_muscle_groups, catalog.goal_tier_weights IN SHARE MODE;
        END;
        $function$;
        ALTER FUNCTION app.lock_workout_planning_catalog() OWNER TO musclecuties_migrator;
        REVOKE ALL ON FUNCTION app.lock_workout_planning_catalog() FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION app.lock_workout_planning_catalog() TO musclecuties_app;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP FUNCTION app.lock_workout_planning_catalog();");
}
