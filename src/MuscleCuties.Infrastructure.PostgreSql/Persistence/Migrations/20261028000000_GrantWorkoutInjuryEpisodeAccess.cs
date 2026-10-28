using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261028000000_GrantWorkoutInjuryEpisodeAccess")]
public sealed class GrantWorkoutInjuryEpisodeAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.workout_injury_episodes TO musclecuties_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.workout_injury_prescriptions TO musclecuties_app;
        GRANT SELECT, INSERT, DELETE ON app.workout_injury_prescription_links TO musclecuties_app;
        """);

    // Preserve any pre-existing deployment-specific grants on rollback.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
