using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.UI.Services.Workout;

/// <summary>Turns a session DTO into the activity blocks and exercise cards the session screen binds,</summary>
/// including the per-set rows and their numbering.</summary>
internal static class WorkoutSessionPresentationMapper
{
    /// <summary>Builds the blocks in plan order. A plan without activity blocks is shown as one block, and a plan</summary>
    /// with neither blocks nor exercises maps to nothing rather than an empty section.</summary>
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
            Prescription = activity.Prescription,
            ActivityType = activity.ActivityType,
            ActivityBackground = WorkoutPresentationPalette.Background(activity.Tag),
            ActivityTextColor = WorkoutPresentationPalette.Text(activity.Tag),
            Exercises = new ObservableCollection<WorkoutExerciseItem>(
                activity.WorkoutDayExerciseIds
                    .Where(byId.ContainsKey)
                    .Select(id => byId[id]))
        }).ToArray();
    }

    /// <summary>Builds one exercise card: its planned values, its metric fields, and its sets renumbered by kind
    /// so a warm-up never hides "working set 1".</summary>
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
            PlannedWarmUpSets = source.PlannedWarmUpSets,
            SupersetGroupId = source.SupersetGroupId,
            IsUnilateral = source.IsUnilateral,
            RequiresInitialCalibration = source.RequiresInitialCalibration,
            Description = source.Description,
            ImageUrl = source.ImageUrl,
            TechniqueNotes = source.TechniqueNotes,
            QuickTipsText = source.QuickTipsText,
            TargetText = source.MinRepetitions > 0 && source.MaxRepetitions >= source.MinRepetitions
                ? UiTextCatalog.Format("Workout_Session_TargetRangeFormat", source.PlannedSets,
                    source.MinRepetitions, source.MaxRepetitions, source.TargetRir)
                : source.TargetText,
            PreviousText = source.PreviousText,
            RecommendationText = FormatRecommendation(source),
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
        var warmupSetNumber = 0;
        var workingSetNumber = 0;
        foreach (var set in source.Sets)
        {
            var input = new ExerciseSetInputItem(
                Guid.NewGuid(), Guid.Empty, source.ExerciseId.ToString(CultureInfo.InvariantCulture), source.Name,
                set.SetNumber, set.WeightKg, set.RepsCompleted, set.Rpe,
                set.IsWarmup, set.RestTimeSeconds)
            {
                OnChanged = () => item.UpdateAggregatesFromSets(fromUser: true),
                RemoveCommand = item.RemoveSetCommand,
                ShowWeight = item.UsesWeight,
                DisplaySetNumber = set.IsWarmup ? ++warmupSetNumber : ++workingSetNumber,
                IsObserved = set.IsObserved,
                TechniqueAcceptable = set.TechniqueAcceptable,
                WasEdited = false,
                ShowPerSideReps = item.IsUnilateral
            };
            if (source.RequiresInitialCalibration && !set.IsObserved)
                input.ClearUnobservedCalibrationWeightInput();
            input.HydrateSideRepetitions(set.LeftSideReps, set.RightSideReps);
            item.Sets.Add(input);
        }
        if (item.Sets.Count == 0 && item.UsesStrengthMetrics)
            item.EnsureSets(source.PlannedSets + source.PlannedWarmUpSets > 0
                ? source.PlannedSets + source.PlannedWarmUpSets : 3);
        item.RenumberSets();
        item.SyncSavedOptionalState();
        return item;
    }

    /// <summary>Uses the localized recommendation when the catalog has its key, otherwise the server text, so an</summary>
    /// unknown code never appears raw on screen.</summary>
    private static string FormatRecommendation(WorkoutExerciseDetailDto source)
    {
        if (string.IsNullOrWhiteSpace(source.RecommendationCode))
            return source.RecommendationText;

        if (!UiTextCatalog.HasKey(source.RecommendationCode))
            return source.RecommendationText;

        return source.RecommendationLoadKg is { } load
            ? UiTextCatalog.Format(source.RecommendationCode,
                load.ToString("0.#", CultureInfo.CurrentCulture))
            : UiTextCatalog.Get(source.RecommendationCode);
    }
}
