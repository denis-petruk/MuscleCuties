using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027230000_AddWorkoutInjuryExerciseProvenance")]
public sealed class AddWorkoutInjuryExerciseProvenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("injury_original_exercise_id", "workout_day_exercises", schema: "app", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("injury_restoration_required_from_date", "workout_day_exercises", schema: "app", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("injury_original_exercise_id", "workout_day_exercises", "app");
        migrationBuilder.DropColumn("injury_restoration_required_from_date", "workout_day_exercises", "app");
    }
}
