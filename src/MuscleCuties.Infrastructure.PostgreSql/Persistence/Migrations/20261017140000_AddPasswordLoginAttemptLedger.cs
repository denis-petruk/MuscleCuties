using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261017140000_AddPasswordLoginAttemptLedger")]
public sealed class AddPasswordLoginAttemptLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE app.password_login_attempts (
            id uuid PRIMARY KEY,
            scope_key character varying(64) NOT NULL,
            attempted_at_utc timestamp with time zone NOT NULL
        );
        CREATE INDEX ix_password_login_attempts_scope_time
            ON app.password_login_attempts(scope_key, attempted_at_utc DESC);
        CREATE INDEX ix_password_login_attempts_cleanup
            ON app.password_login_attempts(attempted_at_utc);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS app.password_login_attempts;
        """);
}
