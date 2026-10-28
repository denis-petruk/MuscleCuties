using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261028010000_AddCanonicalDeloadState")]
public sealed class AddCanonicalDeloadState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("target_repetitions_in_reserve", "workout_day_exercises", schema: "app", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("deload_evidence_through_exclusive", "workout_account_planning_states", schema: "app", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("deload_start_date", "workout_account_planning_states", schema: "app", nullable: true);
        migrationBuilder.AddColumn<DateOnly>("deload_end_exclusive", "workout_account_planning_states", schema: "app", nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("target_repetitions_in_reserve", "workout_day_exercises", "app");
        migrationBuilder.DropColumn("deload_evidence_through_exclusive", "workout_account_planning_states", "app");
        migrationBuilder.DropColumn("deload_start_date", "workout_account_planning_states", "app");
        migrationBuilder.DropColumn("deload_end_exclusive", "workout_account_planning_states", "app");
    }
}
