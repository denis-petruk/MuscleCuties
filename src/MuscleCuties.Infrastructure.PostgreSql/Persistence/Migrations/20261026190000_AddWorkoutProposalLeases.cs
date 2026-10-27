using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261026190000_AddWorkoutProposalLeases")]
public sealed class AddWorkoutProposalLeases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("lease_id", "workout_plan_proposals_v02", schema: "app", nullable: true);
        migrationBuilder.AddColumn<long>("lease_fence", "workout_plan_proposals_v02", schema: "app", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<DateTime>("lease_expires_at_utc", "workout_plan_proposals_v02", schema: "app", type: "timestamp with time zone", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("lease_id", "workout_plan_proposals_v02", "app");
        migrationBuilder.DropColumn("lease_fence", "workout_plan_proposals_v02", "app");
        migrationBuilder.DropColumn("lease_expires_at_utc", "workout_plan_proposals_v02", "app");
    }
}
