using System.Globalization;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// NM02: the grouped editor must round-trip an untouched quantity exactly, parse a deliberate edit
/// predictably, and reject an ambiguous grouping separator instead of reinterpreting it.
/// </summary>
public sealed class IngredientPortionDraftTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo RuRu = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    private static bool TryParse(string? text, float original, CultureInfo culture, out float grams) =>
        IngredientPortionDraft.TryParse(text, IngredientPortionDraft.Format(original, culture), original,
            culture, out grams);

    [Theory]
    [InlineData(0.1f)]
    [InlineData(33.333333f)]
    [InlineData(1000f)]
    [InlineData(1e-7f)]
    public void UntouchedDraftsKeepTheStoredValueExactly(float original)
    {
        foreach (var culture in new[] { EnUs, RuRu, DeDe })
        {
            var text = IngredientPortionDraft.Format(original, culture);
            Assert.True(IngredientPortionDraft.TryParse(text, text, original, culture, out var grams));
            Assert.Equal(
                BitConverter.SingleToInt32Bits(original),
                BitConverter.SingleToInt32Bits(grams));
        }
    }

    [Fact]
    public void FormattedTextRoundTripsAtFullPrecision()
    {
        Assert.Equal(33.333333f, float.Parse(IngredientPortionDraft.Format(33.333333f, EnUs), EnUs));
        Assert.True(float.Parse(IngredientPortionDraft.Format(1e-7f, EnUs), EnUs) > 0f);
    }

    [Fact]
    public void DeliberateEditsParseInTheCurrentCulture()
    {
        Assert.True(TryParse("12.5", 100f, EnUs, out var enGrams));
        Assert.Equal(12.5f, enGrams);

        Assert.True(TryParse("12,5", 100f, RuRu, out var ruGrams));
        Assert.Equal(12.5f, ruGrams);

        Assert.True(TryParse("12,5", 100f, DeDe, out var deGrams));
        Assert.Equal(12.5f, deGrams);
    }

    [Fact]
    public void TheSingleAlternateDecimalSeparatorIsAccepted()
    {
        Assert.True(TryParse("12,5", 100f, EnUs, out var enGrams));
        Assert.Equal(12.5f, enGrams);

        Assert.True(TryParse("12.5", 100f, RuRu, out var ruGrams));
        Assert.Equal(12.5f, ruGrams);
    }

    [Theory]
    [InlineData("1,234")]
    [InlineData("1.234")]
    [InlineData("1,234,567")]
    [InlineData("1.234.567")]
    public void AmbiguousGroupingIsRejectedInEveryCulture(string text)
    {
        foreach (var culture in new[] { EnUs, RuRu, DeDe })
            Assert.False(TryParse(text, 100f, culture, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e999")]
    [InlineData("abc")]
    public void UnusableDraftsAreRejected(string text) =>
        Assert.False(TryParse(text, 100f, EnUs, out _));

    [Fact]
    public void EditingBackToTheOriginalTextKeepsTheExactValue()
    {
        const float original = 33.333333f;
        var text = IngredientPortionDraft.Format(original, EnUs);
        Assert.True(IngredientPortionDraft.TryParse(text, text, original, EnUs, out var grams));
        Assert.Equal(BitConverter.SingleToInt32Bits(original), BitConverter.SingleToInt32Bits(grams));
    }

    [Fact]
    public void RepeatedFoodRowsKeepTheirOwnAmounts()
    {
        // Two occurrences of one food, only the first edited: the second keeps 33.333332 exactly.
        float[] original = [0.1f, 33.333332f];
        int[] foods = [7, 7];
        IngredientPortionEdit?[] edits = [new IngredientPortionEdit(0, 7, 500f), null];

        var resolved = GroupedMealEditProjection.ResolveAmounts(original, foods, edits);

        Assert.Equal(500f, resolved[0]);
        Assert.Equal(BitConverter.SingleToInt32Bits(33.333332f), BitConverter.SingleToInt32Bits(resolved[1]));
    }

    [Fact]
    public void AnUntouchedSavePreservesEveryOriginalAmount()
    {
        float[] original = [0.1f, 33.333332f, 1e-7f];
        int[] foods = [1, 2, 3];
        IngredientPortionEdit?[] edits = [null, null, null];

        var resolved = GroupedMealEditProjection.ResolveAmounts(original, foods, edits);

        Assert.Equal(original.Length, resolved.Length);
        for (var index = 0; index < original.Length; index++)
            Assert.Equal(BitConverter.SingleToInt32Bits(original[index]), BitConverter.SingleToInt32Bits(resolved[index]));
    }

    [Fact]
    public void AMismatchedRowIdentityCannotOverwriteAnotherIngredient()
    {
        float[] original = [10f, 20f];
        int[] foods = [1, 2];
        // Row 1 claims food 1: a reordered or stale row must be ignored, not applied.
        IngredientPortionEdit?[] edits = [null, new IngredientPortionEdit(1, 1, 999f)];

        var resolved = GroupedMealEditProjection.ResolveAmounts(original, foods, edits);

        Assert.Equal(20f, resolved[1]);
    }

    [Fact]
    public void InvalidEditValuesAreIgnored()
    {
        float[] original = [10f];
        int[] foods = [1];
        IngredientPortionEdit?[] edits = [new IngredientPortionEdit(0, 1, float.NaN)];

        var resolved = GroupedMealEditProjection.ResolveAmounts(original, foods, edits);

        Assert.Equal(10f, resolved[0]);
    }
}
