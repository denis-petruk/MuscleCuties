-- =============================================================================
-- MuscleCuties — K13 PostgreSQL Privilege Separation Migration Script
-- Version: 1.0.0
-- Roles:
--   1. mc_admin: Exclusive owner role for DbMaintenance migration runner.
--                Has DDL, schema ownership, advisory locking, migration table access.
--   2. mc_runtime: Least-privilege application connection role for AWS Lambda.
--                  DML only (SELECT, INSERT, UPDATE, DELETE), strictly NO DDL (no CREATE/DROP/ALTER/TRUNCATE).
--                  Enforces Row-Level Security (RLS) with NOBYPASSRLS.
-- =============================================================================

-- Step 1: Ensure roles exist with strict baseline attributes
DO $$
BEGIN
    -- mc_admin: Dedicated migration runner and schema owner
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mc_admin') THEN
        CREATE ROLE mc_admin WITH LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    ELSE
        ALTER ROLE mc_admin WITH NOSUPERUSER NOBYPASSRLS;
    END IF;

    -- mc_runtime: Dedicated Lambda application execution role
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mc_runtime') THEN
        CREATE ROLE mc_runtime WITH LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    ELSE
        ALTER ROLE mc_runtime WITH NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
    END IF;
END $$;

-- Step 2: Database Connection Privileges
DO $$
BEGIN
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO mc_admin, mc_runtime', current_database());
END $$;

-- Step 3: Schema Ownership and DDL Control (mc_admin exclusively)
ALTER SCHEMA app OWNER TO mc_admin;
ALTER SCHEMA catalog OWNER TO mc_admin;
ALTER SCHEMA ops OWNER TO mc_admin;

GRANT ALL PRIVILEGES ON SCHEMA app, catalog, ops TO mc_admin;

-- Ensure mc_runtime has zero DDL privileges in all schemas
REVOKE ALL ON SCHEMA ops FROM mc_runtime;
REVOKE CREATE ON SCHEMA app, catalog, ops, public FROM mc_runtime;
GRANT USAGE ON SCHEMA app, catalog TO mc_runtime;

-- Step 4: Transfer Table and Sequence Ownership to mc_admin
DO $$
DECLARE
    table_row record;
    seq_row record;
BEGIN
    -- Transfer all tables in app, catalog, ops to mc_admin
    FOR table_row IN
        SELECT n.nspname AS schema_name, c.relname AS table_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        WHERE n.nspname IN ('app', 'catalog', 'ops') AND c.relkind IN ('r', 'p', 'v', 'm')
    LOOP
        EXECUTE format('ALTER TABLE %I.%I OWNER TO mc_admin', table_row.schema_name, table_row.table_name);
    END LOOP;

    -- Transfer all sequences in app, catalog, ops to mc_admin
    FOR seq_row IN
        SELECT n.nspname AS schema_name, c.relname AS seq_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        WHERE n.nspname IN ('app', 'catalog', 'ops') AND c.relkind = 'S'
    LOOP
        EXECUTE format('ALTER SEQUENCE %I.%I OWNER TO mc_admin', seq_row.schema_name, seq_row.seq_name);
    END LOOP;
END $$;

-- Step 5: Grant mc_admin full administrative access to all tables and sequences
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA app, catalog, ops TO mc_admin;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA app, catalog, ops TO mc_admin;

-- Step 6: Grant mc_runtime least-privilege DML access only
-- 6a. Tenant & App tables: SELECT, INSERT, UPDATE, DELETE (NO TRUNCATE, NO DDL)
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA app TO mc_runtime;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA app TO mc_runtime;

-- 6b. Catalog tables: Read-only SELECT access
GRANT SELECT ON ALL TABLES IN SCHEMA catalog TO mc_runtime;

-- 6c. Ops schema: Read-only access to migration records for /health/ready inspection
GRANT USAGE ON SCHEMA ops TO mc_runtime;
GRANT SELECT ON TABLE ops.schema_migrations, ops.schema_metadata TO mc_runtime;

-- Step 7: Enforce Row-Level Security (RLS) on all tenant tables
DO $$
DECLARE
    tenant_table record;
BEGIN
    FOR tenant_table IN
        SELECT c.relname AS table_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        WHERE n.nspname = 'app' AND c.relkind IN ('r', 'p')
    LOOP
        EXECUTE format('ALTER TABLE app.%I ENABLE ROW LEVEL SECURITY', tenant_table.table_name);
        EXECUTE format('ALTER TABLE app.%I FORCE ROW LEVEL SECURITY', tenant_table.table_name);
    END LOOP;
END $$;

-- Step 8: Configure Default Privileges for Future Objects Created by mc_admin
ALTER DEFAULT PRIVILEGES FOR ROLE mc_admin IN SCHEMA app
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO mc_runtime;

ALTER DEFAULT PRIVILEGES FOR ROLE mc_admin IN SCHEMA app
    GRANT USAGE, SELECT ON SEQUENCES TO mc_runtime;

ALTER DEFAULT PRIVILEGES FOR ROLE mc_admin IN SCHEMA catalog
    GRANT SELECT ON TABLES TO mc_runtime;

ALTER DEFAULT PRIVILEGES FOR ROLE mc_admin IN SCHEMA ops
    GRANT SELECT ON TABLES TO mc_runtime;

-- Step 9: Advisory Lock Permissions (session-lock-runner)
-- mc_admin acquires class 0x4D435343, key 0x53434845
GRANT EXECUTE ON FUNCTION pg_advisory_lock(bigint), pg_advisory_unlock(bigint) TO mc_admin;
GRANT EXECUTE ON FUNCTION pg_try_advisory_lock(integer, integer), pg_advisory_unlock(integer, integer) TO mc_admin;
