using Npgsql;

namespace MuscleCuties.DbMaintenance;

/// <summary>Read-only checks for the ownership migration before any pending EF migration runs.</summary>
public static class MigrationPrerequisites
{
    public const string OwnershipMigrationId = "20261027234500_OwnReviewedTemplateVolumeFloors";

    public static async Task<MigrationPrerequisiteResult> CheckAsync(
        NpgsqlConnection connection, bool ownershipMigrationPending, CancellationToken cancellationToken = default)
    {
        if (!ownershipMigrationPending)
            return new MigrationPrerequisiteResult(true, "not_pending");

        const string sql = """
            SELECT
                EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_migrator') AS migrator_exists,
                CASE WHEN EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_migrator')
                    THEN pg_has_role(current_user, 'musclecuties_migrator', 'MEMBER')
                    ELSE false END AS runner_can_assume_migrator,
                CASE WHEN EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_migrator')
                          AND EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'musclecuties_app')
                    THEN pg_has_role('musclecuties_app', 'musclecuties_migrator', 'MEMBER')
                    ELSE false END AS app_can_assume_migrator,
                (SELECT pg_has_role(current_user, c.relowner, 'MEMBER')
                   FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                  WHERE n.nspname = 'catalog' AND c.relname = 'workout_template_volume_floors'
                    AND c.relkind = 'r') AS runner_can_assume_floor_owner
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new MigrationPrerequisiteResult(false, "catalog_unreadable");

        if (!reader.GetBoolean(0))
            return new MigrationPrerequisiteResult(false, "migrator_role_missing");
        if (!reader.GetBoolean(1))
            return new MigrationPrerequisiteResult(false, "runner_lacks_migrator_membership");
        if (reader.GetBoolean(2))
            return new MigrationPrerequisiteResult(false, "app_has_migrator_membership");
        if (!reader.IsDBNull(3) && !reader.GetBoolean(3))
            return new MigrationPrerequisiteResult(false, "runner_lacks_floor_owner_membership");
        return new MigrationPrerequisiteResult(true, "ready");
    }
}

public sealed record MigrationPrerequisiteResult(bool Ready, string Code);
