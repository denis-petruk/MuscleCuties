using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261018030500_AddAccountPhotoPurgeStageBridge")]
public sealed class AddAccountPhotoPurgeStageBridge : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        BEGIN
            IF current_user = 'musclecuties_app' THEN
                RAISE EXCEPTION 'Migrations must not be executed as musclecuties_app';
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_photo_purge_stage_owner') THEN
                CREATE ROLE musclecuties_photo_purge_stage_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app') THEN
                CREATE ROLE musclecuties_app NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_photo_purge_stage_owner'
                       AND (rolcanlogin OR rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication)) THEN
                RAISE EXCEPTION 'musclecuties_photo_purge_stage_owner must remain a restricted NOLOGIN role';
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app'
                       AND (rolsuper OR rolbypassrls
                            OR pg_has_role('musclecuties_app', 'musclecuties_photo_purge_stage_owner', 'MEMBER')
                            OR pg_has_role('musclecuties_app', 'musclecuties_photo_reconcile', 'MEMBER')
                            OR pg_has_role('musclecuties_app', 'musclecuties_photo_reconcile_owner', 'MEMBER'))) THEN
                RAISE EXCEPTION 'musclecuties_app must not have superuser, BYPASSRLS, or reconciliation role membership';
            END IF;
            IF NOT pg_has_role(current_user, 'musclecuties_photo_purge_stage_owner', 'MEMBER') THEN
                EXECUTE format('GRANT musclecuties_photo_purge_stage_owner TO %I', current_user);
            END IF;
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_migrator') THEN
                GRANT musclecuties_photo_purge_stage_owner TO musclecuties_migrator;
            END IF;
        END $$;

        GRANT USAGE ON SCHEMA app TO musclecuties_photo_purge_stage_owner;
        GRANT USAGE ON SCHEMA app TO musclecuties_app;
        REVOKE ALL ON app.photo_object_reconcile_targets FROM PUBLIC;
        REVOKE ALL ON app.photo_object_reconcile_targets FROM musclecuties_app;
        GRANT SELECT (target_key, not_before_utc),
              INSERT (id, target_key, not_before_utc, created_at_utc),
              UPDATE (not_before_utc)
            ON app.photo_object_reconcile_targets
            TO musclecuties_photo_purge_stage_owner;

        CREATE POLICY photo_object_reconcile_stage_purge ON app.photo_object_reconcile_targets
            FOR ALL TO musclecuties_photo_purge_stage_owner
            USING (true)
            WITH CHECK (true);

        CREATE POLICY photo_object_reconcile_owner_access ON app.photo_object_reconcile_targets
            FOR ALL TO musclecuties_photo_reconcile_owner
            USING (true)
            WITH CHECK (true);

        CREATE FUNCTION app.stage_account_photo_purge(
            p_user_id uuid,
            p_target_key text,
            p_not_before_utc timestamptz)
        RETURNS void
        LANGUAGE plpgsql
        VOLATILE
        SECURITY DEFINER
        SET search_path = pg_catalog, app, pg_temp
        AS $function$
        DECLARE
            v_raw_tenant text;
            v_tenant_user_id uuid;
            v_photo_segment text;
        BEGIN
            v_raw_tenant := pg_catalog.btrim(COALESCE(pg_catalog.current_setting('app.user_id', true), ''));
            IF v_raw_tenant = '' THEN
                RAISE EXCEPTION 'Account photo purge staging requires an active tenant context (app.user_id).'
                    USING ERRCODE = '42501';
            END IF;

            BEGIN
                v_tenant_user_id := v_raw_tenant::uuid;
            EXCEPTION WHEN invalid_text_representation THEN
                RAISE EXCEPTION 'Account photo purge staging requires a valid UUID tenant context (app.user_id).'
                    USING ERRCODE = '42501';
            END;

            IF p_user_id IS NULL
               OR p_user_id = '00000000-0000-0000-0000-000000000000'::uuid
               OR v_tenant_user_id = '00000000-0000-0000-0000-000000000000'::uuid
               OR v_tenant_user_id IS DISTINCT FROM p_user_id THEN
                RAISE EXCEPTION 'Account photo purge staging owner does not match the active tenant context.'
                    USING ERRCODE = '42501';
            END IF;

            IF p_not_before_utc IS NULL THEN
                RAISE EXCEPTION 'Account photo purge staging requires a non-null not_before_utc timestamp.'
                    USING ERRCODE = '22004';
            END IF;

            v_photo_segment := CASE
                WHEN p_target_key IS NOT NULL AND pg_catalog.char_length(p_target_key) >= 32
                    THEN pg_catalog.right(p_target_key, 32)
                ELSE ''
            END;

            IF p_target_key IS NULL
               OR pg_catalog.char_length(p_target_key) > 512
               OR v_photo_segment !~ '^[0-9a-f]{32}$'
               OR v_photo_segment = '00000000000000000000000000000000'
               OR (p_target_key <> ('photos/' || (p_user_id::text) || '/' || v_photo_segment)
                   AND p_target_key <> ('published-photos/' || (p_user_id::text) || '/' || v_photo_segment)) THEN
                RAISE EXCEPTION 'Account photo purge target key must be a canonical upload or published key for the authenticated owner.'
                    USING ERRCODE = '22023';
            END IF;

            INSERT INTO app.photo_object_reconcile_targets (
                id,
                target_key,
                not_before_utc,
                created_at_utc
            )
            VALUES (
                pg_catalog.gen_random_uuid(),
                p_target_key,
                p_not_before_utc,
                pg_catalog.clock_timestamp()
            )
            ON CONFLICT (target_key) DO UPDATE
            SET not_before_utc = GREATEST(
                app.photo_object_reconcile_targets.not_before_utc,
                EXCLUDED.not_before_utc
            );
        END;
        $function$;

        GRANT CREATE ON SCHEMA app TO musclecuties_photo_purge_stage_owner;
        ALTER FUNCTION app.stage_account_photo_purge(uuid, text, timestamptz)
            OWNER TO musclecuties_photo_purge_stage_owner;
        REVOKE CREATE ON SCHEMA app FROM musclecuties_photo_purge_stage_owner;

        REVOKE ALL ON FUNCTION app.stage_account_photo_purge(uuid, text, timestamptz) FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION app.stage_account_photo_purge(uuid, text, timestamptz) TO musclecuties_app;
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_migrator') THEN
                GRANT EXECUTE ON FUNCTION app.stage_account_photo_purge(uuid, text, timestamptz) TO musclecuties_migrator;
            END IF;
        END $$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP FUNCTION app.stage_account_photo_purge(uuid, text, timestamptz);
        DROP POLICY photo_object_reconcile_owner_access ON app.photo_object_reconcile_targets;
        DROP POLICY photo_object_reconcile_stage_purge ON app.photo_object_reconcile_targets;
        REVOKE SELECT (target_key, not_before_utc),
               INSERT (id, target_key, not_before_utc, created_at_utc),
               UPDATE (not_before_utc)
            ON app.photo_object_reconcile_targets
            FROM musclecuties_photo_purge_stage_owner;
        REVOKE USAGE ON SCHEMA app FROM musclecuties_photo_purge_stage_owner;
        """);
}
