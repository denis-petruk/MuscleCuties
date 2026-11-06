using System.Text.Json;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;

namespace MuscleCuties.Core.Tests;

public sealed class MicronutrientCoverageRuleTests
{
    [Theory]
    [InlineData(0, 0, MicronutrientCoverageStatus.KnownZero)]
    [InlineData(3, 0, MicronutrientCoverageStatus.Complete)]
    [InlineData(2, 1, MicronutrientCoverageStatus.Partial)]
    [InlineData(0, 2, MicronutrientCoverageStatus.Unavailable)]
    [InlineData(1, 4, MicronutrientCoverageStatus.Partial)]
    public void Classify_distinguishes_zero_coverage_from_a_known_zero(
        int covered, int uncovered, MicronutrientCoverageStatus expected)
    {
        Assert.Equal(expected, MicronutrientCoverageRule.Classify(covered, uncovered));
    }

    [Fact]
    public void Classify_rejects_negative_counts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MicronutrientCoverageRule.Classify(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MicronutrientCoverageRule.Classify(0, -1));
    }

    [Fact]
    public void Overview_response_without_coverage_keeps_the_field_absent()
    {
        var response = NewResponse();

        Assert.Null(response.MicronutrientCoverage);
    }

    [Fact]
    public void Additive_coverage_round_trips_without_changing_existing_values()
    {
        var response = NewResponse() with
        {
            MicronutrientCoverage = new MicronutrientCoverageDto(
                "Partial", 1, 1, ["Independent food entry"])
        };

        var json = JsonSerializer.Serialize(response);
        var restored = JsonSerializer.Deserialize<NutritionOverviewResponse>(json);

        Assert.NotNull(restored);
        Assert.Equal(2000f, restored!.Targets.Calories);
        Assert.Equal("Partial", restored.MicronutrientCoverage!.Status);
        Assert.Equal(1, restored.MicronutrientCoverage.CoveredItemCount);
        Assert.Equal(1, restored.MicronutrientCoverage.UncoveredItemCount);
        Assert.Equal(["Independent food entry"], restored.MicronutrientCoverage.UnavailableItemNames);
    }

    [Fact]
    public void An_older_response_without_the_additive_field_deserializes()
    {
        const string json = """
        {
          "Targets": { "Calories": 2000, "Protein": 130, "Carbs": 220, "Fats": 65 },
          "Consumed": { "Calories": 0, "Protein": 0, "Carbs": 0, "Fats": 0 },
          "CurrentPhase": "Unknown",
          "CurrentPhaseName": "",
          "PhaseFocusTitle": "",
          "PhaseFocusCopy": "",
          "Meals": [],
          "Micronutrients": [],
          "BreakfastPreference": "Savoury"
        }
        """;

        var restored = JsonSerializer.Deserialize<NutritionOverviewResponse>(json);

        Assert.NotNull(restored);
        Assert.Null(restored!.MicronutrientCoverage);
        Assert.Equal("Savoury", restored.BreakfastPreference);
    }

    private static NutritionOverviewResponse NewResponse() => new(
        new NutritionTargetMacrosDto(2000, 130, 220, 65),
        new NutritionConsumedMacrosDto(0, 0, 0, 0),
        "Unknown",
        string.Empty,
        string.Empty,
        string.Empty,
        [],
        [],
        "Savoury");
}
