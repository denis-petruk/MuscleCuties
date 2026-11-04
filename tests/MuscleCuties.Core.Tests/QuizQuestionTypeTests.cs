using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;

namespace MuscleCuties.Core.Tests;

public sealed class QuizQuestionTypeTests
{
    private const int CurrentCyclePhaseStorageValue = 13;
    private const int SessionDurationStorageValue = 14;
    private const int EquipmentStorageValue = 15;
    private const int GoalPaceStorageValue = 16;
    private const int AvailableWeekdaysStorageValue = 17;

    [Fact]
    public void CycleTrackingIsRemovedAndExistingStorageValuesStayStable()
    {
        Assert.DoesNotContain("CycleTrackingMode", Enum.GetNames<QuizQuestionType>());
        Assert.Null(typeof(CyclePhase).Assembly.GetType("MuscleCuties.Core.Models.Enums.Cycle.CycleTrackingMode"));
        Assert.Equal(CurrentCyclePhaseStorageValue, (int)QuizQuestionType.CurrentCyclePhase);
        Assert.Equal(SessionDurationStorageValue, (int)QuizQuestionType.SessionDuration);
        Assert.Equal(EquipmentStorageValue, (int)QuizQuestionType.Equipment);
        Assert.Equal(GoalPaceStorageValue, (int)QuizQuestionType.GoalPace);
        Assert.Equal(AvailableWeekdaysStorageValue, (int)QuizQuestionType.AvailableWeekdays);
    }

    [Fact]
    public void RetiredGlobalWorkoutDaysStaysStoredWhileSessionDurationAndAvailableWeekdaysAreActive()
    {
        Assert.False(QuizQuestionType.WorkoutDaysPerWeek.IsAvailableInCurrentOnboarding());
        Assert.False(QuizQuestionType.RetiredLegacy.IsAvailableInCurrentOnboarding());
        Assert.True(QuizQuestionType.SessionDuration.IsAvailableInCurrentOnboarding());
        Assert.True(QuizQuestionType.AvailableWeekdays.IsAvailableInCurrentOnboarding());
        Assert.True(QuizQuestionType.Equipment.IsAvailableInCurrentOnboarding());
    }
}
