using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261023160000_AddWorkoutExerciseReviewEvidenceV020")]
public partial class AddWorkoutExerciseReviewEvidenceV020 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "review_evidence_v020", schema: "catalog",
            table: "workout_exercise_definitions", type: "jsonb", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "review_evidence_v020", schema: "catalog",
            table: "workout_exercise_definitions");
}
