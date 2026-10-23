namespace MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Snack
}

public sealed record NutritionDiaryEntry(
    string Identity,
    DateOnly Date,
    MealType MealType,
    string Name,
    decimal ServingSize,
    string ServingUnit,
    decimal ServingsCount,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    DateTime LoggedAtUtc,
    bool IsEditable,
    string? AcceptedSuggestionName = null,
    string? AcceptedSuggestionPhase = null);

public sealed record NutritionDiaryDay(
    Guid LogId,
    DateOnly Date,
    int WaterIntakeMl,
    string? Notes,
    decimal TargetCalories,
    decimal TargetProtein,
    decimal TargetCarbs,
    decimal TargetFat,
    IReadOnlyList<NutritionDiaryEntry> Entries,
    string? NextPageToken,
    DateTime? CreatedAtUtc = null,
    DateTime? UpdatedAtUtc = null);

public sealed record NutritionDiaryHistory(
    IReadOnlyList<NutritionDiaryHistoryDay> Days,
    DateOnly? NextBeforeDate);

public sealed record NutritionDiaryHistoryDay(
    DateOnly Date,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    int WaterIntakeMl,
    int ItemsCount);

public sealed record SaveNutritionDiaryEntry(
    string? Identity,
    DateOnly Date,
    MealType MealType,
    string Name,
    decimal ServingSize,
    string ServingUnit,
    decimal ServingsCount,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    DateTime LoggedAtUtc,
    bool IsDeleted = false,
    bool RequireExisting = true);

public sealed record UpdateNutritionDiaryMetadata(
    DateOnly Date,
    int WaterIntakeMl,
    string? Notes);

public sealed record BatchSaveNutritionDiaryEntries(
    Guid RequestId,
    DateOnly Date,
    IReadOnlyList<SaveNutritionDiaryEntry> Entries);

public sealed record BatchSaveNutritionDiaryResponse(
    IReadOnlyList<string> Identities);

public sealed record CopyNutritionDiary(
    Guid RequestId,
    DateOnly SourceDate,
    DateOnly TargetDate,
    MealType? MealType = null);

public sealed record CopyNutritionDiaryResponse(
    int CopiedCount,
    IReadOnlyList<string> Identities);

public sealed record QuickAddNutritionDiaryEntry(
    Guid RequestId,
    DateOnly Date,
    MealType MealType,
    string Name,
    decimal Calories,
    decimal? Protein = null,
    decimal? Carbs = null,
    decimal? Fat = null,
    decimal? ServingSize = null,
    string? ServingUnit = null,
    decimal? ServingsCount = null);

public sealed record QuickAddNutritionDiaryResponse(
    string Identity);

public static class NutritionDiaryDefaults
{
    public const decimal DefaultQuickAddServingSize = 1m;
    public const decimal DefaultQuickAddServingsCount = 1m;
    public const decimal DefaultQuickAddMacroGrams = 0m;
    public const string DefaultQuickAddServingUnit = "serving";
    public const string DefaultQuickAddTitle = "Quick Calorie Add";
    public const string BatchCommandPrefix = "nutrition:diary:batch:";
    public const string CopyCommandPrefix = "nutrition:diary:copy:";
    public const string QuickAddCommandPrefix = "nutrition:diary:quick-add:";
}
