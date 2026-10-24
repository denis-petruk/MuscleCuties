using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261023170000_AddWorkoutPlanRevisionSourceProposalIndexV02")]
public sealed class AddWorkoutPlanRevisionSourceProposalIndexV02 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateIndex(
            name: "IX_workout_plan_revisions_v02_user_id_source_proposal_id",
            schema: "app",
            table: "workout_plan_revisions_v02",
            columns: new[] { "user_id", "source_proposal_id" });

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropIndex(
            name: "IX_workout_plan_revisions_v02_user_id_source_proposal_id",
            schema: "app",
            table: "workout_plan_revisions_v02");
}
