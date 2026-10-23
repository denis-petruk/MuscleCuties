using System.Collections.ObjectModel;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.UI.Services.Workout;

internal static class WorkoutSessionPresentationMapper
{
    public static IReadOnlyList<WorkoutActivitySectionItem> MapActivities(WorkoutSessionDetailDto detail)
    {
        var exercises = detail.Exercises.Select(MapExercise).ToArray();
        var byId = exercises.ToDictionary(item => item.WorkoutDayExerciseId);
        if (detail.Activities.Count == 0)
        {
            if (exercises.Length == 0) return [];
            var tag = exercises[0].ActivityTag;
            return
            [
                new WorkoutActivitySectionItem
                {
                    OrderIndex = 1,
                    TotalActivities = 1,
                    Tag = tag,
                    Title = detail.Title,
                    Subtitle = detail.Subtitle,
                    ActivityBackground = WorkoutPresentationPalette.Background(tag),
                    ActivityTextColor = WorkoutPresentationPalette.Text(tag),
                    Exercises = new ObservableCollection<WorkoutExerciseItem>(exercises)
                }
            ];
        }

        return detail.Activities.Select(activity => new WorkoutActivitySectionItem
        {
            OrderIndex = activity.OrderIndex,
            TotalActivities = activity.TotalActivities,
            Tag = activity.Tag,
            Title = activity.Title,
            Subtitle = activity.Subtitle,
            MetricText = activity.MetricText,
            SummaryText = activity.SummaryText,
            ActivityBackground = WorkoutPresentationPalette.Background(activity.Tag),
            ActivityTextColor = WorkoutPresentationPalette.Text(activity.Tag),
            Exercises = new ObservableCollection<WorkoutExerciseItem>(
                activity.WorkoutDayExerciseIds
                    .Where(byId.ContainsKey)
                    .Select(id => byId[id]))
        }).ToArray();
    }

    private static WorkoutExerciseItem MapExercise(WorkoutExerciseDetailDto source)
    {
        var item = new WorkoutExerciseItem
        {
            WorkoutDayExerciseId = source.WorkoutDayExerciseId,
            ExerciseId = source.ExerciseId,
            Name = source.Name,
            ActivityTag = source.ActivityTag,
            ActivityTitle = source.ActivityTitle,
            ActivityBackground = WorkoutPresentationPalette.Background(source.ActivityTag),
            ActivityTextColor = WorkoutPresentationPalette.Text(source.ActivityTag),
            IsLogged = source.IsLogged,
            IsOptional = source.IsOptional,
            OptionalSets = source.OptionalSets,
            PlannedSets = source.PlannedSets,
            Description = source.Description,
            ImageUrl = source.ImageUrl,
            VideoUrl = source.VideoUrl,
            TechniqueNotes = source.TechniqueNotes,
            QuickTipsText = source.QuickTipsText,
            TargetText = source.TargetText,
            PreviousText = source.PreviousText,
            RecommendationText = source.RecommendationText,
            MovementNotes = source.MovementNotes,
            LoggedSetsText = source.LoggedSetsText,
            LoggedRepsText = source.LoggedRepsText,
            LoggedWeightText = source.LoggedWeightText,
            LoggedDurationMinutesText = source.LoggedDurationMinutesText,
            LoggedDistanceKmText = source.LoggedDistanceKmText,
            LoggedPaceText = source.LoggedPaceText,
            LoggedHeartRateText = source.LoggedHeartRateText,
            LoggedPowerWattsText = source.LoggedPowerWattsText,
            LoggedCadenceRpmText = source.LoggedCadenceRpmText,
            LoggedEffortText = source.LoggedEffortText,
            DurationLabel = source.DurationLabel,
            DistanceLabel = source.DistanceLabel,
            PaceLabel = source.PaceLabel,
            UsesEnduranceMetrics = source.UsesEnduranceMetrics,
            UsesDurationMetric = source.UsesDurationMetric,
            UsesDistanceMetric = source.UsesDistanceMetric,
            UsesPaceMetric = source.UsesPaceMetric,
            UsesHeartRateMetric = source.UsesHeartRateMetric,
            UsesPowerMetric = source.UsesPowerMetric,
            UsesCadenceMetric = source.UsesCadenceMetric,
            UsesEffortMetric = source.UsesEffortMetric,
            UsesWeight = source.UsesWeight
        };
        foreach (var set in source.Sets)
            item.Sets.Add(new ExerciseSetInputItem(
                Guid.NewGuid(), Guid.Empty, source.ExerciseId.ToString(CultureInfo.InvariantCulture), source.Name,
                set.SetNumber, set.WeightKg, set.RepsCompleted, set.Rpe,
                set.IsWarmup, set.RestTimeSeconds)
            {
                OnChanged = item.UpdateAggregatesFromSets
            });
        return item;
    }
}
