using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthPlanPreviewRuleTests
{
    private const int DurationTooShortMinutes = WorkoutPreferencesRule.MinimumActivitySessionDurationMinutes;
    private const int DurationSufficientMinutes = DurationTooShortMinutes +
        WorkoutPreferencesRule.ActivitySessionDurationIncrementMinutes;
    private const int SlowExerciseSecondsPerRep = 17;
    private const int NoWarmupOrCooldownMinutes = 0;
    private const int FirstExerciseId = 101;
    private const int FirstGoalAccessoryExerciseId = 201;
    private const int SecondGoalAccessoryExerciseId = 202;
    private const int TierAScore = 3;
    private const int TierCScore = 1;
    private const int ExerciseIdOffset = 1;
    private const int SecondsPerRep = 4;
    private const int SetupMinutes = 1;
    private const int CompletedWeeks = 1;
    private const int PreviousSessionDayOffset = -1;
    private const int InvalidDurationOffsetMinutes = 1;
    private const int LocalYear = 2026;
    private const int LocalMonth = 10;
    private const int LocalDay = 23;
    private const decimal AverageSessionsPerWeek = 1m;
    private static readonly DateOnly LocalDate = new(LocalYear, LocalMonth, LocalDay);

    [Fact]
    public void SelectedStrengthActivityDurationLimitsItsProposal()
    {
        var tooShort = StrengthPlanPreviewRule.Evaluate(Choices(), Data(
            new Dictionary<WorkoutActivityType, int>
            {
                [WorkoutActivityType.GeneralStrength] = DurationTooShortMinutes,
                [WorkoutActivityType.Walking] = WorkoutPreferencesRule.MaximumActivitySessionDurationMinutes
            }, SlowExerciseSecondsPerRep));

        Assert.Equal(StrengthPlanPreviewStatus.Limited, tooShort.Status);
        Assert.Contains("RequiredMovementWorkExceedsSessionDurationOrTimingMissing",
            tooShort.LimitationCodes);
        Assert.All(tooShort.Sessions, session => Assert.Null(session.EstimatedMinutes));
    }

    [Fact]
    public void DurationForAnotherActivityDoesNotOverrideSelectedStrengthDuration()
    {
        var result = StrengthPlanPreviewRule.Evaluate(Choices(), Data(
            new Dictionary<WorkoutActivityType, int>
            {
                [WorkoutActivityType.GeneralStrength] = DurationSufficientMinutes,
                [WorkoutActivityType.Walking] = DurationTooShortMinutes
            }, SlowExerciseSecondsPerRep));

        Assert.Equal(StrengthPlanPreviewStatus.Feasible, result.Status);
        Assert.All(result.Sessions, session => Assert.InRange(session.EstimatedMinutes!.Value,
            0m, DurationSufficientMinutes));
        Assert.Equal(DayOfWeek.Monday, result.Sessions[0].PlannedWeekday);
    }

    [Fact]
    public void Goal_Tier_Priorities_Change_Only_The_Goal_Specific_Accessory()
    {
        var data = Data(new Dictionary<WorkoutActivityType, int>
        {
            [WorkoutActivityType.GeneralStrength] = WorkoutPreferencesRule.MaximumActivitySessionDurationMinutes
        });
        var templates = StrengthTemplateCatalog.ForSchedule(
            ActivityPrescriptionRule.GeneralStrengthSessionsPerWeek);
        var accessoryCandidates = new[]
        {
            Entry(FirstGoalAccessoryExerciseId, MovementPattern.HipAbduction),
            Entry(SecondGoalAccessoryExerciseId, MovementPattern.ElbowFlexion)
        };
        var allowedIdsBySlot = data.ReviewedAllowedExerciseIdsByTemplateSlot!
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var template in templates)
            allowedIdsBySlot[$"{template.TemplateId}|goal-specific-accessory"] =
                accessoryCandidates.Select(entry => entry.Candidate.ExerciseId).ToHashSet();

        var catalog = data.Catalog!.Concat(accessoryCandidates).ToArray();
        var secondsPerRepByExerciseId = data.SecondsPerRepByExerciseId!.ToDictionary(pair => pair.Key,
            pair => pair.Value);
        var primaryMuscleByExerciseId = data.PrimaryMuscleByExerciseId!.ToDictionary(pair => pair.Key,
            pair => pair.Value);
        foreach (var accessory in accessoryCandidates)
        {
            secondsPerRepByExerciseId[accessory.Candidate.ExerciseId] = SecondsPerRep;
            primaryMuscleByExerciseId[accessory.Candidate.ExerciseId] = accessory.Candidate.Pattern.ToString();
        }

        var firstPriorityData = data with
        {
            Catalog = catalog,
            ReviewedAllowedExerciseIdsByTemplateSlot = allowedIdsBySlot,
            SecondsPerRepByExerciseId = secondsPerRepByExerciseId,
            PrimaryMuscleByExerciseId = primaryMuscleByExerciseId,
            GoalPriorityByExerciseId = new Dictionary<int, int>
            {
                [FirstGoalAccessoryExerciseId] = TierAScore,
                [SecondGoalAccessoryExerciseId] = TierCScore
            }
        };
        var secondPriorityData = firstPriorityData with
        {
            GoalPriorityByExerciseId = new Dictionary<int, int>
            {
                [FirstGoalAccessoryExerciseId] = TierCScore,
                [SecondGoalAccessoryExerciseId] = TierAScore
            }
        };

        var firstGoalResult = StrengthPlanPreviewRule.Evaluate(Choices(), firstPriorityData);
        var secondGoalResult = StrengthPlanPreviewRule.Evaluate(Choices(), secondPriorityData);

        Assert.Equal(StrengthPlanPreviewStatus.Feasible, firstGoalResult.Status);
        Assert.Equal(StrengthPlanPreviewStatus.Feasible, secondGoalResult.Status);
        Assert.Equal(firstGoalResult.Sessions.Count, secondGoalResult.Sessions.Count);
        for (var sessionIndex = 0; sessionIndex < firstGoalResult.Sessions.Count; sessionIndex++)
        {
            var firstSession = firstGoalResult.Sessions[sessionIndex];
            var secondSession = secondGoalResult.Sessions[sessionIndex];
            Assert.Equal(firstSession.Slots
                    .Where(slot => slot.MovementSlot != StrengthTemplateSlotId.GoalSpecificAccessory)
                    .Select(slot => slot.ExerciseId),
                secondSession.Slots
                    .Where(slot => slot.MovementSlot != StrengthTemplateSlotId.GoalSpecificAccessory)
                    .Select(slot => slot.ExerciseId));
            Assert.Equal(FirstGoalAccessoryExerciseId,
                firstSession.Slots.Single(slot =>
                    slot.MovementSlot == StrengthTemplateSlotId.GoalSpecificAccessory).ExerciseId);
            Assert.Equal(SecondGoalAccessoryExerciseId,
                secondSession.Slots.Single(slot =>
                    slot.MovementSlot == StrengthTemplateSlotId.GoalSpecificAccessory).ExerciseId);
        }
    }

    [Theory]
    [InlineData(WorkoutActivityType.GeneralStrength, ActivityPrescriptionRule.GeneralStrengthSessionsPerWeek)]
    [InlineData(WorkoutActivityType.HighVolumeStrength, ActivityPrescriptionRule.HighVolumeStrengthSessionsPerWeek)]
    [InlineData(WorkoutActivityType.StrengthHighIntensity,
        ActivityPrescriptionRule.StrengthHighIntensityMaximumSessionsPerWeekInitially)]
    public void ScheduledFrequencyComesFromSelectedActivityPrescription(
        WorkoutActivityType activity, int expectedSessionsPerWeek)
    {
        var result = StrengthPlanPreviewRule.Evaluate(Choices(activity), Data(activity: activity));

        Assert.Equal(expectedSessionsPerWeek, result.Sessions.Count);
        Assert.Equal(expectedSessionsPerWeek,
            result.Sessions.Select(session => session.PlannedWeekday).Distinct().Count());
    }

    [Fact]
    public void High_Intensity_Preview_Offers_Trusted_Candidates_But_Stays_Limited_Until_Confirmed()
    {
        var data = Data(activity: WorkoutActivityType.StrengthHighIntensity);
        var candidateIds = HeavyCandidateIds(data);
        data = data with
        {
            CompletedExerciseExposureCounts = candidateIds.ToDictionary(id => id,
                _ => ActivityPrescriptionRule.StrengthHighIntensityRequiredExerciseExposures),
            KnownManageableLoadExerciseIds = candidateIds.ToHashSet()
        };

        var result = StrengthPlanPreviewRule.Evaluate(EstablishedHighIntensityChoices(), data);
        var presentedCandidates = result.Sessions.SelectMany(session => session.Slots)
            .Where(slot => slot.IsHeavyMain && slot.ExerciseId.HasValue)
            .Select(slot => slot.ExerciseId!.Value).ToHashSet();

        Assert.Equal(StrengthPlanPreviewStatus.Limited, result.Status);
        Assert.Equal(candidateIds.ToHashSet(), presentedCandidates);
        Assert.Contains(StrengthHighIntensityExerciseEvidenceRule.UserConfirmationRequired,
            result.LimitationCodes);
    }

    [Fact]
    public void High_Intensity_Preview_Resolves_When_Trusted_Candidates_Are_Confirmed()
    {
        var data = Data(activity: WorkoutActivityType.StrengthHighIntensity);
        var candidateIds = HeavyCandidateIds(data);
        data = data with
        {
            CompletedExerciseExposureCounts = candidateIds.ToDictionary(id => id,
                _ => ActivityPrescriptionRule.StrengthHighIntensityRequiredExerciseExposures),
            KnownManageableLoadExerciseIds = candidateIds.ToHashSet()
        };
        var choices = EstablishedHighIntensityChoices() with
        {
            UserConfirmedFamiliarExerciseIds = candidateIds.ToHashSet(),
            UserConfirmedManageableLoadExerciseIds = candidateIds.ToHashSet()
        };

        var result = StrengthPlanPreviewRule.Evaluate(choices, data);

        Assert.Equal(StrengthPlanPreviewStatus.Feasible, result.Status);
        Assert.Equal(ActivityPrescriptionRule.StrengthHighIntensityMaximumSessionsPerWeekInitially,
            result.Sessions.Count);
    }

    [Fact]
    public void MissingDurationForSelectedActivityIsReportedWithoutBorrowingAnotherDuration()
    {
        var result = StrengthPlanPreviewRule.Evaluate(Choices(), Data(
            new Dictionary<WorkoutActivityType, int>
            {
                [WorkoutActivityType.Walking] = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes
            }));

        Assert.Equal(StrengthPlanPreviewStatus.Limited, result.Status);
        Assert.Contains(StrengthPlanPreviewRule.SessionDurationMissingCode, result.LimitationCodes);
        Assert.Empty(result.Sessions);
    }

    [Fact]
    public void InvalidSavedActivityDurationFailsClosed()
    {
        var result = StrengthPlanPreviewRule.Evaluate(Choices(), Data(
            new Dictionary<WorkoutActivityType, int>
            {
                [WorkoutActivityType.GeneralStrength] = DurationTooShortMinutes + InvalidDurationOffsetMinutes
            }));

        Assert.Equal(StrengthPlanPreviewStatus.InvalidInput, result.Status);
        Assert.Contains(StrengthPlanPreviewRule.SessionDurationInvalidCode, result.LimitationCodes);
    }

    private static StrengthPlanPreviewChoices Choices(
        WorkoutActivityType activity = WorkoutActivityType.GeneralStrength) => new(
        activity,
        LocalDate,
        new ActivityTrainingHistory(activity.ToString(), CompletedWeeks,
            AverageSessionsPerWeek, LocalDate.AddDays(PreviousSessionDayOffset), true),
        WarmupMinutes: NoWarmupOrCooldownMinutes,
        CooldownMinutes: NoWarmupOrCooldownMinutes);

    private static StrengthPlanPreviewData Data(
        IReadOnlyDictionary<WorkoutActivityType, int>? durations = null,
        int secondsPerRep = SecondsPerRep,
        WorkoutActivityType activity = WorkoutActivityType.GeneralStrength)
    {
        var templates = StrengthTemplateCatalog.ForSchedule(SessionsPerWeekFor(activity));
        var requiredSlots = templates.SelectMany(template => template.Slots
            .Where(slot => slot.IsRequired)
            .Select(slot => (Template: template, Slot: slot))).ToArray();
        var catalog = requiredSlots.Select((item, index) => Entry(
            FirstExerciseId + index * ExerciseIdOffset,
            item.Slot.PreferredPatterns.First())).ToArray();
        var reviewedSlots = requiredSlots.Select((item, index) =>
            (Key: $"{item.Template.TemplateId}|{item.Slot.SlotId}", Id: FirstExerciseId + index * ExerciseIdOffset))
            .ToDictionary(item => item.Key,
                item => (IReadOnlySet<int>)new HashSet<int> { item.Id });
        var weekdays = new HashSet<DayOfWeek>
        {
            DayOfWeek.Monday,
            DayOfWeek.Wednesday,
            DayOfWeek.Friday
        };

        return new StrengthPlanPreviewData(
            UserGoal.MaintainHealth,
            Equipment.Bodyweight,
            InjuryFlag.None,
            false,
            new HashSet<WorkoutActivityType> { activity },
            weekdays,
            durations ?? new Dictionary<WorkoutActivityType, int>
            {
                [activity] = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes
            },
            null,
            catalog,
            new Dictionary<int, int>(),
            new HashSet<int>(),
            new Dictionary<int, int>(),
            new HashSet<int>(),
            catalog.ToDictionary(entry => entry.Candidate.ExerciseId, _ => secondsPerRep),
            catalog.ToDictionary(entry => entry.Candidate.ExerciseId,
                entry => entry.Candidate.Pattern.ToString()),
            reviewedSlots,
            reviewedSlots.Keys.ToDictionary(key => key, _ => "Reviewed slot conditions"));
    }

    private static int SessionsPerWeekFor(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.GeneralStrength => ActivityPrescriptionRule.GeneralStrengthSessionsPerWeek,
        WorkoutActivityType.HighVolumeStrength => ActivityPrescriptionRule.HighVolumeStrengthSessionsPerWeek,
        WorkoutActivityType.StrengthHighIntensity =>
            ActivityPrescriptionRule.StrengthHighIntensityMaximumSessionsPerWeekInitially,
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null)
    };

    private static StrengthPlanPreviewChoices EstablishedHighIntensityChoices() =>
        Choices(WorkoutActivityType.StrengthHighIntensity) with
        {
            ActivityHistory = new ActivityTrainingHistory(
                WorkoutActivityType.StrengthHighIntensity.ToString(),
                ActivityExperienceRule.MinimumConsistentWeeksForEstablished,
                ActivityExperienceRule.EstablishedSessionsPerWeek,
                LocalDate,
                true)
        };

    private static int[] HeavyCandidateIds(StrengthPlanPreviewData data) =>
        StrengthTemplateCatalog.ForSchedule(
                ActivityPrescriptionRule.StrengthHighIntensityMaximumSessionsPerWeekInitially)
            .SelectMany(template => template.Slots
                .Where(slot => slot is { IsRequired: true, IsHeavyMainInHighIntensity: true })
                .Select(slot => data.ReviewedAllowedExerciseIdsByTemplateSlot![
                    $"{template.TemplateId}|{slot.SlotId}"].Single()))
            .ToArray();

    private static StrengthExerciseCatalogEntry Entry(int exerciseId, MovementPattern pattern) => new(
        $"Exercise {exerciseId}",
        new ReviewedExerciseCandidate(exerciseId, "synthetic-catalog-v1", pattern,
            new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
            new HashSet<ActivityExperience> { ActivityExperience.Beginner, ActivityExperience.Established },
            new HashSet<ActivityDemandTag>(), true, true,
            new HashSet<ActivityDemandTag>(),
            new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
            SetupMinutes, new HashSet<int>(), InjuryFlag.None),
        []);
}
