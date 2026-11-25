namespace MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

/// <summary>One appended diary command, which other devices replay to catch up.</summary>
public sealed record NutritionDiaryCommandEvent(long Cursor, DateOnly DiaryDate, string CommandId);

/// <summary>A page of those events with the cursor to continue from.</summary>
public sealed record NutritionDiaryCommandEventPage(IReadOnlyList<NutritionDiaryCommandEvent> Events, long NextCursor);
