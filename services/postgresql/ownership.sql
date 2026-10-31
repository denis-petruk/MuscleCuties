-- Restore the restricted owners that the EF migrations establish for these
-- SECURITY DEFINER objects. schema.sql is applied as musclecuties_migrator, so
-- these transfers are explicit here instead of being inherited from migrations.
-- PostgreSQL requires the new owner to hold CREATE on the schema during the
-- transfer, so mirror the narrow grant/revoke windows used by the migrations.
GRANT CREATE ON SCHEMA app TO musclecuties_photo_reconcile_owner;
ALTER TABLE app.photo_object_reconcile_targets
    OWNER TO musclecuties_photo_reconcile_owner;
REVOKE CREATE ON SCHEMA app FROM musclecuties_photo_reconcile_owner;

GRANT CREATE ON SCHEMA app TO musclecuties_cleanup_discovery_owner;
ALTER FUNCTION app.discover_due_photo_cleanup_owners(timestamp with time zone, uuid, integer)
    OWNER TO musclecuties_cleanup_discovery_owner;
REVOKE CREATE ON SCHEMA app FROM musclecuties_cleanup_discovery_owner;

GRANT CREATE ON SCHEMA app TO musclecuties_photo_purge_stage_owner;
ALTER FUNCTION app.stage_account_photo_purge(uuid, text, timestamp with time zone)
    OWNER TO musclecuties_photo_purge_stage_owner;
REVOKE CREATE ON SCHEMA app FROM musclecuties_photo_purge_stage_owner;
