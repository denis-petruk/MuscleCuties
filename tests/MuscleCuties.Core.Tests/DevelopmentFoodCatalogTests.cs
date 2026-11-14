using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

namespace MuscleCuties.Core.Tests;

public sealed class DevelopmentFoodCatalogTests
{
    [Fact]
    public void The_development_catalogue_answers_a_generic_query()
    {
        var results = DevelopmentFoodCatalog.Search("chicken");

        Assert.NotEmpty(results);
        Assert.Contains(results, candidate =>
            candidate.Name.Contains("Chicken", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_development_catalogue_matches_case_insensitively_and_on_partial_words()
    {
        var results = DevelopmentFoodCatalog.Search("CHICK");

        Assert.NotEmpty(results);
    }

    [Fact]
    public void The_development_catalogue_is_empty_for_a_blank_query()
    {
        Assert.Empty(DevelopmentFoodCatalog.Search("   "));
    }

    [Fact]
    public void Every_development_candidate_carries_macros_so_the_result_row_renders()
    {
        var results = DevelopmentFoodCatalog.Search("rice");

        var item = Assert.Single(results);
        Assert.True(item.Calories is > 0);
        Assert.True(item.Protein is > 0);
        Assert.False(string.IsNullOrWhiteSpace(item.Name));
    }
}
