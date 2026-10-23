using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.UseCases.Features.Nutrition.Diary;

public interface INutritionDiaryCommandEventStore
{
    Task<NutritionDiaryCommandEventPage> GetEventsAfterAsync(Guid userId, long afterCursor, int pageSize, CancellationToken cancellationToken);
}
