using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261024140000_AddWorkoutPhaseAdjustmentStateV010")]
public sealed class AddWorkoutPhaseAdjustmentStateV010 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_removed_by_user_adjustment",
            schema: "app",
            table: "workout_days",
            type: "boolean",
            nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<decimal>(
            name: "planned_main_duration_minutes",
            schema: "app",
            table: "workout_days",
            type: "numeric",
            nullable: true);
        migrationBuilder.AddCheckConstraint(
            name: "ck_workout_days_planned_main_duration_nonnegative",
            schema: "app",
            table: "workout_days",
            sql: "planned_main_duration_minutes IS NULL OR planned_main_duration_minutes >= 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_workout_days_planned_main_duration_nonnegative",
            schema: "app",
            table: "workout_days");
        migrationBuilder.DropColumn(
            name: "is_removed_by_user_adjustment",
            schema: "app",
            table: "workout_days");
        migrationBuilder.DropColumn(
            name: "planned_main_duration_minutes",
            schema: "app",
            table: "workout_days");
    }
}
