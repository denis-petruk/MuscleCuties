using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;

/// <summary>DTO-only projection; persistence and UI binding remain sequential integration work.</summary>
public sealed class DiaryTargetPolicyUseCase
{
    public DiaryEffectiveTargetDto Resolve(
        Guid userId,
        DateOnly date,
        DiaryNutritionTargetDto? calculatedTarget,
        DiaryWeekdayOverrideDto? weekdayOverride)
    {
        var calculated = calculatedTarget is null ? null : ToDomain(calculatedTarget);
        var userOverride = weekdayOverride is null
            ? null
            : DiaryWeekdayOverride.Create(weekdayOverride.OwnerId, weekdayOverride.Weekday,
                weekdayOverride.IsEnabled, ToDomain(weekdayOverride.Target));
        var result = DiaryTargetPolicy.Resolve(date, userId, calculated, userOverride);
        return new DiaryEffectiveTargetDto(
            result.Target is null ? null : ToDto(result.Target),
            result.Source switch
            {
                DiaryTargetSource.Unavailable => DiaryTargetProvenanceDto.Unavailable,
                DiaryTargetSource.Calculated => DiaryTargetProvenanceDto.Calculated,
                DiaryTargetSource.UserWeekdayOverride => DiaryTargetProvenanceDto.UserWeekdayOverride,
                _ => throw new ArgumentOutOfRangeException(nameof(result))
            });
    }

    private static DiaryNutritionTarget ToDomain(DiaryNutritionTargetDto target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return DiaryNutritionTarget.Create(target.Calories, target.Protein, target.Carbs, target.Fat);
    }

    private static DiaryNutritionTargetDto ToDto(DiaryNutritionTarget target) =>
        new(target.Calories, target.Protein, target.Carbs, target.Fat);
}
