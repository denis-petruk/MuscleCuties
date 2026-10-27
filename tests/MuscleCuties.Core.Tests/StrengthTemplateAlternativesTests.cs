using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthTemplateAlternativesTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ExistingCatalogHasNoAlternateTemplateForTheSameSession(int days)
    {
        foreach (var template in StrengthTemplateCatalog.ForSchedule(days))
            Assert.Empty(StrengthTemplateCatalog.AlternativesForSchedule(days, template));
    }
}
