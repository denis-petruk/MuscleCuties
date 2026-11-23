using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;

namespace MuscleCuties.Core.Tests;

public sealed class BarcodeScanRuleTests
{
    private static FoodCatalogItemDto Food() =>
        new(1, "Greek yoghurt", 59f, 10f, 3.6f, 0.4f, 100f, "g", null, "Nutrition_Source_LocalFood",
            string.Empty, false, "food-1");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_Input_Asks_For_Digits(string? typed)
    {
        var decision = BarcodeScanRule.Decide(typed, null);

        Assert.Equal(BarcodeScanOutcome.EnterDigits, decision.Outcome);
        Assert.Equal(string.Empty, decision.Input);
    }

    [Fact]
    public void Missing_Lookup_Is_Reported_As_Unavailable()
    {
        var decision = BarcodeScanRule.Decide("3017620422003", null);

        Assert.Equal(BarcodeScanOutcome.LookupUnavailable, decision.Outcome);
        Assert.Equal("3017620422003", decision.Input);
    }

    [Fact]
    public void Invalid_Barcode_Is_Not_Treated_As_A_Missing_Food()
    {
        var decision = BarcodeScanRule.Decide("abc", new BarcodeLookupResult(
            IsFound: false, IsValidBarcode: false, NormalizedBarcode: "abc", FoodItem: null,
            ErrorMessage: "Barcode is not a supported format."));

        Assert.Equal(BarcodeScanOutcome.InvalidFormat, decision.Outcome);
        Assert.Null(decision.FoodItem);
    }

    [Fact]
    public void Valid_Barcode_Without_A_Match_Keeps_The_Normalised_Value()
    {
        var decision = BarcodeScanRule.Decide("3017620422003", new BarcodeLookupResult(
            IsFound: false, IsValidBarcode: true, NormalizedBarcode: "3017620422003", FoodItem: null,
            ErrorMessage: "Food item not found for barcode."));

        Assert.Equal(BarcodeScanOutcome.NotFound, decision.Outcome);
        Assert.Equal("3017620422003", decision.NormalizedBarcode);
    }

    [Fact]
    public void A_Found_Food_Carries_The_Item_For_The_Caller_To_Apply()
    {
        var decision = BarcodeScanRule.Decide(" 3017620422003 ", new BarcodeLookupResult(
            IsFound: true, IsValidBarcode: true, NormalizedBarcode: "3017620422003", FoodItem: Food(),
            ErrorMessage: null));

        Assert.Equal(BarcodeScanOutcome.Found, decision.Outcome);
        Assert.Equal("3017620422003", decision.Input);
        Assert.Equal("Greek yoghurt", decision.FoodItem?.Name);
    }

    [Fact]
    public void Camera_Timeout_Has_One_Value()
    {
        Assert.Equal(20, BarcodeScanRule.CameraCaptureTimeoutSeconds);
    }
}
