using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class ReviewedTemplateFloorSourceTests
{
    private const int SyntheticWorkingSets = 6;
    private const int SyntheticExerciseCount = 3;
    private const int SyntheticSlotSets = 1;
    private const int SyntheticSha256HexLength = 64;

    private static ReviewedTemplateFloorRow Row()
    {
        var template = StrengthTemplateCatalog.Templates.First();
        return new ReviewedTemplateFloorRow
        {
            TemplateId = template.TemplateId,
            TemplateVersion = template.Version,
            MinimumWorkingSets = SyntheticWorkingSets,
            MinimumExercises = SyntheticExerciseCount,
            SlotFloorsJson = JsonSerializer.Serialize(template.Slots.ToDictionary(slot => slot.SlotId, _ => SyntheticSlotSets)),
            Reviewer = "synthetic-reviewer",
            EvidenceReference = "synthetic-evidence",
            EvidenceSha256 = new string('a', SyntheticSha256HexLength),
            ReviewedOn = new DateOnly(2026, 1, 1)
        };
    }

    [Fact]
    public void CompleteMatchingReviewMapsToCoreFloor()
    {
        var row = Row();
        var floors = ReviewedTemplateFloorSource.Map([row]);
        Assert.True(floors.ContainsKey(row.TemplateId));
        Assert.Equal(SyntheticWorkingSets, floors[row.TemplateId].MinimumWorkingSets);
    }

    [Fact]
    public void MissingEvidenceOrMismatchedTemplateVersionCannotEnableFloor()
    {
        var missingEvidence = Row();
        missingEvidence.EvidenceReference = " ";
        Assert.Empty(ReviewedTemplateFloorSource.Map([missingEvidence]));
        var wrongVersion = Row();
        wrongVersion.TemplateVersion = "synthetic-other-version";
        Assert.Empty(ReviewedTemplateFloorSource.Map([wrongVersion]));
    }

    [Fact]
    public void IncompleteOrMalformedSlotReviewCannotEnableFloor()
    {
        var row = Row();
        row.SlotFloorsJson = "{}";
        Assert.Empty(ReviewedTemplateFloorSource.Map([row]));
        row.SlotFloorsJson = "not-json";
        Assert.Empty(ReviewedTemplateFloorSource.Map([row]));
    }
}
