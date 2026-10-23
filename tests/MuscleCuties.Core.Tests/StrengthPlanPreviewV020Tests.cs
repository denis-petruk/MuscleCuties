using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthPlanPreviewV020Tests
{
    private static readonly DateOnly LocalDate = new(2026, 10, 23);

    [Fact]
    public async Task Reviewed_Required_Slots_Produce_Deterministic_Policy_Preview()
    {
        var source = new Source(Profile());
        var useCase = new PreviewStrengthPlanV020UseCase(source);
        var request = Request();

        var first = await useCase.ExecuteAsync(Guid.NewGuid(), request);
        var second = await useCase.ExecuteAsync(Guid.NewGuid(), request);

        Assert.Equal("Feasible", first.Status);
        Assert.Single(first.Sessions);
        Assert.Equal([101, 102, 103, 104], first.Sessions[0].Slots
            .Where(slot => slot.IsRequired).Select(slot => slot.ExerciseId));
        Assert.Equal(first.Sessions[0].Slots.Select(slot => slot.ExerciseId),
            second.Sessions[0].Slots.Select(slot => slot.ExerciseId));
        Assert.True(first.Sessions[0].EstimatedMinutes > 0);
        Assert.Contains("OneDayFullBodyHasLimitedWeeklyCoverage", first.AdvisoryCodes);
    }

    [Fact]
    public async Task Missing_Restriction_Review_Leaves_Only_Affected_Strength_Preview_Limited()
    {
        var profile = Profile();
        var catalog = profile.Catalog.Select(entry => entry.Candidate.ExerciseId == 101
            ? entry with { Candidate = entry.Candidate with { RestrictionMappingsReviewed = false } }
            : entry).ToArray();
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(profile with { Catalog = catalog }));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("Limited", result.Status);
        Assert.Contains(result.Sessions[0].Slots,
            slot => slot.SlotId == "knee-dominant" && slot.ExerciseId is null);
    }

    [Fact]
    public async Task Four_Hard_Strength_Days_Offer_Three_Instead_Of_Silent_Overrun()
    {
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile()));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            StrengthDaysPerWeek = 4
        });

        Assert.Equal("Limited", result.Status);
        Assert.Empty(result.Sessions);
        Assert.Equal(3, result.FeasibleAlternativeStrengthDaysPerWeek);
        Assert.Contains("FourStrengthDaysExceedThreeHardDayWeeklyCap", result.LimitationCodes);
    }

    [Fact]
    public async Task Three_Initial_High_Intensity_Days_Offer_Two()
    {
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile()));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            Activity = WorkoutActivityType.StrengthHighIntensity,
            StrengthDaysPerWeek = 3
        });

        Assert.Equal("Limited", result.Status);
        Assert.Empty(result.Sessions);
        Assert.Equal(2, result.FeasibleAlternativeStrengthDaysPerWeek);
        Assert.Contains("HighIntensityStrengthLimitedToTwoInitialSessionsPerWeek",
            result.LimitationCodes);
    }

    [Fact]
    public async Task Strength_Timing_Does_Not_Invent_Warmup_Or_Cooldown_Duration()
    {
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile()));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request() with
        {
            WarmupMinutes = null
        });

        Assert.Equal("Limited", result.Status);
        Assert.Empty(result.Sessions);
        Assert.Contains("StrengthWarmupOrCooldownDurationMissing", result.LimitationCodes);
    }

    [Fact]
    public async Task Current_Symptoms_Block_Prescription_Despite_Reviewed_Catalog_Clearance()
    {
        var useCase = new PreviewStrengthPlanV020UseCase(new Source(Profile() with
        {
            HasCurrentSymptoms = true
        }));

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Request());

        Assert.Equal("Limited", result.Status);
        Assert.Contains("RestrictionOrSymptomRequiresReview", result.LimitationCodes);
    }

    private static StrengthPlanPreviewRequestV020 Request() => new(
        WorkoutActivityType.GeneralStrength, 1, LocalDate,
        new ActivityTrainingHistoryV020(nameof(WorkoutActivityType.GeneralStrength), 1, 1,
            LocalDate.AddDays(-1), true), WarmupMinutes: 0, CooldownMinutes: 0);

    private static StrengthPlanPreviewSourceV020 Profile()
    {
        var catalog = new[]
        {
            Entry(101, MovementPattern.SquatPattern),
            Entry(102, MovementPattern.HipHinge),
            Entry(103, MovementPattern.HorizontalPush),
            Entry(104, MovementPattern.HorizontalPull)
        };
        var template = StrengthTemplateCatalogV020.ForSchedule(1).Single();
        var allowed = template.Slots.Where(slot => slot.IsRequired)
            .Select((slot, index) => (Key: $"{template.TemplateId}|{slot.SlotId}", Id: 101 + index))
            .ToDictionary(item => item.Key,
                item => (IReadOnlySet<int>)new HashSet<int> { item.Id });
        var conditions = allowed.ToDictionary(item => item.Key, _ => "Reviewed slot conditions");
        return new StrengthPlanPreviewSourceV020(UserGoal.MaintainHealth, Equipment.Bodyweight, 30,
            InjuryFlag.None, false, catalog, new Dictionary<int, int>(), new HashSet<int>(),
            new Dictionary<int, int>(), new HashSet<int>(),
            catalog.ToDictionary(entry => entry.Candidate.ExerciseId, _ => 4),
            catalog.ToDictionary(entry => entry.Candidate.ExerciseId,
                entry => entry.Candidate.Pattern.ToString()),
            allowed, conditions);
    }

    private static StrengthExerciseCatalogEntryV020 Entry(int id, MovementPattern pattern) => new(
        $"Exercise {id}", new ReviewedExerciseCandidate(id, "synthetic-catalog-v1", pattern,
            new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
            new HashSet<ActivityExperienceV020> { ActivityExperienceV020.Beginner },
            new HashSet<ActivityDemandTag>(), true, true, new HashSet<ActivityDemandTag>(),
            new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
            1, new HashSet<int>(), InjuryFlag.None), []);

    private sealed class Source(StrengthPlanPreviewSourceV020 profile) : IStrengthPlanPreviewSourceV020
    {
        public Task<StrengthPlanPreviewSourceV020?> LoadAsync(Guid userId,
            CancellationToken cancellationToken) => Task.FromResult<StrengthPlanPreviewSourceV020?>(profile);
    }
}
