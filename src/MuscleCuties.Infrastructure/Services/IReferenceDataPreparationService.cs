namespace MuscleCuties.Core.Services;

public interface IReferenceDataPreparationService
{
    Task EnsureWorkoutReadyAsync();
    Task EnsureNutritionReadyAsync();
}
