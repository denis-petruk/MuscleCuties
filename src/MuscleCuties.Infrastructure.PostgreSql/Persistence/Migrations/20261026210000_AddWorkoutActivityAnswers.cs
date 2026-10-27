using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261026210000_AddWorkoutActivityAnswers")]
public sealed class AddWorkoutActivityAnswers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>("answer_version", "workout_account_planning_states", schema: "app", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<string>("answers_json", "workout_account_planning_states", schema: "app", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("answer_version", "workout_account_planning_states", "app");
        migrationBuilder.DropColumn("answers_json", "workout_account_planning_states", "app");
    }
}
