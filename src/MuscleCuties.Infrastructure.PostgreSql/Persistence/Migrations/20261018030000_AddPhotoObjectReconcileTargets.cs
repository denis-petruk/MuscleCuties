using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261018030000_AddPhotoObjectReconcileTargets")]
public sealed class AddPhotoObjectReconcileTargets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_photo_reconcile_owner') THEN
                CREATE ROLE musclecuties_photo_reconcile_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_photo_reconcile') THEN
                CREATE ROLE musclecuties_photo_reconcile NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_photo_reconcile'
                AND (rolcanlogin OR rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication)) THEN
                RAISE EXCEPTION 'musclecuties_photo_reconcile must remain a restricted NOLOGIN role';
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app'
                AND pg_has_role('musclecuties_app', 'musclecuties_photo_reconcile', 'MEMBER')) THEN
                RAISE EXCEPTION 'musclecuties_app must not inherit photo reconciliation privileges';
            END IF;
        END $$;
        CREATE TABLE app.photo_object_reconcile_targets (
            id uuid PRIMARY KEY,
            target_key varchar(512) NOT NULL UNIQUE,
            not_before_utc timestamptz NOT NULL,
            created_at_utc timestamptz NOT NULL,
            lease_token uuid NULL,
            lease_generation bigint NOT NULL DEFAULT 0,
            lease_expires_at_utc timestamptz NULL,
            CONSTRAINT ck_photo_object_reconcile_targets_lease CHECK
                ((lease_token IS NULL AND lease_expires_at_utc IS NULL) OR (lease_token IS NOT NULL AND lease_expires_at_utc IS NOT NULL))
        );
        CREATE INDEX ix_photo_object_reconcile_targets_due
            ON app.photo_object_reconcile_targets (not_before_utc, id);
        GRANT CREATE ON SCHEMA app TO musclecuties_photo_reconcile_owner;
        DO $$ BEGIN
            ALTER TABLE app.photo_object_reconcile_targets OWNER TO musclecuties_photo_reconcile_owner;
        END $$;
        REVOKE CREATE ON SCHEMA app FROM musclecuties_photo_reconcile_owner;
        ALTER TABLE app.photo_object_reconcile_targets ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.photo_object_reconcile_targets FORCE ROW LEVEL SECURITY;
        CREATE POLICY photo_object_reconcile_maintenance ON app.photo_object_reconcile_targets
            FOR ALL TO musclecuties_photo_reconcile USING (true) WITH CHECK (true);
        REVOKE ALL ON app.photo_object_reconcile_targets FROM PUBLIC;
        GRANT USAGE ON SCHEMA app TO musclecuties_photo_reconcile;
        GRANT SELECT, INSERT, UPDATE ON app.photo_object_reconcile_targets TO musclecuties_photo_reconcile;
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_migrator') THEN
                GRANT musclecuties_photo_reconcile_owner TO musclecuties_migrator;
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app') THEN
                EXECUTE 'REVOKE ALL ON app.photo_object_reconcile_targets FROM musclecuties_app';
            END IF;
        END $$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM app.photo_object_reconcile_targets) THEN
                RAISE EXCEPTION 'Refusing rollback while recurring photo reconciliation targets remain';
            END IF;
        END $$;
        DROP POLICY photo_object_reconcile_maintenance ON app.photo_object_reconcile_targets;
        DROP TABLE app.photo_object_reconcile_targets;
        """);
}
