using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261026220000_AddWorkoutSafetySuppression")]
public sealed class AddWorkoutSafetySuppression : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("is_removed_by_safety", "workout_days", schema: "app", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<Guid>("safety_injury_id", "workout_days", schema: "app", nullable: true);
        migrationBuilder.AddColumn<int>("safety_activity_type_id", "workout_days", schema: "app", nullable: true);
        migrationBuilder.AddColumn<int>("activity_type_id", "workout_days", schema: "app", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("cleared_date", "workout_injury_logs", schema: "app", nullable: true);
        migrationBuilder.AddColumn<Guid>("planned_workout_day_id", "workout_session_logs", schema: "app", nullable: true);
        migrationBuilder.AddColumn<string>("planned_time_zone_id", "workout_session_logs", schema: "app", nullable: true);
        migrationBuilder.AddColumn<long>("planned_time_zone_version", "workout_session_logs", schema: "app", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("planned_local_date", "workout_session_logs", schema: "app", nullable: true);
        migrationBuilder.AddColumn<bool>("safety_regeneration_pending", "workout_account_planning_states", schema: "app", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("is_removed_by_safety", "workout_days", "app");
        migrationBuilder.DropColumn("safety_injury_id", "workout_days", "app");
        migrationBuilder.DropColumn("safety_activity_type_id", "workout_days", "app");
        migrationBuilder.DropColumn("activity_type_id", "workout_days", "app");
        migrationBuilder.DropColumn("cleared_date", "workout_injury_logs", "app");
        migrationBuilder.DropColumn("planned_workout_day_id", "workout_session_logs", "app");
        migrationBuilder.DropColumn("planned_time_zone_id", "workout_session_logs", "app");
        migrationBuilder.DropColumn("planned_time_zone_version", "workout_session_logs", "app");
        migrationBuilder.DropColumn("planned_local_date", "workout_session_logs", "app");
        migrationBuilder.DropColumn("safety_regeneration_pending", "workout_account_planning_states", "app");
    }
}
