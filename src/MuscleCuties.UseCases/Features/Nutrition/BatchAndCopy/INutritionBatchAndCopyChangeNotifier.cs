namespace MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;

/// <summary>Publishes owner-scoped PostgreSQL diary changes after a command commits.</summary>
public interface INutritionBatchAndCopyChangeNotifier
{
    void NotifyNutritionChanged(Guid userId, DateOnly date);
}
