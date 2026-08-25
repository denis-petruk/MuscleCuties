using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Quiz;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class FdcFoodMapperTests
{
    [Fact]
    public void ApplyToFoodItem_FdcDetail_MapsSupportedNutrients()
    {
        var syncedAt = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        var detail = BuildDetail(
            fdcId: 173904,
            name: "Oats, rolled, dry",
            (1008, 389f),
            (1003, 16.9f),
            (1005, 66.3f),
            (1004, 6.9f),
            (1079, 10.6f),
            (1089, 4.7f),
            (1178, 0f),
            (1162, 0f),
            (1114, 0f),
            (1104, 10f),
            (1175, 0.12f),
            (1177, 56f),
            (1087, 54f),
            (1090, 177f),
            (1095, 3.97f),
            (1092, 429f));

        var item = FdcFoodMapper.ApplyToFoodItem(detail, syncedAt: syncedAt);

        Assert.Equal(173904, item.FdcId);
        Assert.Equal("Oats, rolled, dry", item.Name);
        Assert.Equal(389f, item.Calories);
        Assert.Equal(16.9f, item.Protein);
        Assert.Equal(66.3f, item.Carbs);
        Assert.Equal(6.9f, item.Fats);
        Assert.Equal(10.6f, item.Fiber);
        Assert.Equal(4.7f, item.Iron);
        Assert.Equal(10f, item.VitaminA);
        Assert.Equal(56f, item.Folate);
        Assert.Equal(177f, item.Magnesium);
        Assert.Equal(429f, item.Potassium);
        Assert.False(item.IsCustom);
        Assert.Equal("Foundation", item.DataType);
        Assert.Equal(syncedAt, item.LastSyncedAt);
        Assert.Equal(syncedAt, item.CreatedAt);
        Assert.Equal(syncedAt, item.UpdatedAt);
    }

    [Fact]
    public void ApplyToFoodItem_BrandedDetail_MapsFoodIdentityMetadata()
    {
        var detail = BuildDetail(700001, "Protein bar", (1008, 220f), (1003, 20f));
        detail.DataType = "Branded";
        detail.BrandOwner = "Acme Nutrition";
        detail.BrandName = "Acme";
        detail.GtinUpc = "123456789";
        detail.Ingredients = "Oats, whey protein";
        detail.ServingSize = 50f;
        detail.ServingSizeUnit = "g";

        var item = FdcFoodMapper.ApplyToFoodItem(detail);

        Assert.True(item.IsBranded);
        Assert.Equal("Acme Nutrition", item.BrandOwner);
        Assert.Equal("Acme", item.BrandName);
        Assert.Equal("123456789", item.GtinUpc);
        Assert.Equal("Oats, whey protein", item.Ingredients);
        Assert.Equal(50f, item.ServingSize);
        Assert.Equal("g", item.ServingSizeUnit);
    }

    [Fact]
    public void ApplyToFoodItem_FdcServingData_StoresServingOptions()
    {
        var detail = BuildDetail(700003, "Prepared carrots", (1008, 41f), (1003, 0.9f));
        detail.ServingSize = 85f;
        detail.ServingSizeUnit = "g";
        detail.PackageWeight = "NET WT 10 OZ (283 g)";
        detail.FoodPortions =
        [
            new FdcFoodPortion
            {
                Amount = 1f,
                GramWeight = 156f,
                MeasureUnit = new FdcMeasureUnit { Name = "cup", Abbreviation = "cup" }
            }
        ];

        var item = FdcFoodMapper.ApplyToFoodItem(detail);

        var options = FoodServingOptions.ReadOptions(item.ServingOptionsJson);
        Assert.Contains(options, option => option.Label == "serving" && option.Grams == 85f);
        Assert.Contains(options, option => option.Label == "container" && option.Grams == 283f);
        Assert.Contains(options, option => option.Label == "cup" && option.Grams == 156f);
    }

    [Fact]
    public void HasNutrientChanges_SameValues_ReturnsFalse()
    {
        var detail = BuildDetail(331960, "Chicken breast", (1008, 120f), (1003, 22.5f));
        var item = FdcFoodMapper.ApplyToFoodItem(detail);

        Assert.False(FdcFoodMapper.HasNutrientChanges(item, detail));
    }

    [Fact]
    public void HasNutrientChanges_ChangedValues_ReturnsTrue()
    {
        var detail = BuildDetail(331960, "Chicken breast", (1008, 120f), (1003, 22.5f));
        var item = FdcFoodMapper.ApplyToFoodItem(detail);
        var changed = BuildDetail(331960, "Chicken breast", (1008, 125f), (1003, 22.5f));

        Assert.True(FdcFoodMapper.HasNutrientChanges(item, changed));
    }

    [Fact]
    public void ApplyToFoodItem_SearchNutrientShape_MapsMacroValues()
    {
        var detail = new FdcFoodDetail
        {
            FdcId = 170393,
            Description = "Carrots, raw",
            FoodNutrients =
            [
                new() { NutrientId = 1008, Value = 41f },
                new() { NutrientId = 1003, Value = 0.93f },
                new() { NutrientId = 1005, Value = 9.58f },
                new() { NutrientId = 1004, Value = 0.24f }
            ]
        };

        var item = FdcFoodMapper.ApplyToFoodItem(detail);

        Assert.Equal(41f, item.Calories);
        Assert.Equal(0.93f, item.Protein);
        Assert.Equal(9.58f, item.Carbs);
        Assert.Equal(0.24f, item.Fats);
    }

    [Fact]
    public void ApplyToFoodItem_AbridgedNumberShape_MapsMacroValues()
    {
        var detail = new FdcFoodDetail
        {
            FdcId = 170393,
            Description = "Carrots, raw",
            FoodNutrients =
            [
                new() { Number = "208", Amount = 41f },
                new() { Number = "203", Amount = 0.93f },
                new() { Number = "205", Amount = 9.58f },
                new() { Number = "204", Amount = 0.24f }
            ]
        };

        var item = FdcFoodMapper.ApplyToFoodItem(detail);

        Assert.Equal(41f, item.Calories);
        Assert.Equal(0.93f, item.Protein);
        Assert.Equal(9.58f, item.Carbs);
        Assert.Equal(0.24f, item.Fats);
    }

    [Fact]
    public void ApplyToFoodItem_AtwaterEnergyShape_MapsCalories()
    {
        var detail = new FdcFoodDetail
        {
            FdcId = 173904,
            Description = "Oats, rolled, dry",
            FoodNutrients =
            [
                new() { NutrientId = 2047, Value = 389f },
                new() { NutrientId = 1003, Value = 16.9f },
                new() { NutrientId = 1005, Value = 66.3f },
                new() { NutrientId = 1004, Value = 6.9f }
            ]
        };

        var item = FdcFoodMapper.ApplyToFoodItem(detail);

        Assert.Equal(389f, item.Calories);
        Assert.Equal(16.9f, item.Protein);
        Assert.Equal(66.3f, item.Carbs);
        Assert.Equal(6.9f, item.Fats);
    }

    [Fact]
    public void ApplyToFoodItem_AllCapsDescription_FormatsName()
    {
        var detail = BuildDetail(331960, "CHICKEN BREAST", (1008, 120f), (1003, 22.5f));

        var item = FdcFoodMapper.ApplyToFoodItem(detail);

        Assert.Equal("Chicken Breast", item.Name);
    }

    private static FdcFoodDetail BuildDetail(int fdcId, string name, params (int Id, float Amount)[] nutrients) =>
        new()
        {
            FdcId = fdcId,
            Description = name,
            DataType = "Foundation",
            FoodNutrients = nutrients
                .Select(n => new FdcFoodDetailNutrient
                {
                    Nutrient = new FdcNutrient { Id = n.Id },
                    Amount = n.Amount
                })
                .ToList()
        };
}
