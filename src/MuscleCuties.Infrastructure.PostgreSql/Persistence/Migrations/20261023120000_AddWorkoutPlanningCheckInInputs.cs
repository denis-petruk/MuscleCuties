using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261023120000_AddWorkoutPlanningCheckInInputs")]
public partial class AddWorkoutPlanningCheckInInputs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "planning_sleep_quality", schema: "app", table: "daily_readiness_logs", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>(name: "planning_soreness", schema: "app", table: "daily_readiness_logs", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>(name: "planning_stress", schema: "app", table: "daily_readiness_logs", type: "integer", nullable: true);
        migrationBuilder.AddColumn<string>(name: "planning_time_zone_id", schema: "app", table: "daily_readiness_logs", type: "text", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "planning_recorded_at_utc", schema: "app", table: "daily_readiness_logs", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<long>(name: "planning_revision", schema: "app", table: "daily_readiness_logs", type: "bigint", nullable: false, defaultValue: 0L);

        migrationBuilder.AddCheckConstraint(
            name: "ck_daily_readiness_logs_planning_scales",
            schema: "app",
            table: "daily_readiness_logs",
            sql: "(planning_sleep_quality IS NULL OR planning_sleep_quality BETWEEN 1 AND 5) AND (planning_soreness IS NULL OR planning_soreness BETWEEN 1 AND 5) AND (planning_stress IS NULL OR planning_stress BETWEEN 1 AND 5)");
        migrationBuilder.AddCheckConstraint(
            name: "ck_daily_readiness_logs_planning_revision",
            schema: "app",
            table: "daily_readiness_logs",
            sql: "planning_revision >= 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_daily_readiness_logs_planning_scales",
            schema: "app",
            table: "daily_readiness_logs");
        migrationBuilder.DropCheckConstraint(
            name: "ck_daily_readiness_logs_planning_revision",
            schema: "app",
            table: "daily_readiness_logs");

        migrationBuilder.DropColumn(name: "planning_sleep_quality", schema: "app", table: "daily_readiness_logs");
        migrationBuilder.DropColumn(name: "planning_soreness", schema: "app", table: "daily_readiness_logs");
        migrationBuilder.DropColumn(name: "planning_stress", schema: "app", table: "daily_readiness_logs");
        migrationBuilder.DropColumn(name: "planning_time_zone_id", schema: "app", table: "daily_readiness_logs");
        migrationBuilder.DropColumn(name: "planning_recorded_at_utc", schema: "app", table: "daily_readiness_logs");
        migrationBuilder.DropColumn(name: "planning_revision", schema: "app", table: "daily_readiness_logs");
    }
}
