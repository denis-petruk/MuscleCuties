using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261027231000_AddWorkoutReconciliationDiscovery")]
public sealed class AddWorkoutReconciliationDiscovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_workout_discovery_owner') THEN
                CREATE ROLE musclecuties_workout_discovery_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_workout_dispatch') THEN
                CREATE ROLE musclecuties_workout_dispatch NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname IN ('musclecuties_workout_discovery_owner', 'musclecuties_workout_dispatch')
                AND (rolcanlogin OR rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication)) THEN
                RAISE EXCEPTION 'Workout discovery roles must remain restricted NOLOGIN roles';
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app') AND
                (pg_has_role('musclecuties_app', 'musclecuties_workout_discovery_owner', 'MEMBER') OR
                 pg_has_role('musclecuties_app', 'musclecuties_workout_dispatch', 'MEMBER')) THEN
                RAISE EXCEPTION 'musclecuties_app must not inherit workout discovery privileges';
            END IF;
            IF EXISTS (SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid = m.member
                       WHERE r.rolname IN ('musclecuties_workout_discovery_owner', 'musclecuties_workout_dispatch')) OR
               EXISTS (SELECT 1 FROM pg_auth_members m
                       JOIN pg_roles granted ON granted.oid = m.roleid
                       JOIN pg_roles member ON member.oid = m.member
                       WHERE granted.rolname = 'musclecuties_workout_discovery_owner'
                         AND member.rolname <> current_user) THEN
                RAISE EXCEPTION 'Workout discovery roles have unexpected role memberships';
            END IF;
        END $$;
        GRANT USAGE ON SCHEMA app TO musclecuties_workout_discovery_owner, musclecuties_workout_dispatch;
        GRANT SELECT (user_id, promotion_pending, safety_regeneration_pending)
            ON app.workout_account_planning_states TO musclecuties_workout_discovery_owner;
        CREATE POLICY workout_pending_discovery ON app.workout_account_planning_states
            FOR SELECT TO musclecuties_workout_discovery_owner USING (true);
        CREATE FUNCTION app.discover_pending_workout_owners(p_after uuid, p_limit integer)
        RETURNS TABLE (owner_id uuid)
        LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, app, pg_temp AS $function$
            SELECT s.user_id FROM app.workout_account_planning_states AS s
            WHERE (s.promotion_pending OR s.safety_regeneration_pending)
              AND (p_after IS NULL OR s.user_id > p_after)
            ORDER BY s.user_id
            LIMIT LEAST(GREATEST(COALESCE(p_limit, 0), 0), 100)
        $function$;
        REVOKE ALL ON FUNCTION app.discover_pending_workout_owners(uuid, integer) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION app.discover_pending_workout_owners(uuid, integer) TO musclecuties_workout_dispatch;
        GRANT CREATE ON SCHEMA app TO musclecuties_workout_discovery_owner;
        ALTER FUNCTION app.discover_pending_workout_owners(uuid, integer) OWNER TO musclecuties_workout_discovery_owner;
        REVOKE CREATE ON SCHEMA app FROM musclecuties_workout_discovery_owner;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP FUNCTION app.discover_pending_workout_owners(uuid, integer);
        DROP POLICY workout_pending_discovery ON app.workout_account_planning_states;
        REVOKE SELECT (user_id, promotion_pending, safety_regeneration_pending)
            ON app.workout_account_planning_states FROM musclecuties_workout_discovery_owner;
        REVOKE USAGE ON SCHEMA app FROM musclecuties_workout_discovery_owner, musclecuties_workout_dispatch;
        """);
}
