-- Fresh-database installer: roles, schema, grants, then canonical catalog.
--
-- This path does not use EF migrations and creates no __EFMigrationsHistory.
-- Run as a PostgreSQL administrator that can SET ROLE musclecuties_migrator:
--   psql "$DATABASE_URL" -f services/postgresql/install.sql
--
-- bootstrap.sql creates the NOLOGIN group roles and the app/catalog/ops schemas.
-- schema.sql runs as the migrator group so every object it creates is owned there.
-- roles-and-grants.sql then applies runtime grants and removes the temporary
-- public CREATE privilege. catalog-seed.sql inserts the reviewed 742-row catalog.
\set ON_ERROR_STOP on
\ir bootstrap.sql

-- A managed PostgreSQL master user may not inherit the newly created group yet.
GRANT musclecuties_migrator TO CURRENT_USER;

SET ROLE musclecuties_migrator;
\ir schema.sql
\ir ownership.sql
RESET ROLE;

\ir roles-and-grants.sql

SET ROLE musclecuties_migrator;
\ir catalog-seed.sql
RESET ROLE;

-- Fail loudly if the reference data that workout generation needs is absent.
DO $$
BEGIN
    IF (SELECT count(*) FROM catalog.week_templates) = 0 THEN
        RAISE EXCEPTION 'catalog.week_templates is empty';
    END IF;
    IF (SELECT count(*) FROM catalog.exercises) = 0 THEN
        RAISE EXCEPTION 'catalog.exercises is empty';
    END IF;
    IF (SELECT count(*) FROM catalog.workout_exercise_definitions) = 0 THEN
        RAISE EXCEPTION 'catalog.workout_exercise_definitions is empty';
    END IF;
END $$;

SELECT 'catalog' AS section, count(*) AS exercises FROM catalog.exercises
UNION ALL SELECT 'week_templates', count(*) FROM catalog.week_templates
UNION ALL SELECT 'workout_exercise_definitions', count(*) FROM catalog.workout_exercise_definitions;
