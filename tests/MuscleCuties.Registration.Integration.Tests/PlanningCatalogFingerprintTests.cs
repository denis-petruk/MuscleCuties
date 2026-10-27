using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class PlanningCatalogFingerprintTests
{
    private const int FirstExerciseId = 1;
    private const int SecondExerciseId = 2;
    [Fact]
    public void SafetyMetadataAndPreferenceChangesInvalidateFingerprint()
    {
        var exercise = new WorkoutExerciseDefinition { Id = FirstExerciseId, SeedCode = "synthetic", Name = "Synthetic" };
        var preference = new UserExercisePreferenceRecord { Id = Guid.NewGuid(), OriginalExerciseId = FirstExerciseId, PreferredExerciseId = FirstExerciseId };
        var initial = PlanningCatalogFingerprint.FromInputs([exercise], [], [preference], []);

        exercise.RestrictionMappingsReviewedV020 = true;
        var safetyChanged = PlanningCatalogFingerprint.FromInputs([exercise], [], [preference], []);
        preference.PreferredExerciseId = SecondExerciseId;
        var preferenceChanged = PlanningCatalogFingerprint.FromInputs([exercise], [], [preference], []);

        Assert.NotEqual(initial, safetyChanged);
        Assert.NotEqual(safetyChanged, preferenceChanged);
    }

    [Fact]
    public void InputOrderingDoesNotChangeFingerprint()
    {
        var first = new WorkoutExerciseDefinition { Id = FirstExerciseId, SeedCode = "first" };
        var second = new WorkoutExerciseDefinition { Id = SecondExerciseId, SeedCode = "second" };
        Assert.Equal(PlanningCatalogFingerprint.FromInputs([first, second], [], [], []),
            PlanningCatalogFingerprint.FromInputs([second, first], [], [], []));
    }
    [Fact]
    public void FloorValuesAndReviewEvidenceInvalidateFingerprint()
    {
        const int originalFloor = 4;
        const int revisedFloor = 5;
        var floor = new ReviewedTemplateFloorRow
        {
            TemplateId = "synthetic-template",
            TemplateVersion = "synthetic-version",
            MinimumWorkingSets = originalFloor,
            EvidenceReference = "synthetic-evidence-a"
        };
        var initial = PlanningCatalogFingerprint.FromInputs([], [], [], [floor]);
        floor.MinimumWorkingSets = revisedFloor;
        var doseChanged = PlanningCatalogFingerprint.FromInputs([], [], [], [floor]);
        floor.EvidenceReference = "synthetic-evidence-b";
        var evidenceChanged = PlanningCatalogFingerprint.FromInputs([], [], [], [floor]);

        Assert.NotEqual(initial, doseChanged);
        Assert.NotEqual(doseChanged, evidenceChanged);
        Assert.NotEqual(initial, PlanningCatalogFingerprint.FromInputs([], [], [], []));
    }
}
