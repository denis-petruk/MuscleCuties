using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261028110000_AddActivityOccurrencePrescriptions")]
public sealed class AddActivityOccurrencePrescriptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>("planned_activity_prescription_json", "workout_days",
            type: "jsonb", schema: "app", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn("planned_activity_prescription_json", "workout_days", "app");
}
