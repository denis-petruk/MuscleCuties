using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261018025413_AddPhotoCleanupDispatchCursor")]
public sealed class AddPhotoCleanupDispatchCursor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_dispatch'
                           AND NOT rolcanlogin AND NOT rolsuper AND NOT rolbypassrls
                           AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication) THEN
                RAISE EXCEPTION 'musclecuties_cleanup_dispatch must remain a restricted NOLOGIN role';
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app')
               AND pg_has_role('musclecuties_app', 'musclecuties_cleanup_dispatch', 'MEMBER') THEN
                RAISE EXCEPTION 'musclecuties_app must not inherit photo cleanup dispatch privileges';
            END IF;
        END $$;
        CREATE TABLE app.photo_cleanup_dispatch_state (
            state_id boolean PRIMARY KEY DEFAULT TRUE CHECK (state_id),
            after_owner_id uuid NULL
        );
        INSERT INTO app.photo_cleanup_dispatch_state (state_id, after_owner_id) VALUES (TRUE, NULL);
        DO $$
        DECLARE had_create boolean;
        BEGIN
            IF current_user <> 'musclecuties_migrator'
               AND EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_migrator') THEN
                had_create := has_schema_privilege('musclecuties_migrator', 'app', 'CREATE');
                IF NOT had_create THEN
                    GRANT CREATE ON SCHEMA app TO musclecuties_migrator;
                END IF;
                ALTER TABLE app.photo_cleanup_dispatch_state OWNER TO musclecuties_migrator;
                IF NOT had_create THEN
                    REVOKE CREATE ON SCHEMA app FROM musclecuties_migrator;
                END IF;
            END IF;
        END $$;
        ALTER TABLE app.photo_cleanup_dispatch_state ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.photo_cleanup_dispatch_state FORCE ROW LEVEL SECURITY;
        CREATE POLICY cleanup_dispatch_cursor_access ON app.photo_cleanup_dispatch_state
            FOR ALL TO musclecuties_cleanup_dispatch USING (true) WITH CHECK (true);
        REVOKE ALL ON app.photo_cleanup_dispatch_state FROM PUBLIC;
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app') THEN
                EXECUTE 'REVOKE ALL ON app.photo_cleanup_dispatch_state FROM musclecuties_app';
            END IF;
        END $$;
        GRANT SELECT, UPDATE ON app.photo_cleanup_dispatch_state TO musclecuties_cleanup_dispatch;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP POLICY cleanup_dispatch_cursor_access ON app.photo_cleanup_dispatch_state;
        DROP TABLE app.photo_cleanup_dispatch_state;
        """);
}
