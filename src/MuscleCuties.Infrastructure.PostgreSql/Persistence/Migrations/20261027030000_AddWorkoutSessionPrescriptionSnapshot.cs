using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027030000_AddWorkoutSessionPrescriptionSnapshot")]
public sealed class AddWorkoutSessionPrescriptionSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>("planned_prescription_snapshot_json", "workout_session_logs", type: "jsonb", schema: "app", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn("planned_prescription_snapshot_json", "workout_session_logs", "app");
}
