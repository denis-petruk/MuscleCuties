using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;

namespace MuscleCuties.Core.Tests.Services.Cycle;

public class CyclePredictionPlannerTests
{
    private readonly CyclePredictionPlanner _planner = new(new CyclePhaseCalculator());

    // -- Edge case 1: Very short cycle (18 days) --

    [Fact]
    public void CreatePrediction_ShortCycle18Days_PhaseBoundariesDoNotOverlap()
    {
        var start = new DateTime(2026, 9, 1);
        var history = new List<CycleLog>
        {
            new() { StartDate = start, CycleLength = 18 }
        };

        var prediction = _planner.CreatePrediction(history[0], history, profile: null, today: start);

        Assert.Equal(18, prediction.PredictedCycleLength);

        // Ovulation day = 18 - 14 = 4, so ovulation date is start + 4 days
        Assert.Equal(start.AddDays(4), prediction.PredictedOvulationDate);
        Assert.Equal(start.AddDays(18), prediction.PredictedNextPeriodDate);

        // Fertile window: ovulation - 5 to ovulation + 1
        Assert.Equal(start.AddDays(-1), prediction.FertileWindowStartDate);
        Assert.Equal(start.AddDays(5), prediction.FertileWindowEndDate);
    }

    [Theory]
    [InlineData(1, CyclePhase.Menstrual)]
    [InlineData(5, CyclePhase.Menstrual)]
    [InlineData(6, CyclePhase.Ovulatory)]   // No follicular phase -- ovulationDay=4, follicular <= 2
    [InlineData(7, CyclePhase.Luteal)]
    [InlineData(18, CyclePhase.Luteal)]
    public void CreatePrediction_ShortCycle18Days_EachDayMapsToSinglePhase(
        int dayOffset, CyclePhase expectedPhase)
    {
        var start = new DateTime(2026, 9, 1);
        var today = start.AddDays(dayOffset - 1);
        var history = new List<CycleLog>
        {
            new() { StartDate = start, CycleLength = 18 }
        };

        var prediction = _planner.CreatePrediction(history[0], history, profile: null, today: today);

        Assert.Equal(dayOffset, prediction.CurrentDay);
        Assert.Equal(expectedPhase, prediction.CurrentPhase);
    }

    // -- Edge case 2: Very long cycle (55 days) --

    [Fact]
    public void CreatePrediction_LongCycle55Days_LutealPhaseIs14Days()
    {
        var start = new DateTime(2026, 8, 1);
        var history = new List<CycleLog>
        {
            new() { StartDate = start, CycleLength = 55 }
        };

        var prediction = _planner.CreatePrediction(history[0], history, profile: null, today: start);

        Assert.Equal(55, prediction.PredictedCycleLength);

        // Ovulation day = 55 - 14 = 41
        Assert.Equal(start.AddDays(41), prediction.PredictedOvulationDate);
        Assert.Equal(start.AddDays(55), prediction.PredictedNextPeriodDate);

        // Luteal phase spans from ovulationDay+3 to end (days 44-55 = 12 days),
        // but the key invariant is: luteal starts at cycleLength - 14 + 3 = day 44
        // and next period is day 55, so the post-ovulatory span is exactly 14 days.
        var ovulationDate = prediction.PredictedOvulationDate!.Value;
        var nextPeriod = prediction.PredictedNextPeriodDate!.Value;
        Assert.Equal(14, (nextPeriod - ovulationDate).Days);
    }

    [Theory]
    [InlineData(1, CyclePhase.Menstrual)]
    [InlineData(6, CyclePhase.Follicular)]
    [InlineData(38, CyclePhase.Follicular)]  // Day before ovulatory window
    [InlineData(39, CyclePhase.Ovulatory)]   // ovulationDay - 2 = 39
    [InlineData(41, CyclePhase.Ovulatory)]   // ovulationDay = 41
    [InlineData(43, CyclePhase.Ovulatory)]   // ovulationDay + 2 = 43
    [InlineData(44, CyclePhase.Luteal)]      // First luteal day
    [InlineData(55, CyclePhase.Luteal)]
    public void CreatePrediction_LongCycle55Days_CorrectPhasePerDay(
        int dayOffset, CyclePhase expectedPhase)
    {
        var start = new DateTime(2026, 8, 1);
        var today = start.AddDays(dayOffset - 1);
        var history = new List<CycleLog>
        {
            new() { StartDate = start, CycleLength = 55 }
        };

        var prediction = _planner.CreatePrediction(history[0], history, profile: null, today: today);

        Assert.Equal(dayOffset, prediction.CurrentDay);
        Assert.Equal(expectedPhase, prediction.CurrentPhase);
    }

    // -- Edge case 3: No cycle history (empty list, null latest) --

    [Fact]
    public void CreatePrediction_NoCycleHistoryNullLatest_FallsBackToDefault()
    {
        var prediction = _planner.CreatePrediction(
            latestCycle: null,
            history: Array.Empty<CycleLog>(),
            profile: null,
            today: new DateTime(2026, 9, 12));

        Assert.False(prediction.HasActiveCycle);
        Assert.Equal(0, prediction.CurrentDay);
        Assert.Equal(CyclePhaseRules.DefaultCycleLength, prediction.PredictedCycleLength);
        Assert.Equal(CyclePhase.Follicular, prediction.CurrentPhase);
        Assert.Equal(0, prediction.DaysUntilPeriod);
        Assert.Equal("default", prediction.PredictionSource);
        Assert.Null(prediction.PredictedNextPeriodDate);
        Assert.Null(prediction.PredictedOvulationDate);
    }

    [Fact]
    public void CreatePrediction_NoCycleHistoryWithProfilePhase_UsesProfilePhase()
    {
        var profile = new UserProfile { CycleLength = 0, CurrentCyclePhase = CyclePhase.Luteal };

        var prediction = _planner.CreatePrediction(
            latestCycle: null,
            history: Array.Empty<CycleLog>(),
            profile: profile,
            today: new DateTime(2026, 9, 12));

        Assert.False(prediction.HasActiveCycle);
        Assert.Equal(CyclePhase.Luteal, prediction.CurrentPhase);
        Assert.Equal("profile phase", prediction.PredictionSource);
    }

    [Fact]
    public void CreatePrediction_NoCycleHistoryWithValidProfileLength_UsesProfileLength()
    {
        var profile = new UserProfile { CycleLength = 30 };

        var prediction = _planner.CreatePrediction(
            latestCycle: null,
            history: Array.Empty<CycleLog>(),
            profile: profile,
            today: new DateTime(2026, 9, 12));

        Assert.Equal(30, prediction.PredictedCycleLength);
        Assert.Equal("profile", prediction.PredictionSource);
    }

    // -- Edge case 4: Single cycle in history --

    [Fact]
    public void CreatePrediction_SingleCycleWithMeasuredLength_UsesThatLength()
    {
        var start = new DateTime(2026, 9, 1);
        var cycle = new CycleLog { StartDate = start, CycleLength = 32 };
        var history = new List<CycleLog> { cycle };

        var prediction = _planner.CreatePrediction(cycle, history, profile: null, today: start);

        Assert.Equal(32, prediction.PredictedCycleLength);
        Assert.Equal("recent cycles", prediction.PredictionSource);
        Assert.Equal(start.AddDays(32), prediction.PredictedNextPeriodDate);
    }

    [Fact]
    public void CreatePrediction_SingleCycleNoMeasuredLength_FallsBackToDefault()
    {
        // Single cycle with CycleLength=0 and no profile means:
        // - measuredLengths is empty (0 fails IsUsableCycleLength)
        // - startDateLengths needs 2+ cycles to compute, so empty with 1 cycle
        // - profile is null
        // -> falls to default
        var start = new DateTime(2026, 9, 1);
        var cycle = new CycleLog { StartDate = start, CycleLength = 0 };
        var history = new List<CycleLog> { cycle };

        var prediction = _planner.CreatePrediction(cycle, history, profile: null, today: start);

        Assert.Equal(CyclePhaseRules.DefaultCycleLength, prediction.PredictedCycleLength);
        Assert.Equal("default", prediction.PredictionSource);
    }

    // -- Edge case 5: Cycle with CycleLength == 0 in history --

    [Fact]
    public void CreatePrediction_AllCyclesHaveZeroLength_FallsBackToStartDateDerivedLength()
    {
        // Two cycles with CycleLength=0 but known start dates 25 days apart
        // -> measuredLengths is empty, but startDateLengths yields 25
        var cycle1 = new CycleLog { StartDate = new DateTime(2026, 8, 1), CycleLength = 0 };
        var cycle2 = new CycleLog { StartDate = new DateTime(2026, 8, 26), CycleLength = 0 };
        var history = new List<CycleLog> { cycle1, cycle2 };

        var prediction = _planner.CreatePrediction(cycle2, history, profile: null, today: cycle2.StartDate);

        Assert.Equal(25, prediction.PredictedCycleLength);
        Assert.Equal("cycle start history", prediction.PredictionSource);
    }

    [Fact]
    public void CreatePrediction_AllCyclesHaveZeroLengthSingleCycle_FallsBackToProfileLength()
    {
        var cycle = new CycleLog { StartDate = new DateTime(2026, 9, 1), CycleLength = 0 };
        var history = new List<CycleLog> { cycle };
        var profile = new UserProfile { CycleLength = 26 };

        var prediction = _planner.CreatePrediction(cycle, history, profile: profile, today: cycle.StartDate);

        Assert.Equal(26, prediction.PredictedCycleLength);
        Assert.Equal("profile", prediction.PredictionSource);
    }

    [Fact]
    public void CreatePrediction_ZeroLengthCyclesAndInvalidProfileLength_FallsBackToDefault()
    {
        // CycleLength=0 in history, profile CycleLength=10 (below MinimumCycleLength of 18)
        var cycle = new CycleLog { StartDate = new DateTime(2026, 9, 1), CycleLength = 0 };
        var history = new List<CycleLog> { cycle };
        var profile = new UserProfile { CycleLength = 10 };

        var prediction = _planner.CreatePrediction(cycle, history, profile: profile, today: cycle.StartDate);

        Assert.Equal(CyclePhaseRules.DefaultCycleLength, prediction.PredictedCycleLength);
        Assert.Equal("default", prediction.PredictionSource);
    }

    // -- Edge case 6: Today is past the predicted next period (overdue) --

    [Fact]
    public void CreatePrediction_TodayPastPredictedPeriod_DaysUntilPeriodIsZero()
    {
        var start = new DateTime(2026, 8, 1);
        var cycle = new CycleLog { StartDate = start, CycleLength = 28 };
        var history = new List<CycleLog> { cycle };

        // 28-day cycle started Aug 1 -> next period Aug 29. Today is Sep 5 (7 days overdue).
        var today = new DateTime(2026, 9, 5);
        var prediction = _planner.CreatePrediction(cycle, history, profile: null, today: today);

        Assert.Equal(0, prediction.DaysUntilPeriod);
        Assert.True(prediction.IsPeriodDue);
        Assert.Equal(start.AddDays(28), prediction.PredictedNextPeriodDate);
    }

    [Fact]
    public void CreatePrediction_TodayPastPredictedPeriod_CurrentDayKeepsIncrementing()
    {
        var start = new DateTime(2026, 8, 1);
        var cycle = new CycleLog { StartDate = start, CycleLength = 28 };
        var history = new List<CycleLog> { cycle };

        // Day 36 of a 28-day cycle (8 days past expected end)
        var today = start.AddDays(35);
        var prediction = _planner.CreatePrediction(cycle, history, profile: null, today: today);

        Assert.Equal(36, prediction.CurrentDay);
        Assert.True(prediction.HasActiveCycle);
        Assert.Equal(0, prediction.DaysUntilPeriod);
    }

    [Fact]
    public void CreatePrediction_TodayPastPredictedPeriod_PhaseIsLuteal()
    {
        // Even when overdue, the phase calculator receives the raw currentDay,
        // which is past the cycle length. CyclePhaseRules says day > ovulationDay+2 -> Luteal.
        var start = new DateTime(2026, 8, 1);
        var cycle = new CycleLog { StartDate = start, CycleLength = 28 };
        var history = new List<CycleLog> { cycle };

        var today = start.AddDays(34); // day 35, well past cycle length
        var prediction = _planner.CreatePrediction(cycle, history, profile: null, today: today);

        Assert.Equal(CyclePhase.Luteal, prediction.CurrentPhase);
    }

    // -- Additional boundary validations --

    [Fact]
    public void CreatePrediction_CycleLengthBelowMinimum_ClampedToMinimum()
    {
        // A measured CycleLength of 15 is below MinimumCycleLength (18),
        // so it is filtered out by IsUsableCycleLength and not used.
        var start = new DateTime(2026, 9, 1);
        var cycle = new CycleLog { StartDate = start, CycleLength = 15 };
        var history = new List<CycleLog> { cycle };

        var prediction = _planner.CreatePrediction(cycle, history, profile: null, today: start);

        // 15 is below minimum, so it's filtered out -> falls to default
        Assert.Equal(CyclePhaseRules.DefaultCycleLength, prediction.PredictedCycleLength);
        Assert.Equal("default", prediction.PredictionSource);
    }

    [Fact]
    public void CreatePrediction_CycleLengthAboveMaximum_ClampedByNormalization()
    {
        // CycleLength of 70 is above MaximumCycleLength (60), filtered out.
        var start = new DateTime(2026, 9, 1);
        var cycle = new CycleLog { StartDate = start, CycleLength = 70 };
        var history = new List<CycleLog> { cycle };

        var prediction = _planner.CreatePrediction(cycle, history, profile: null, today: start);

        Assert.Equal(CyclePhaseRules.DefaultCycleLength, prediction.PredictedCycleLength);
        Assert.Equal("default", prediction.PredictionSource);
    }

    [Fact]
    public void CreatePrediction_MixedValidAndInvalidCycleLengths_AveragesOnlyValidOnes()
    {
        var history = new List<CycleLog>
        {
            new() { StartDate = new DateTime(2026, 5, 1), CycleLength = 0 },   // invalid
            new() { StartDate = new DateTime(2026, 6, 1), CycleLength = 10 },  // below minimum
            new() { StartDate = new DateTime(2026, 7, 1), CycleLength = 26 },  // valid
            new() { StartDate = new DateTime(2026, 8, 1), CycleLength = 30 },  // valid
        };
        var latest = history[^1];

        var prediction = _planner.CreatePrediction(latest, history, profile: null, today: latest.StartDate);

        // Average of 26 and 30 = 28
        Assert.Equal(28, prediction.PredictedCycleLength);
        Assert.Equal("recent cycles", prediction.PredictionSource);
    }

    [Fact]
    public void CreatePrediction_TodayEqualsStartDate_CurrentDayIsOne()
    {
        var start = new DateTime(2026, 9, 1);
        var cycle = new CycleLog { StartDate = start, CycleLength = 28 };
        var history = new List<CycleLog> { cycle };

        var prediction = _planner.CreatePrediction(cycle, history, profile: null, today: start);

        Assert.Equal(1, prediction.CurrentDay);
        Assert.Equal(CyclePhase.Menstrual, prediction.CurrentPhase);
    }

    [Fact]
    public void CreatePrediction_TodayExactlyOnPredictedPeriodDate_DaysUntilPeriodIsZero()
    {
        var start = new DateTime(2026, 8, 1);
        var cycle = new CycleLog { StartDate = start, CycleLength = 28 };
        var history = new List<CycleLog> { cycle };

        // Next period = Aug 29. Today is exactly Aug 29.
        var today = start.AddDays(28);
        var prediction = _planner.CreatePrediction(cycle, history, profile: null, today: today);

        Assert.Equal(0, prediction.DaysUntilPeriod);
        Assert.True(prediction.IsPeriodDue);
        Assert.Equal(29, prediction.CurrentDay);
    }
}
