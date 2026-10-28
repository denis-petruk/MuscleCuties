using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

// Existing attribution values remain unchanged; actual versioned floor values remain mandatory.
[DbContext(typeof(PostgresDbContext))]
[Migration("20261028000000_RemoveTemplateFloorAttributionRequirement")]
public sealed class RemoveTemplateFloorAttributionRequirement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE catalog.workout_template_volume_floors
            DROP CONSTRAINT IF EXISTS workout_template_volume_floors_reviewer_check,
            DROP CONSTRAINT IF EXISTS workout_template_volume_floors_evidence_reference_check,
            DROP CONSTRAINT IF EXISTS workout_template_volume_floors_evidence_sha256_check,
            ALTER COLUMN reviewer DROP NOT NULL,
            ALTER COLUMN evidence_reference DROP NOT NULL,
            ALTER COLUMN evidence_sha256 DROP NOT NULL,
            ALTER COLUMN reviewed_on DROP NOT NULL;
        """);

    // Reinstating mandatory attribution would reject legitimate later rows; do not invent it.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
