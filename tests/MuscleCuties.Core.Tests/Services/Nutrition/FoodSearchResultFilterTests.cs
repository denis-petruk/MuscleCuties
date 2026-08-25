using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Quiz;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class FoodSearchResultFilterTests
{
    [Fact]
    public void PrepareFoodItems_FiltersMissingCaloriesAndDeduplicatesExactNames()
    {
        var foods = new[]
        {
            new FoodItem { Id = 1, Name = "CHICKEN", Calories = 120f, Protein = 22f },
            new FoodItem { Id = 2, Name = "Chicken", Calories = 130f, Protein = 24f, Fats = 3f },
            new FoodItem { Id = 3, Name = "Chicken, raw", Calories = 119f, Protein = 21f },
            new FoodItem { Id = 4, Name = "Chicken missing calories", Calories = 0f, Protein = 25f }
        };

        var results = FoodSearchResultFilter.PrepareFoodItems("chicken", foods);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, food => food.Name == "Chicken");
        Assert.Contains(results, food => food.Name == "Chicken, raw");
        Assert.DoesNotContain(results, food => food.Calories <= 0f);
    }

    [Fact]
    public void PrepareRemoteResults_DeduplicatesAndRanksConcreteMatches()
    {
        var results = new[]
        {
            new FdcFoodSearchResult { FdcId = 1, Description = "CHICKEN", DataType = "Branded" },
            new FdcFoodSearchResult { FdcId = 2, Description = "Chicken", DataType = "Foundation" },
            new FdcFoodSearchResult { FdcId = 3, Description = "Chicken breast, raw", DataType = "Foundation" },
            new FdcFoodSearchResult { FdcId = 4, Description = "Chicken noodle soup", DataType = "Survey (FNDDS)" },
            new FdcFoodSearchResult { FdcId = 5, Description = "Beef", DataType = "Foundation" }
        };

        var prepared = FoodSearchResultFilter.PrepareRemoteResults("chicken breast", results);

        var result = Assert.Single(prepared);
        Assert.Equal(3, result.FdcId);
    }

    [Fact]
    public void PrepareFoodItems_KeepsBrandedVariantsWithSameName()
    {
        var foods = new[]
        {
            new FoodItem { Id = 1, Name = "Chicken", Calories = 120f, Protein = 22f, DataType = "Foundation" },
            new FoodItem
            {
                Id = 2,
                Name = "Chicken",
                Calories = 130f,
                Protein = 20f,
                DataType = "Branded",
                BrandOwner = "Brand A",
                GtinUpc = "111"
            },
            new FoodItem
            {
                Id = 3,
                Name = "Chicken",
                Calories = 140f,
                Protein = 21f,
                DataType = "Branded",
                BrandOwner = "Brand B",
                GtinUpc = "222"
            }
        };

        var results = FoodSearchResultFilter.PrepareFoodItems("chicken", foods);

        Assert.Equal(3, results.Count);
        Assert.Contains(results, food => food.DataType == "Foundation");
        Assert.Contains(results, food => food.BrandOwner == "Brand A");
        Assert.Contains(results, food => food.BrandOwner == "Brand B");
    }

    [Fact]
    public void PrepareFoodItems_ReturnsAllMatchingOptionsWithoutAppLimit()
    {
        var foods = Enumerable
            .Range(1, 25)
            .Select(i => new FoodItem
            {
                Id = i,
                Name = $"Oats product {i}",
                Calories = 100f + i,
                DataType = "Foundation"
            });

        var results = FoodSearchResultFilter.PrepareFoodItems("oats", foods);

        Assert.Equal(25, results.Count);
    }

    [Fact]
    public void PrepareFoodItems_MatchesSingularAndPluralFoodNames()
    {
        var foods = new[]
        {
            new FoodItem { Id = 1, Name = "Oatmeal, dry", Calories = 389f, Protein = 16.9f },
            new FoodItem { Id = 2, Name = "Brown rice", Calories = 216f, Protein = 5f }
        };

        var results = FoodSearchResultFilter.PrepareFoodItems("oats", foods);

        var result = Assert.Single(results);
        Assert.Equal("Oatmeal, dry", result.Name);
    }

    [Fact]
    public void PrepareFoodItems_PrefersNameBrandAndUpcMatchesBeforeIngredientOnlyMatches()
    {
        var foods = new[]
        {
            new FoodItem { Id = 1, Name = "Chicken breast", Calories = 120f, Protein = 22f },
            new FoodItem
            {
                Id = 2,
                Name = "Protein bowl",
                Calories = 300f,
                Protein = 18f,
                Ingredients = "Rice, chicken stock, herbs"
            }
        };

        var results = FoodSearchResultFilter.PrepareFoodItems("chicken", foods);

        var result = Assert.Single(results);
        Assert.Equal("Chicken breast", result.Name);
    }

    [Fact]
    public void PrepareFoodItems_MatchesBrandAndUpcFields()
    {
        var foods = new[]
        {
            new FoodItem
            {
                Id = 1,
                Name = "Granola bar",
                Calories = 210f,
                DataType = "Branded",
                BrandOwner = "Acme Foods",
                GtinUpc = "123456789"
            },
            new FoodItem
            {
                Id = 2,
                Name = "Granola bar",
                Calories = 190f,
                DataType = "Branded",
                BrandOwner = "Other Foods",
                GtinUpc = "987654321"
            }
        };

        var brandResults = FoodSearchResultFilter.PrepareFoodItems("acme granola", foods);
        var upcResults = FoodSearchResultFilter.PrepareFoodItems("123456789", foods);

        Assert.Single(brandResults);
        Assert.Equal("Acme Foods", brandResults[0].BrandOwner);
        Assert.Single(upcResults);
        Assert.Equal("123456789", upcResults[0].GtinUpc);
    }

    [Fact]
    public void PrepareRemoteResults_KeepsBrandedVariantsWithSameDescription()
    {
        var results = new[]
        {
            new FdcFoodSearchResult { FdcId = 1, Description = "Granola bar", DataType = "Foundation" },
            new FdcFoodSearchResult
            {
                FdcId = 2,
                Description = "Granola bar",
                DataType = "Branded",
                BrandOwner = "Brand A",
                GtinUpc = "111"
            },
            new FdcFoodSearchResult
            {
                FdcId = 3,
                Description = "Granola bar",
                DataType = "Branded",
                BrandOwner = "Brand B",
                GtinUpc = "222"
            }
        };

        var prepared = FoodSearchResultFilter.PrepareRemoteResults("granola", results);

        Assert.Equal(3, prepared.Count);
        Assert.Contains(prepared, result => result.DataType == "Foundation");
        Assert.Contains(prepared, result => result.BrandOwner == "Brand A");
        Assert.Contains(prepared, result => result.BrandOwner == "Brand B");
    }

    [Fact]
    public void PrepareRemoteResults_MatchesBrandAndUpcFields()
    {
        var results = new[]
        {
            new FdcFoodSearchResult
            {
                FdcId = 1,
                Description = "Granola bar",
                DataType = "Branded",
                BrandOwner = "Acme Foods",
                GtinUpc = "123456789"
            },
            new FdcFoodSearchResult
            {
                FdcId = 2,
                Description = "Granola bar",
                DataType = "Branded",
                BrandOwner = "Other Foods",
                GtinUpc = "987654321"
            }
        };

        var brandResults = FoodSearchResultFilter.PrepareRemoteResults("acme granola", results);
        var upcResults = FoodSearchResultFilter.PrepareRemoteResults("123456789", results);

        Assert.Single(brandResults);
        Assert.Equal("Acme Foods", brandResults[0].BrandOwner);
        Assert.Single(upcResults);
        Assert.Equal("123456789", upcResults[0].GtinUpc);
    }

    [Fact]
    public void PrepareRemoteResults_RanksExactGenericFoodBeforeLongerMatches()
    {
        var results = new[]
        {
            new FdcFoodSearchResult { FdcId = 1, Description = "Chicken noodle soup", DataType = "Survey (FNDDS)" },
            new FdcFoodSearchResult { FdcId = 2, Description = "Chicken", DataType = "Foundation" },
            new FdcFoodSearchResult { FdcId = 3, Description = "Chicken breast, raw", DataType = "Foundation" }
        };

        var prepared = FoodSearchResultFilter.PrepareRemoteResults("chicken", results);

        Assert.Equal([2, 3, 1], prepared.Select(result => result.FdcId));
    }

    [Fact]
    public void PrepareRemoteResults_RanksRawWholeFoodBeforePreparedCarrotMatches()
    {
        var results = new[]
        {
            new FdcFoodSearchResult { FdcId = 1, Description = "Carrot juice", DataType = "Foundation" },
            new FdcFoodSearchResult { FdcId = 2, Description = "Babyfood, carrots", DataType = "Survey (FNDDS)" },
            new FdcFoodSearchResult { FdcId = 3, Description = "Carrots, raw", DataType = "Foundation" },
            new FdcFoodSearchResult { FdcId = 4, Description = "Carrot soup", DataType = "Survey (FNDDS)" }
        };

        var prepared = FoodSearchResultFilter.PrepareRemoteResults("carrot", results);

        Assert.Equal(3, prepared[0].FdcId);
    }

    [Fact]
    public void PrepareFoodItems_RanksRawWholeFoodBeforePreparedCarrotMatches()
    {
        var foods = new[]
        {
            new FoodItem { Id = 1, Name = "Carrot juice", Calories = 40f, DataType = "Foundation" },
            new FoodItem { Id = 2, Name = "Babyfood, carrots", Calories = 35f, DataType = "Survey (FNDDS)" },
            new FoodItem { Id = 3, Name = "Carrots, raw", Calories = 41f, DataType = "Foundation" }
        };

        var prepared = FoodSearchResultFilter.PrepareFoodItems("carrot", foods);

        Assert.Equal(3, prepared[0].Id);
    }

    [Fact]
    public void FormatFoodName_AllCaps_ReturnsTitleCase()
    {
        var result = FoodSearchResultFilter.FormatFoodName("CHICKEN BREAST");

        Assert.Equal("Chicken Breast", result);
    }
}
