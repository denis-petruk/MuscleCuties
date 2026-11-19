-- Canonical, diffable description of a MuscleCuties staging database.
-- Run against source and restore, then diff the two outputs.
-- Deliberately schema-aware: workout_account_planning_states lives in "app", not "public".
\pset format unaligned
\pset tuples_only on

\echo '## server_version'
SELECT current_setting('server_version');

\echo '## schema_object_counts'
SELECT table_schema||'|'||count(*)
FROM information_schema.tables
WHERE table_schema NOT IN ('pg_catalog','information_schema')
GROUP BY table_schema ORDER BY table_schema;

\echo '## migration_history_counts'
SELECT 'ops.schema_migrations|'||count(*) FROM ops.schema_migrations;
SELECT 'public.__EFMigrationsHistory|'||count(*) FROM public."__EFMigrationsHistory";

\echo '## exact_table_counts'
SELECT format('SELECT %L AS t, count(*)::text AS n FROM %I.%I',
              schemaname||'.'||relname, schemaname, relname)
FROM pg_stat_user_tables ORDER BY 1
\gexec

\echo '## role_settings'
SELECT rolname||'|'||coalesce(array_to_string(rolconfig,','),'-')
FROM pg_roles WHERE rolname ~ '^(mc_|musclecuties_)' ORDER BY rolname;

\echo '## schema_privileges'
SELECT r.rolname||'|'||n.nspname||'|usage='||has_schema_privilege(r.rolname,n.oid,'USAGE')
       ||'|create='||has_schema_privilege(r.rolname,n.oid,'CREATE')
FROM pg_roles r CROSS JOIN pg_namespace n
WHERE r.rolname ~ '^(mc_|musclecuties_)'
  AND n.nspname IN ('app','catalog','ops','public')
ORDER BY 1,2;
