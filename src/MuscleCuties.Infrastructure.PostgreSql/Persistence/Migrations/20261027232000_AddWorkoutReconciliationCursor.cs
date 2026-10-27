using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027232000_AddWorkoutReconciliationCursor")]
public sealed class AddWorkoutReconciliationCursor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE app.workout_reconciliation_cursor (
            singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
            after_owner_id uuid NULL
        );
        INSERT INTO app.workout_reconciliation_cursor (singleton) VALUES (true);
        REVOKE ALL ON app.workout_reconciliation_cursor FROM PUBLIC;
        GRANT SELECT, UPDATE ON app.workout_reconciliation_cursor TO musclecuties_workout_dispatch;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE app.workout_reconciliation_cursor;
        """);
}
