using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261023140000_AddWorkoutExerciseEligibilityMetadataV020")]
public partial class AddWorkoutExerciseEligibilityMetadataV020 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "training_catalog_version_v020", schema: "catalog",
            table: "workout_exercise_definitions", type: "character varying(32)", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<string>(name: "supported_experiences_v020", schema: "catalog",
            table: "workout_exercise_definitions", type: "jsonb", nullable: true);
        migrationBuilder.AddColumn<string>(name: "demand_tags_v020", schema: "catalog",
            table: "workout_exercise_definitions", type: "jsonb", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "demand_tags_reviewed_v020", schema: "catalog",
            table: "workout_exercise_definitions", type: "boolean", nullable: true);
        migrationBuilder.AddColumn<string>(name: "restriction_conflict_tags_v020", schema: "catalog",
            table: "workout_exercise_definitions", type: "jsonb", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "restriction_mappings_reviewed_v020", schema: "catalog",
            table: "workout_exercise_definitions", type: "boolean", nullable: true);
        migrationBuilder.AddColumn<string>(name: "supported_timing_methods_v020", schema: "catalog",
            table: "workout_exercise_definitions", type: "jsonb", nullable: true);
        migrationBuilder.AddColumn<string>(name: "reviewed_substitute_exercise_ids_v020", schema: "catalog",
            table: "workout_exercise_definitions", type: "jsonb", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "training_catalog_version_v020", schema: "catalog",
            table: "workout_exercise_definitions");
        migrationBuilder.DropColumn(name: "supported_experiences_v020", schema: "catalog",
            table: "workout_exercise_definitions");
        migrationBuilder.DropColumn(name: "demand_tags_v020", schema: "catalog",
            table: "workout_exercise_definitions");
        migrationBuilder.DropColumn(name: "demand_tags_reviewed_v020", schema: "catalog",
            table: "workout_exercise_definitions");
        migrationBuilder.DropColumn(name: "restriction_conflict_tags_v020", schema: "catalog",
            table: "workout_exercise_definitions");
        migrationBuilder.DropColumn(name: "restriction_mappings_reviewed_v020", schema: "catalog",
            table: "workout_exercise_definitions");
        migrationBuilder.DropColumn(name: "supported_timing_methods_v020", schema: "catalog",
            table: "workout_exercise_definitions");
        migrationBuilder.DropColumn(name: "reviewed_substitute_exercise_ids_v020", schema: "catalog",
            table: "workout_exercise_definitions");
    }
}
