namespace MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

/// <summary>Which meal of the day an entry belongs to.</summary>
public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Snack
}

/// <summary>One diary entry with its nutrients for the amount eaten.</summary>
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
    string? AcceptedSuggestionPhase = null,
    int? LoggedMealId = null);

/// <summary>One day's entries and totals.</summary>
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

/// <summary>The diary over a range, newest first.</summary>
public sealed record NutritionDiaryHistory(
    IReadOnlyList<NutritionDiaryHistoryDay> Days,
    DateOnly? NextBeforeDate);

/// <summary>One day inside that history.</summary>
public sealed record NutritionDiaryHistoryDay(
    DateOnly Date,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    int WaterIntakeMl,
    int ItemsCount);

/// <summary>An entry to store, idempotent on its key.</summary>
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

/// <summary>A change to a day's notes or water.</summary>
public sealed record UpdateNutritionDiaryMetadata(
    DateOnly Date,
    int WaterIntakeMl,
    string? Notes);

/// <summary>Several entries saved together.</summary>
public sealed record BatchSaveNutritionDiaryEntries(
    Guid RequestId,
    DateOnly Date,
    IReadOnlyList<SaveNutritionDiaryEntry> Entries);

/// <summary>What the batch wrote.</summary>
public sealed record BatchSaveNutritionDiaryResponse(
    IReadOnlyList<string> Identities);

/// <summary>A day copy request.</summary>
public sealed record CopyNutritionDiary(
    Guid RequestId,
    DateOnly SourceDate,
    DateOnly TargetDate,
    MealType? MealType = null);

/// <summary>What the copy wrote.</summary>
public sealed record CopyNutritionDiaryResponse(
    int CopiedCount,
    IReadOnlyList<string> Identities);

/// <summary>A quick add of a food.</summary>
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

/// <summary>The stored quick add and the day's totals.</summary>
public sealed record QuickAddNutritionDiaryResponse(
    string Identity);

/// <summary>The values a diary day starts with, so an empty day still renders.</summary>
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
