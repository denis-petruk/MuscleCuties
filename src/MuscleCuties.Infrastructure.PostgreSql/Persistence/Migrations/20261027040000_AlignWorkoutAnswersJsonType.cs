using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027040000_AlignWorkoutAnswersJsonType")]
public sealed class AlignWorkoutAnswersJsonType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE app.workout_account_planning_states ALTER COLUMN answers_json TYPE jsonb USING answers_json::jsonb");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE app.workout_account_planning_states ALTER COLUMN answers_json TYPE text USING answers_json::text");
}
