namespace MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

public sealed record NutritionDiaryCommandEvent(long Cursor, DateOnly DiaryDate, string CommandId);

public sealed record NutritionDiaryCommandEventPage(IReadOnlyList<NutritionDiaryCommandEvent> Events, long NextCursor);
