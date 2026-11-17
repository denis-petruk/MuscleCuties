using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class V2PrescriptionDoseProjectionTests
{
    [Fact]
    public void Occurrence_adjustment_preserves_v2_metadata_and_renders_the_saved_reduced_dose()
    {
        var original = new GenerationSessionV2(0, DayOfWeek.Monday, new DateOnly(2026, 11, 16),
            0, 5, 40m, [new GenerationExerciseV2(0, 17, "Synthetic exercise", 1, 3, 8, 12,
                2, 90, "CalibrationRequired", null, 1, true, "InitialLoadCalibrationRequired")]);
        var adjusted = new PlannedExerciseSnapshot(Guid.NewGuid(), 17, 2, 0, 8, null, true)
            { TargetRepetitionsInReserve = 4 };

        var result = AcceptedGenerationPlanV2.ApplyOccurrenceDose(original, [adjusted]);
        var exercise = Xunit.Assert.Single(result!.Exercises);
        Xunit.Assert.Equal(2, exercise.WorkingSets);
        Xunit.Assert.Equal(4, exercise.TargetRir);
        Xunit.Assert.Equal(1, exercise.WarmUpSets);
        Xunit.Assert.Equal(1, exercise.SupersetGroupId);
        Xunit.Assert.True(exercise.IsUnilateral);
        Xunit.Assert.Equal("CalibrationRequired", exercise.LoadStatus);
        Xunit.Assert.Null(exercise.PrescribedLoadKg);
        Xunit.Assert.Equal(3, Xunit.Assert.Single(original.Exercises).WorkingSets);
    }
}
