using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027060000_AddWorkoutExercisePlanningLineage")]
public sealed class AddWorkoutExercisePlanningLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE app.workout_day_exercises ADD COLUMN planning_slot_id text NULL;
        ALTER TABLE app.workout_day_exercises ADD COLUMN origin_workout_day_exercise_id uuid NULL;
        ALTER TABLE app.workout_injury_prescriptions ADD COLUMN original_planning_slot_id text NULL;
        ALTER TABLE app.workout_injury_prescriptions ADD COLUMN original_origin_workout_day_exercise_id uuid NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE app.workout_injury_prescriptions DROP COLUMN original_origin_workout_day_exercise_id;
        ALTER TABLE app.workout_injury_prescriptions DROP COLUMN original_planning_slot_id;
        ALTER TABLE app.workout_day_exercises DROP COLUMN origin_workout_day_exercise_id;
        ALTER TABLE app.workout_day_exercises DROP COLUMN planning_slot_id;
        """);
}
