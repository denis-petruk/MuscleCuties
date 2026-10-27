using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027080000_AddUnplannedCompletionProvenance")]
public sealed class AddUnplannedCompletionProvenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE app.workout_session_logs ADD COLUMN unplanned_activity_type integer NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN unplanned_completion_local_date date NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN unplanned_completion_time_zone_id text NULL;
        ALTER TABLE app.workout_session_logs ADD COLUMN unplanned_completion_time_zone_version bigint NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE app.workout_session_logs DROP COLUMN unplanned_completion_time_zone_version;
        ALTER TABLE app.workout_session_logs DROP COLUMN unplanned_completion_time_zone_id;
        ALTER TABLE app.workout_session_logs DROP COLUMN unplanned_completion_local_date;
        ALTER TABLE app.workout_session_logs DROP COLUMN unplanned_activity_type;
        """);
}
