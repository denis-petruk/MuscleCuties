using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261023160000_AddReadinessOptionalWorkMarkers")]
public sealed class AddReadinessOptionalWorkMarkers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "is_optional", schema: "app", table: "workout_days",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "is_easier_alternative", schema: "app", table: "workout_days",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "is_removed_by_readiness", schema: "app", table: "workout_days",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>(name: "optional_sets", schema: "app", table: "workout_day_exercises",
            type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<bool>(name: "is_optional", schema: "app", table: "workout_day_exercises",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "is_removed_by_readiness", schema: "app", table: "workout_day_exercises",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddCheckConstraint(name: "ck_workout_day_exercises_optional_sets",
            schema: "app", table: "workout_day_exercises",
            sql: "optional_sets >= 0 AND (optional_sets = 0 OR optional_sets < sets)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "ck_workout_day_exercises_optional_sets",
            schema: "app", table: "workout_day_exercises");
        migrationBuilder.DropColumn(name: "is_optional", schema: "app", table: "workout_days");
        migrationBuilder.DropColumn(name: "is_easier_alternative", schema: "app", table: "workout_days");
        migrationBuilder.DropColumn(name: "is_removed_by_readiness", schema: "app", table: "workout_days");
        migrationBuilder.DropColumn(name: "optional_sets", schema: "app", table: "workout_day_exercises");
        migrationBuilder.DropColumn(name: "is_optional", schema: "app", table: "workout_day_exercises");
        migrationBuilder.DropColumn(name: "is_removed_by_readiness", schema: "app", table: "workout_day_exercises");
    }
}
