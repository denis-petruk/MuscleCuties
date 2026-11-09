using MuscleCuties.UseCases.Features.Dashboard.CheckIn;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// S-08: untouched check-in controls are never submitted as self-reported answers. The view model
/// builds its request through <see cref="DailyCheckInAnswers"/>; the source guards below pin that
/// wiring because the MAUI project is not referenced by the backend test gate.
/// </summary>
public sealed class DailyCheckInAnswersTests
{
    private const string TimeZone = "UTC";
    private static readonly DateOnly Today = new(2026, 11, 8);
    private static readonly DailyCheckInAnswers Complete = new(
        SleepHours: 6.5, SleepQuality: 2, Energy: 1, Soreness: 4, PainLevel: 0, HasBloating: false);

    [Fact]
    public void Nothing_is_preselected()
    {
        var untouched = new DailyCheckInAnswers();

        Assert.False(untouched.IsComplete);
        Assert.Null(untouched.ToRequest(Today, TimeZone));
    }

    public static TheoryData<DailyCheckInAnswers> EachMissingAnswer => new()
    {
        Complete with { SleepHours = null },
        Complete with { SleepQuality = null },
        Complete with { Energy = null },
        Complete with { Soreness = null },
        Complete with { PainLevel = null },
        Complete with { HasBloating = null }
    };

    [Theory]
    [MemberData(nameof(EachMissingAnswer))]
    public void Any_missing_required_answer_blocks_submission(DailyCheckInAnswers answers)
    {
        Assert.False(answers.IsComplete);
        Assert.Null(answers.ToRequest(Today, TimeZone));
    }

    [Fact]
    public void Explicit_answers_are_sent_as_given_and_weight_stays_optional()
    {
        var request = Assert.IsType<UseCases.Features.Dashboard.CheckIn.Contracts.SubmitDailyCheckInRequest>(
            Complete.ToRequest(Today, TimeZone));

        Assert.Equal(6.5, request.SleepHours);
        Assert.Equal(2, request.PlanningSleepQuality);
        Assert.Equal(1, request.EnergyLevel);
        Assert.Equal(4, request.PlanningSoreness);
        Assert.Equal(0, request.PainLevel);
        Assert.False(request.HasBloating);
        Assert.Null(request.BodyWeightKg);
        Assert.Equal(TimeZone, request.PlanningTimeZoneId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Out_of_range_ratings_are_not_answers(int rating)
    {
        Assert.False((Complete with { Soreness = rating }).IsComplete);
        Assert.False((Complete with { PainLevel = rating == 0 ? -1 : 4 }).IsComplete);
    }

    [Fact]
    public void View_model_starts_unanswered_and_never_derives_stress_from_energy()
    {
        var source = ViewModelSource();

        Assert.DoesNotContain("PlanningStress = EnergyLevel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PlanningStress = value", source, StringComparison.Ordinal);
        Assert.DoesNotContain("readiness?.Stress ?? readiness?.Energy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_sleepHours = 7", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_planningSoreness = MinimumPlanningScaleValue", source, StringComparison.Ordinal);
        Assert.Contains("private int? _painLevel;", source, StringComparison.Ordinal);
        Assert.Contains("private bool? _hasBloating;", source, StringComparison.Ordinal);
        Assert.Contains("Answers.ToRequest(", source, StringComparison.Ordinal);
        Assert.Contains("Answers.IsComplete", source, StringComparison.Ordinal);
    }

    private static string ViewModelSource()
    {
        var current = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(current, "MuscleCuties.sln")))
            current = Directory.GetParent(current)?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        return File.ReadAllText(Path.Combine(current, "src", "MuscleCuties.UI", "ViewModels", "Dashboard",
            "DailyCheckInViewModel.cs"));
    }
}
