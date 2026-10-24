using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261024190000_AddRequestedActivityFrequenciesV010")]
public sealed class AddRequestedActivityFrequenciesV010 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "requested_activity_sessions_per_week_json",
            schema: "app",
            table: "user_profiles",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'{}'::jsonb");

        migrationBuilder.AddCheckConstraint(
            name: "ck_user_profiles_requested_activity_sessions_per_week_object",
            schema: "app",
            table: "user_profiles",
            sql: "jsonb_typeof(requested_activity_sessions_per_week_json) = 'object'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_user_profiles_requested_activity_sessions_per_week_object",
            schema: "app",
            table: "user_profiles");

        migrationBuilder.DropColumn(
            name: "requested_activity_sessions_per_week_json",
            schema: "app",
            table: "user_profiles");
    }
}
