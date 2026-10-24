using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261024200000_AddWorkoutExerciseExclusionsV010")]
public sealed class AddWorkoutExerciseExclusionsV010 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "excluded_strength_exercise_ids_json",
            schema: "app",
            table: "user_profiles",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");

        migrationBuilder.AddCheckConstraint(
            name: "ck_user_profiles_excluded_strength_exercise_ids_array",
            schema: "app",
            table: "user_profiles",
            sql: "jsonb_typeof(excluded_strength_exercise_ids_json) = 'array'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_user_profiles_excluded_strength_exercise_ids_array",
            schema: "app",
            table: "user_profiles");

        migrationBuilder.DropColumn(
            name: "excluded_strength_exercise_ids_json",
            schema: "app",
            table: "user_profiles");
    }
}
