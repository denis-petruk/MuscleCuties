using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutCatalogCoverageTests
{
    [Fact]
    public void Reviewed_snapshot_maps_every_required_template_slot_to_known_exercises()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindSnapshot()));
        var root = document.RootElement;
        Assert.Equal(StrengthTemplateCatalog.Version, root.GetProperty("templateVersion").GetString());
        var exerciseIds = root.GetProperty("exercises").EnumerateArray()
            .Select(exercise => exercise.GetProperty("sourceExerciseId").GetInt32()).ToHashSet();
        var reviewedSlots = root.GetProperty("templateSlots").EnumerateArray().ToArray();

        foreach (var template in StrengthTemplateCatalog.Templates)
        foreach (var slot in template.Slots.Where(slot => slot.IsRequired))
        {
            var identity = $"{template.TemplateId} / {StrengthTemplateCatalog.Version}";
            var review = Assert.Single(reviewedSlots, item =>
                item.GetProperty("templateIdAndVersion").GetString() == identity &&
                item.GetProperty("movementSlot").GetString() == slot.SlotId);
            Assert.Equal("Required", review.GetProperty("requiredOrOptional").GetString());
            var ids = review.GetProperty("candidateSourceExerciseIds").EnumerateArray()
                .Select(value => value.GetInt32()).ToArray();
            Assert.NotEmpty(ids);
            Assert.All(ids, id => Assert.Contains(id, exerciseIds));
        }
    }

    private static string FindSnapshot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "MuscleCuties.Infrastructure.PostgreSql",
                "Workout", "Planning", "ReviewData", "ReviewedExerciseCatalogV020.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Reviewed exercise catalog snapshot was not found.");
    }
}
