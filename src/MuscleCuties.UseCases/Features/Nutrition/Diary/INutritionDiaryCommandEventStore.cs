using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Diary;

/// <summary>Appends and reads the diary's command events.</summary>
public interface INutritionDiaryCommandEventStore
{
    Task<NutritionDiaryCommandEventPage> GetEventsAfterAsync(Guid userId, long afterCursor, int pageSize, CancellationToken cancellationToken);
}
