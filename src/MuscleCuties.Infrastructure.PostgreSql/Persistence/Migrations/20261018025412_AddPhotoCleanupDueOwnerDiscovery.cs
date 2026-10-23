using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261018025412_AddPhotoCleanupDueOwnerDiscovery")]
public sealed class AddPhotoCleanupDueOwnerDiscovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_discovery_owner') THEN
                CREATE ROLE musclecuties_cleanup_discovery_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_dispatch') THEN
                CREATE ROLE musclecuties_cleanup_dispatch NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_discovery_owner'
                       AND (rolcanlogin OR rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication)) THEN
                RAISE EXCEPTION 'musclecuties_cleanup_discovery_owner must remain a restricted NOLOGIN role';
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_cleanup_dispatch'
                       AND (rolcanlogin OR rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication)) THEN
                RAISE EXCEPTION 'musclecuties_cleanup_dispatch must remain a restricted NOLOGIN role';
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app')
               AND (pg_has_role('musclecuties_app', 'musclecuties_cleanup_dispatch', 'MEMBER')
                    OR pg_has_role('musclecuties_app', 'musclecuties_cleanup_discovery_owner', 'MEMBER')) THEN
                RAISE EXCEPTION 'musclecuties_app must not inherit photo cleanup discovery privileges';
            END IF;
        END $$;

        GRANT USAGE ON SCHEMA app TO musclecuties_cleanup_discovery_owner;
        GRANT USAGE ON SCHEMA app TO musclecuties_cleanup_dispatch;
        GRANT SELECT (user_id, state, not_before_utc, lease_expires_at_utc)
            ON app.photo_object_cleanups TO musclecuties_cleanup_discovery_owner;
        CREATE POLICY cleanup_due_owner_discovery ON app.photo_object_cleanups
            FOR SELECT TO musclecuties_cleanup_discovery_owner
            USING (true);

        CREATE FUNCTION app.discover_due_photo_cleanup_owners(
            p_now_utc timestamptz,
            p_after_owner_id uuid,
            p_limit integer)
        RETURNS TABLE (owner_id uuid)
        LANGUAGE sql
        STABLE
        SECURITY DEFINER
        SET search_path = pg_catalog, app, pg_temp
        AS $function$
            WITH due_owners AS (
                SELECT DISTINCT c.user_id
                FROM app.photo_object_cleanups AS c
                WHERE c.not_before_utc <= p_now_utc
                  AND (c.state IN ('Pending', 'Retryable')
                       OR (c.state = 'Leased' AND c.lease_expires_at_utc <= p_now_utc))
            ),
            ordered AS (
                SELECT d.user_id, 0 AS segment
                FROM due_owners AS d
                WHERE p_after_owner_id IS NULL OR d.user_id > p_after_owner_id
                UNION ALL
                SELECT d.user_id, 1 AS segment
                FROM due_owners AS d
                WHERE p_after_owner_id IS NOT NULL AND d.user_id <= p_after_owner_id
            )
            SELECT o.user_id
            FROM ordered AS o
            ORDER BY o.segment, o.user_id
            LIMIT LEAST(GREATEST(COALESCE(p_limit, 0), 0), 100)
        $function$;

        REVOKE ALL ON FUNCTION app.discover_due_photo_cleanup_owners(timestamptz, uuid, integer) FROM PUBLIC;
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app') THEN
                EXECUTE 'REVOKE ALL ON FUNCTION app.discover_due_photo_cleanup_owners(timestamptz, uuid, integer) FROM musclecuties_app';
            END IF;
        END $$;
        GRANT EXECUTE ON FUNCTION app.discover_due_photo_cleanup_owners(timestamptz, uuid, integer)
            TO musclecuties_cleanup_dispatch;
        GRANT CREATE ON SCHEMA app TO musclecuties_cleanup_discovery_owner;
        ALTER FUNCTION app.discover_due_photo_cleanup_owners(timestamptz, uuid, integer)
            OWNER TO musclecuties_cleanup_discovery_owner;
        REVOKE CREATE ON SCHEMA app FROM musclecuties_cleanup_discovery_owner;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP FUNCTION app.discover_due_photo_cleanup_owners(timestamptz, uuid, integer);
        DROP POLICY cleanup_due_owner_discovery ON app.photo_object_cleanups;
        REVOKE SELECT (user_id, state, not_before_utc, lease_expires_at_utc)
            ON app.photo_object_cleanups FROM musclecuties_cleanup_discovery_owner;
        REVOKE USAGE ON SCHEMA app FROM musclecuties_cleanup_discovery_owner;
        REVOKE USAGE ON SCHEMA app FROM musclecuties_cleanup_dispatch;
        """);
}
