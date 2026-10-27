using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027234500_OwnReviewedTemplateVolumeFloors")]
public sealed class OwnReviewedTemplateVolumeFloors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        DECLARE
            floor_owner oid;
            lock_owner oid;
            target_role oid;
        BEGIN
            SELECT oid INTO target_role FROM pg_roles WHERE rolname = 'musclecuties_migrator';
            SELECT c.relowner INTO floor_owner FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'catalog' AND c.relname = 'workout_template_volume_floors' AND c.relkind = 'r';
            SELECT p.proowner INTO lock_owner FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname = 'app' AND p.proname = 'lock_workout_planning_catalog' AND p.pronargs = 0;
            IF target_role IS NULL OR floor_owner IS NULL OR lock_owner IS DISTINCT FROM target_role THEN
                RAISE EXCEPTION 'Expected workout floor table, migrator role and migrator-owned catalog lock function';
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app') AND
                pg_has_role('musclecuties_app', target_role, 'MEMBER') THEN
                RAISE EXCEPTION 'Application role must not inherit the catalog owner role';
            END IF;
            IF floor_owner <> target_role THEN
                IF NOT pg_has_role(current_user, floor_owner, 'MEMBER') OR
                   NOT pg_has_role(current_user, target_role, 'MEMBER') OR
                   NOT has_schema_privilege(target_role, 'catalog', 'CREATE') THEN
                    RAISE EXCEPTION 'Migration executor must be able to assume both table owner and migrator roles; migrator requires catalog CREATE for ownership transfer';
                END IF;
                ALTER TABLE catalog.workout_template_volume_floors OWNER TO musclecuties_migrator;
            END IF;
            REVOKE INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
                ON catalog.workout_template_volume_floors FROM musclecuties_app;
        END $$;
        """);

    // The former owner is deployment-specific; do not guess it during rollback.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
