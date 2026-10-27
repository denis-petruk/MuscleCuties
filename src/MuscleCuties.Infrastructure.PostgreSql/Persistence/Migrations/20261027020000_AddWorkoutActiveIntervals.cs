using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027020000_AddWorkoutActiveIntervals")]
public sealed class AddWorkoutActiveIntervals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("confirmed_active_seconds", "workout_session_logs", schema: "app", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<Guid>("timing_interval_id", "workout_session_logs", schema: "app", nullable: true);
        migrationBuilder.AddColumn<DateTime>("timing_interval_started_at_utc", "workout_session_logs", schema: "app", nullable: true);
        migrationBuilder.AddColumn<DateTime>("timing_interval_last_confirmed_at_utc", "workout_session_logs", schema: "app", nullable: true);
        migrationBuilder.AddColumn<int>("timing_interval_confirmed_seconds", "workout_session_logs", schema: "app", nullable: false, defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("confirmed_active_seconds", "workout_session_logs", "app");
        migrationBuilder.DropColumn("timing_interval_id", "workout_session_logs", "app");
        migrationBuilder.DropColumn("timing_interval_started_at_utc", "workout_session_logs", "app");
        migrationBuilder.DropColumn("timing_interval_last_confirmed_at_utc", "workout_session_logs", "app");
        migrationBuilder.DropColumn("timing_interval_confirmed_seconds", "workout_session_logs", "app");
    }
}
