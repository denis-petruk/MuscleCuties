-- Run once as a PostgreSQL administrator before migrating a fresh database.
-- Provision login principals and their group memberships separately; do not put
-- credentials in this file. Existing roles must already be restricted.
BEGIN;
DO $$
DECLARE role_name text;
BEGIN
    FOREACH role_name IN ARRAY ARRAY[
        'musclecuties_migrator', 'musclecuties_app', 'musclecuties_auth',
        'musclecuties_cleanup_dispatch', 'musclecuties_cleanup_discovery_owner',
        'musclecuties_photo_reconcile', 'musclecuties_photo_reconcile_owner',
        'musclecuties_photo_purge_stage_owner'
    ] LOOP
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
            EXECUTE format('CREATE ROLE %I NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION', role_name);
        END IF;
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name
                   AND (rolcanlogin OR rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole OR rolreplication)) THEN
            RAISE EXCEPTION 'Role % must be a restricted NOLOGIN role', role_name;
        END IF;
    END LOOP;
END $$;

-- The cleanup discovery migration transfers a function to this owner.
GRANT musclecuties_cleanup_discovery_owner TO musclecuties_migrator;
GRANT musclecuties_photo_reconcile_owner TO musclecuties_migrator;
GRANT musclecuties_photo_purge_stage_owner TO musclecuties_migrator;

CREATE SCHEMA IF NOT EXISTS app AUTHORIZATION musclecuties_migrator;
CREATE SCHEMA IF NOT EXISTS catalog AUTHORIZATION musclecuties_migrator;
CREATE SCHEMA IF NOT EXISTS ops AUTHORIZATION musclecuties_migrator;

-- Do not silently take over schemas belonging to another owner.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_namespace n JOIN pg_roles r ON r.oid = n.nspowner
               WHERE n.nspname IN ('app', 'catalog', 'ops') AND r.rolname <> 'musclecuties_migrator') THEN
        RAISE EXCEPTION 'Existing application schemas must be owned by musclecuties_migrator';
    END IF;
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO musclecuties_migrator', current_database());
END $$;
GRANT USAGE, CREATE ON SCHEMA app, catalog, ops TO musclecuties_migrator;

-- EF creates its migration history table in public. This temporary privilege is
-- removed by roles-and-grants.sql after migration.
GRANT USAGE, CREATE ON SCHEMA public TO musclecuties_migrator;
COMMIT;
