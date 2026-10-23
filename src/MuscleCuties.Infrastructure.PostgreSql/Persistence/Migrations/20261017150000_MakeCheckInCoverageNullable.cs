using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261017150000_MakeCheckInCoverageNullable")]
public sealed class MakeCheckInCoverageNullable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE app.daily_readiness_logs
            ALTER COLUMN sleep_hours DROP NOT NULL,
            ALTER COLUMN sleep3d_avg DROP NOT NULL,
            ALTER COLUMN steps_yesterday DROP NOT NULL,
            ALTER COLUMN steps7d_avg DROP NOT NULL,
            ALTER COLUMN energy DROP NOT NULL,
            ALTER COLUMN pain DROP NOT NULL,
            ALTER COLUMN bloating DROP NOT NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM app.daily_readiness_logs
                WHERE sleep_hours IS NULL
                   OR sleep3d_avg IS NULL
                   OR steps_yesterday IS NULL
                   OR steps7d_avg IS NULL
                   OR energy IS NULL
                   OR pain IS NULL
                   OR bloating IS NULL
            ) THEN
                RAISE EXCEPTION 'Cannot roll back nullable check-in coverage while persisted check-ins contain unavailable observations; preserve the data or keep this migration applied.';
            END IF;
        END $$;

        ALTER TABLE app.daily_readiness_logs
            ALTER COLUMN sleep_hours SET NOT NULL,
            ALTER COLUMN sleep3d_avg SET NOT NULL,
            ALTER COLUMN steps_yesterday SET NOT NULL,
            ALTER COLUMN steps7d_avg SET NOT NULL,
            ALTER COLUMN energy SET NOT NULL,
            ALTER COLUMN pain SET NOT NULL,
            ALTER COLUMN bloating SET NOT NULL;
        """);
}
