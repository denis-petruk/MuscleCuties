using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027235000_AddWorkoutPrescriptionSlotIdentity")]
public sealed class AddWorkoutPrescriptionSlotIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("planning_template_id", "workout_day_exercises", schema: "app", nullable: true);
        migrationBuilder.AddColumn<string>("planning_template_version", "workout_day_exercises", schema: "app", nullable: true);
        migrationBuilder.AddColumn<string>("planning_slot_id", "workout_day_exercises", schema: "app", nullable: true);
        migrationBuilder.AddColumn<int>("planning_session_index", "workout_day_exercises", schema: "app", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("planning_template_id", "workout_day_exercises", "app");
        migrationBuilder.DropColumn("planning_template_version", "workout_day_exercises", "app");
        migrationBuilder.DropColumn("planning_slot_id", "workout_day_exercises", "app");
        migrationBuilder.DropColumn("planning_session_index", "workout_day_exercises", "app");
    }
}
