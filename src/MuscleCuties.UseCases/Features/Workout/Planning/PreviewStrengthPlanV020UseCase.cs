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
        if (sourceData is not null)
        {
            var selectedActivities = sourceData.SelectedActivityTypes?.ToHashSet() ?? new HashSet<int>();
            selectedActivities.Add((int)request.Activity);

            var weekdays = sourceData.AvailableWeekdays is { Count: >= 2 }
                ? sourceData.AvailableWeekdays
                : request.AvailableWeekdays is { Count: >= 2 }
                    ? request.AvailableWeekdays
                    : new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };

            var durations = sourceData.ActivitySessionDurationsMinutes is not null
                ? new Dictionary<int, int>(sourceData.ActivitySessionDurationsMinutes)
                : new Dictionary<int, int>();
            if (!durations.ContainsKey((int)request.Activity))
            {
                durations[(int)request.Activity] = 45;
            }

            sourceData = sourceData with
            {
                SelectedActivityTypes = selectedActivities,
                AvailableWeekdays = weekdays,
                ActivitySessionDurationsMinutes = durations
            };
        }

        var excludedExerciseIds = new HashSet<int>(request.ExcludedExerciseIds ?? new HashSet<int>());
        if (sourceData?.ExcludedExerciseIds is { } savedExclusions)
            excludedExerciseIds.UnionWith(savedExclusions);
        var choices = StrengthPlanPreviewMapperV020.ToChoices(request with
        {
            ExcludedExerciseIds = excludedExerciseIds,
            AvailableWeekdays = (sourceData?.AvailableWeekdays ?? request.AvailableWeekdays)?.ToHashSet()
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
