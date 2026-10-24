using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Loads trusted planning inputs and delegates all proposal decisions to Core.</summary>
public sealed class PreviewStrengthPlanV020UseCase(IStrengthPlanPreviewSourceV020 source)
{
    public async Task<StrengthPlanPreviewResponseV020> ExecuteAsync(Guid userId,
        StrengthPlanPreviewRequestV020 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty)
            return StrengthPlanPreviewMapperV020.ToResponse(
                StrengthPlanPreviewRule.Evaluate(null, null));

        var sourceData = await source.LoadAsync(userId, cancellationToken).ConfigureAwait(false);
        var excludedExerciseIds = new HashSet<int>(request.ExcludedExerciseIds ?? new HashSet<int>());
        if (sourceData?.ExcludedExerciseIds is { } savedExclusions)
            excludedExerciseIds.UnionWith(savedExclusions);
        var choices = StrengthPlanPreviewMapperV020.ToChoices(request with
        {
            ExcludedExerciseIds = excludedExerciseIds
        });
        var planningData = sourceData is null
            ? null
            : StrengthPlanPreviewMapperV020.ToData(sourceData);
        var result = StrengthPlanPreviewRule.Evaluate(choices, planningData);
        var response = StrengthPlanPreviewMapperV020.ToResponse(result);
        if (sourceData is null)
            return response;

        var mixedContext = MixedActivityScheduleMapperV020.ToContext(request, sourceData, result);
        var mixedSchedule = MixedActivityWeekScheduleRule.Evaluate(mixedContext);
        return StrengthPlanPreviewMapperV020.AttachMixedSchedule(response, mixedSchedule, request.Activity);
    }
}
