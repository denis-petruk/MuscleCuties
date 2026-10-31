using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;

public sealed record DiaryNutritionTargetDto(
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat);

public sealed record DiaryWeekdayOverrideDto(
    Guid OwnerId,
    DayOfWeek Weekday,
    bool IsEnabled,
    DiaryNutritionTargetDto Target);

public enum DiaryTargetProvenanceDto
{
    Unavailable,
    Calculated,
    UserWeekdayOverride
}

public sealed record DiaryEffectiveTargetDto(
    DiaryNutritionTargetDto? Target,
    DiaryTargetProvenanceDto Provenance);
