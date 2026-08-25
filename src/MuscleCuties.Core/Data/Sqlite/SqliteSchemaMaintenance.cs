using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Data.Sqlite;

internal sealed class SqliteSchemaMaintenance
{
    private readonly AppDatabase _db;

    private SqliteSchemaMaintenance(AppDatabase db)
    {
        _db = db;
    }

    public static async Task RepairAsync(AppDatabase db)
    {
        var maintenance = new SqliteSchemaMaintenance(db);
        await maintenance.RepairAsync();
    }

    private async Task RepairAsync()
    {
        await EnsureLoggedMealTimeColumnAsync();
        await EnsureFoodItemMetadataColumnsAsync();
        await EnsureMealTemplateCompatibilityColumnsAsync();
        await EnsureUserProfileCompatibilityColumnsAsync();
        await EnsureExerciseCompatibilityColumnsAsync();
        await EnsureWorkoutDayCompatibilityColumnsAsync();
        await EnsureWorkoutExerciseLogTableAsync();
        await EnsureCyclePhaseLogTableAsync();
    }

    private async Task EnsureLoggedMealTimeColumnAsync()
    {
        if (!await HasColumnAsync("LoggedMeals", "LoggedAt"))
        {
            await _db.Database.ExecuteSqlRawAsync("ALTER TABLE LoggedMeals ADD COLUMN LoggedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE LoggedMeals SET LoggedAt = CASE " +
                "WHEN CreatedAt IS NOT NULL AND CreatedAt <> '0001-01-01 00:00:00' THEN CreatedAt " +
                "ELSE Date END");
        }

        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_LoggedMeals_UserId_LoggedAt ON LoggedMeals (UserId, LoggedAt)");
    }

    private async Task EnsureFoodItemMetadataColumnsAsync()
    {
        await EnsureNullableColumnAsync("FoodItems", "DataType", "TEXT");
        await EnsureNullableColumnAsync("FoodItems", "BrandOwner", "TEXT");
        await EnsureNullableColumnAsync("FoodItems", "BrandName", "TEXT");
        await EnsureNullableColumnAsync("FoodItems", "GtinUpc", "TEXT");
        await EnsureNullableColumnAsync("FoodItems", "Ingredients", "TEXT");
        await EnsureNullableColumnAsync("FoodItems", "ServingSize", "REAL");
        await EnsureNullableColumnAsync("FoodItems", "ServingSizeUnit", "TEXT");
        await EnsureNullableColumnAsync("FoodItems", "ServingOptionsJson", "TEXT");
        await EnsureRealColumnAsync("FoodItems", "Potassium", 0f);
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_FoodItems_DataType_Name ON FoodItems (DataType, Name)");
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_FoodItems_BrandOwner ON FoodItems (BrandOwner)");
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_FoodItems_BrandName ON FoodItems (BrandName)");
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_FoodItems_GtinUpc ON FoodItems (GtinUpc)");
    }

    private async Task EnsureMealTemplateCompatibilityColumnsAsync()
    {
        await EnsureTextColumnAsync("MealTemplates", "DietaryTags", string.Empty);
        await EnsureTextColumnAsync("MealTemplates", "PhaseTags", string.Empty);
        await EnsureIntegerColumnAsync("MealTemplates", "SortOrder", 0);
    }

    private async Task EnsureUserProfileCompatibilityColumnsAsync()
    {
        await EnsureIntegerColumnAsync("UserProfiles", "TrainingExperienceLevel", 0);
        await EnsureIntegerColumnAsync("UserProfiles", "CycleTrackingMode", (int)CycleTrackingMode.ManualPhaseLogging);
        await EnsureNullableColumnAsync("UserProfiles", "CurrentCyclePhase", "INTEGER");
        await EnsureTextColumnAsync("UserProfiles", "UnitSystem", "Metric");
        await EnsureTextColumnAsync("UserProfiles", "BodyWeightUnit", "kg");
        await EnsureTextColumnAsync("UserProfiles", "FoodMassUnit", "g");
        await EnsureTextColumnAsync("UserProfiles", "HeightUnit", "cm");
        await EnsureTextColumnAsync("UserProfiles", "DistanceUnit", "km");
        await EnsureTextColumnAsync("UserProfiles", "EnergyUnit", "kcal");
        await EnsureTextColumnAsync("UserProfiles", "NutritionGoalsJson", string.Empty);
        await EnsureTextColumnAsync("UserProfiles", "PreferredWorkoutActivityTypes", string.Empty);
        await EnsureUserProfileCycleTrackingModeConstraintAsync();
    }

    private async Task EnsureUserProfileCycleTrackingModeConstraintAsync()
    {
        var tableSql = await GetTableSqlAsync("UserProfiles");
        if (string.IsNullOrWhiteSpace(tableSql) ||
            tableSql.Contains("CycleTrackingMode <= 3", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!tableSql.Contains("CycleTrackingMode <= 1", StringComparison.OrdinalIgnoreCase) &&
            !tableSql.Contains("CycleTrackingMode <= 2", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF");
        try
        {
            await _db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"UserProfiles_rebuild\"");
            await _db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE \"UserProfiles_rebuild\" (" +
                "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_UserProfiles\" PRIMARY KEY AUTOINCREMENT, " +
                "\"UserId\" INTEGER NOT NULL, " +
                "\"Name\" TEXT NOT NULL, " +
                "\"DateOfBirth\" TEXT NOT NULL, " +
                "\"Height\" REAL NOT NULL, " +
                "\"Weight\" REAL NOT NULL, " +
                "\"Goal\" INTEGER NOT NULL, " +
                "\"WeightGoalPace\" INTEGER NOT NULL, " +
                "\"TrainingExperienceLevel\" INTEGER NOT NULL, " +
                "\"CycleTrackingMode\" INTEGER NOT NULL, " +
                "\"CurrentCyclePhase\" INTEGER NULL, " +
                "\"WorkoutDaysPerWeek\" INTEGER NOT NULL, " +
                "\"CycleLength\" INTEGER NOT NULL, " +
                "\"DietaryTags\" TEXT NOT NULL, " +
                "\"PreferredWorkoutActivityTypes\" TEXT NOT NULL, " +
                "\"UnitSystem\" TEXT NOT NULL, " +
                "\"BodyWeightUnit\" TEXT NOT NULL, " +
                "\"FoodMassUnit\" TEXT NOT NULL, " +
                "\"HeightUnit\" TEXT NOT NULL, " +
                "\"DistanceUnit\" TEXT NOT NULL, " +
                "\"EnergyUnit\" TEXT NOT NULL, " +
                "\"NutritionGoalsJson\" TEXT NOT NULL, " +
                "\"UpdatedAt\" TEXT NOT NULL, " +
                "CONSTRAINT \"FK_UserProfiles_Users_UserId\" FOREIGN KEY (\"UserId\") REFERENCES \"Users\" (\"Id\") ON DELETE CASCADE, " +
                "CONSTRAINT \"CK_UserProfile_Height\" CHECK (Height >= 0), " +
                "CONSTRAINT \"CK_UserProfile_Weight\" CHECK (Weight >= 0), " +
                "CONSTRAINT \"CK_UserProfile_TrainingExperienceLevel\" CHECK (TrainingExperienceLevel >= 0 AND TrainingExperienceLevel <= 3), " +
                "CONSTRAINT \"CK_UserProfile_CycleTrackingMode\" CHECK (CycleTrackingMode >= 0 AND CycleTrackingMode <= 3), " +
                "CONSTRAINT \"CK_UserProfile_CurrentCyclePhase\" CHECK (CurrentCyclePhase IS NULL OR CurrentCyclePhase BETWEEN 0 AND 3), " +
                "CONSTRAINT \"CK_UserProfile_WorkoutDays\" CHECK (WorkoutDaysPerWeek >= 0 AND WorkoutDaysPerWeek <= 7), " +
                "CONSTRAINT \"CK_UserProfile_CycleLength\" CHECK (CycleLength >= 0 AND CycleLength <= 60))");

            await _db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"UserProfiles_rebuild\" (" +
                "\"Id\", \"UserId\", \"Name\", \"DateOfBirth\", \"Height\", \"Weight\", \"Goal\", \"WeightGoalPace\", " +
                "\"TrainingExperienceLevel\", \"CycleTrackingMode\", \"CurrentCyclePhase\", \"WorkoutDaysPerWeek\", \"CycleLength\", \"DietaryTags\", " +
                "\"PreferredWorkoutActivityTypes\", \"UnitSystem\", \"BodyWeightUnit\", \"FoodMassUnit\", \"HeightUnit\", " +
                "\"DistanceUnit\", \"EnergyUnit\", \"NutritionGoalsJson\", \"UpdatedAt\") " +
                "SELECT \"Id\", \"UserId\", \"Name\", \"DateOfBirth\", \"Height\", \"Weight\", \"Goal\", \"WeightGoalPace\", " +
                "\"TrainingExperienceLevel\", \"CycleTrackingMode\", \"CurrentCyclePhase\", \"WorkoutDaysPerWeek\", \"CycleLength\", \"DietaryTags\", " +
                "\"PreferredWorkoutActivityTypes\", \"UnitSystem\", \"BodyWeightUnit\", \"FoodMassUnit\", \"HeightUnit\", " +
                "\"DistanceUnit\", \"EnergyUnit\", \"NutritionGoalsJson\", \"UpdatedAt\" " +
                "FROM \"UserProfiles\"");

            await _db.Database.ExecuteSqlRawAsync("DROP TABLE \"UserProfiles\"");
            await _db.Database.ExecuteSqlRawAsync("ALTER TABLE \"UserProfiles_rebuild\" RENAME TO \"UserProfiles\"");
            await _db.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_UserProfiles_UserId\" ON \"UserProfiles\" (\"UserId\")");
        }
        finally
        {
            await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON");
        }
    }

    private async Task EnsureExerciseCompatibilityColumnsAsync()
    {
        await EnsureTextColumnAsync("Exercises", "Code", string.Empty);
        await EnsureIntegerColumnAsync("Exercises", "IsInjuryFriendly", 0);
        await EnsureNullableColumnAsync("Exercises", "VideoUrl", "TEXT");
        await EnsureNullableColumnAsync("Exercises", "TechniqueNotes", "TEXT");
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_Exercises_Code ON Exercises (Code)");
    }

    private async Task EnsureWorkoutDayCompatibilityColumnsAsync()
    {
        await EnsureIntegerColumnAsync("WorkoutDays", "WorkoutType", (int)WorkoutType.Strength);
    }

    private async Task EnsureWorkoutExerciseLogTableAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"WorkoutExerciseLogs\" (" +
            "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_WorkoutExerciseLogs\" PRIMARY KEY AUTOINCREMENT, " +
            "\"WorkoutLogId\" INTEGER NOT NULL, " +
            "\"WorkoutDayExerciseId\" INTEGER NOT NULL, " +
            "\"ExerciseId\" INTEGER NOT NULL, " +
            "\"CompletedSets\" INTEGER NOT NULL, " +
            "\"CompletedReps\" INTEGER NOT NULL, " +
            "\"WeightKg\" REAL NULL, " +
            "\"CompletedDurationSeconds\" INTEGER NULL, " +
            "\"DistanceKm\" REAL NULL, " +
            "\"AverageHeartRateBpm\" INTEGER NULL, " +
            "\"PaceSecondsPerKm\" INTEGER NULL, " +
            "\"CreatedAt\" TEXT NOT NULL, " +
            "CONSTRAINT \"FK_WorkoutExerciseLogs_WorkoutLogs_WorkoutLogId\" FOREIGN KEY (\"WorkoutLogId\") REFERENCES \"WorkoutLogs\" (\"Id\") ON DELETE CASCADE, " +
            "CONSTRAINT \"FK_WorkoutExerciseLogs_WorkoutDayExercises_WorkoutDayExerciseId\" FOREIGN KEY (\"WorkoutDayExerciseId\") REFERENCES \"WorkoutDayExercises\" (\"Id\") ON DELETE RESTRICT, " +
            "CONSTRAINT \"FK_WorkoutExerciseLogs_Exercises_ExerciseId\" FOREIGN KEY (\"ExerciseId\") REFERENCES \"Exercises\" (\"Id\") ON DELETE RESTRICT, " +
            "CONSTRAINT \"CK_WorkoutExerciseLog_Sets\" CHECK (CompletedSets >= 0), " +
            "CONSTRAINT \"CK_WorkoutExerciseLog_Reps\" CHECK (CompletedReps >= 0), " +
            "CONSTRAINT \"CK_WorkoutExerciseLog_Weight\" CHECK (WeightKg IS NULL OR WeightKg >= 0), " +
            "CONSTRAINT \"CK_WorkoutExerciseLog_Duration\" CHECK (CompletedDurationSeconds IS NULL OR CompletedDurationSeconds >= 0), " +
            "CONSTRAINT \"CK_WorkoutExerciseLog_Distance\" CHECK (DistanceKm IS NULL OR DistanceKm >= 0), " +
            "CONSTRAINT \"CK_WorkoutExerciseLog_HeartRate\" CHECK (AverageHeartRateBpm IS NULL OR AverageHeartRateBpm >= 0), " +
            "CONSTRAINT \"CK_WorkoutExerciseLog_Pace\" CHECK (PaceSecondsPerKm IS NULL OR PaceSecondsPerKm >= 0))");

        await EnsureNullableColumnAsync("WorkoutExerciseLogs", "CompletedDurationSeconds", "INTEGER");
        await EnsureNullableColumnAsync("WorkoutExerciseLogs", "DistanceKm", "REAL");
        await EnsureNullableColumnAsync("WorkoutExerciseLogs", "AverageHeartRateBpm", "INTEGER");
        await EnsureNullableColumnAsync("WorkoutExerciseLogs", "PaceSecondsPerKm", "INTEGER");

        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WorkoutExerciseLogs_ExerciseId_CreatedAt\" ON \"WorkoutExerciseLogs\" (\"ExerciseId\", \"CreatedAt\")");
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_WorkoutExerciseLogs_WorkoutLogId_WorkoutDayExerciseId\" ON \"WorkoutExerciseLogs\" (\"WorkoutLogId\", \"WorkoutDayExerciseId\")");
    }

    private async Task EnsureCyclePhaseLogTableAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"CyclePhaseLogs\" (" +
            "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_CyclePhaseLogs\" PRIMARY KEY AUTOINCREMENT, " +
            "\"UserId\" INTEGER NOT NULL, " +
            "\"CycleLogId\" INTEGER NULL, " +
            "\"Phase\" INTEGER NOT NULL, " +
            "\"LoggedAt\" TEXT NOT NULL, " +
            "\"Note\" TEXT NULL, " +
            "\"CreatedAt\" TEXT NOT NULL, " +
            "CONSTRAINT \"FK_CyclePhaseLogs_Users_UserId\" FOREIGN KEY (\"UserId\") REFERENCES \"Users\" (\"Id\") ON DELETE CASCADE, " +
            "CONSTRAINT \"FK_CyclePhaseLogs_CycleLogs_CycleLogId\" FOREIGN KEY (\"CycleLogId\") REFERENCES \"CycleLogs\" (\"Id\") ON DELETE SET NULL)");

        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_CyclePhaseLogs_UserId_LoggedAt\" ON \"CyclePhaseLogs\" (\"UserId\", \"LoggedAt\")");
        await _db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_CyclePhaseLogs_CycleLogId_LoggedAt\" ON \"CyclePhaseLogs\" (\"CycleLogId\", \"LoggedAt\")");
    }

    private async Task EnsureIntegerColumnAsync(string tableName, string columnName, int defaultValue)
    {
        if (await HasColumnAsync(tableName, columnName))
            return;

        var commandText =
            $"ALTER TABLE {SafeSqlIdentifier(tableName)} ADD COLUMN {SafeSqlIdentifier(columnName)} INTEGER NOT NULL DEFAULT {defaultValue}";
        await _db.Database.ExecuteSqlRawAsync(commandText);
    }

    private async Task EnsureRealColumnAsync(string tableName, string columnName, float defaultValue)
    {
        if (await HasColumnAsync(tableName, columnName))
            return;

        var commandText =
            $"ALTER TABLE {SafeSqlIdentifier(tableName)} ADD COLUMN {SafeSqlIdentifier(columnName)} REAL NOT NULL DEFAULT {defaultValue.ToString(CultureInfo.InvariantCulture)}";
        await _db.Database.ExecuteSqlRawAsync(commandText);
    }

    private async Task EnsureTextColumnAsync(string tableName, string columnName, string defaultValue)
    {
        if (await HasColumnAsync(tableName, columnName))
            return;

        var escapedDefaultValue = defaultValue.Replace("'", "''", StringComparison.Ordinal);
        var commandText =
            $"ALTER TABLE {SafeSqlIdentifier(tableName)} ADD COLUMN {SafeSqlIdentifier(columnName)} TEXT NOT NULL DEFAULT '{escapedDefaultValue}'";
        await _db.Database.ExecuteSqlRawAsync(commandText);
    }

    private async Task EnsureNullableColumnAsync(string tableName, string columnName, string columnType)
    {
        if (await HasColumnAsync(tableName, columnName))
            return;

        var commandText =
            $"ALTER TABLE {SafeSqlIdentifier(tableName)} ADD COLUMN {SafeSqlIdentifier(columnName)} {SafeColumnType(columnType)} NULL";
        await _db.Database.ExecuteSqlRawAsync(commandText);
    }

    private static string SafeSqlIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw new InvalidOperationException($"Unsafe SQLite identifier: {value}");
        }

        return $"\"{value}\"";
    }

    private static string SafeColumnType(string value) => value switch
    {
        "TEXT" => "TEXT",
        "REAL" => "REAL",
        "INTEGER" => "INTEGER",
        _ => throw new InvalidOperationException($"Unsupported SQLite column type: {value}")
    };

    private async Task<bool> HasColumnAsync(string tableName, string columnName)
    {
        var connection = _db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}')";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private async Task<string?> GetTableSqlAsync(string tableName)
    {
        var connection = _db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $tableName";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return result as string;
    }
}
