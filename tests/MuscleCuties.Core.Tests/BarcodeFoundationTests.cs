using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.Core.Tests;

public sealed class BarcodeFoundationTests
{
    private const string SyntheticUpcA = "012345000065";
    private const string SyntheticEan13 = "0012345000065";
    private const string SyntheticUpcE = "01234565";
    private const string SyntheticCompressedUpcE = "123456";
    private const string SyntheticCode128 = "SYNTHETIC-FOOD";

    [Theory]
    [InlineData(SyntheticUpcA, BarcodeFormat.UpcA)]
    [InlineData(SyntheticEan13, BarcodeFormat.Ean13)]
    [InlineData(SyntheticUpcE, BarcodeFormat.UpcE)]
    [InlineData(SyntheticCompressedUpcE, BarcodeFormat.UpcE)]
    public void Equivalent_codes_share_canonical_identity_and_exact_keys(string text, BarcodeFormat format)
    {
        var barcode = Barcode.Create(text, format);

        Assert.True(barcode.IsValid, barcode.ErrorMessage);
        Assert.Equal(SyntheticEan13, barcode.NormalizedValue);
        Assert.Equal(Barcode.Create(SyntheticEan13), barcode);
        Assert.Equal(Barcode.Create(SyntheticEan13).GetHashCode(), barcode.GetHashCode());
        Assert.Equal(SyntheticEan13, barcode.GetExactLookupValues().First());
        Assert.Contains(SyntheticUpcA, barcode.GetExactLookupValues());
        Assert.Equal(barcode.GetExactLookupValues().Count, barcode.GetExactLookupValues().Distinct().Count());
        Assert.True(barcode.MatchesExactLookupValue(text));
    }

    [Theory]
    [InlineData(null, BarcodeFormat.Unknown)]
    [InlineData(" ", BarcodeFormat.Unknown)]
    [InlineData("012345000066", BarcodeFormat.UpcA)]
    [InlineData("0012345000066", BarcodeFormat.Ean13)]
    [InlineData("01234566", BarcodeFormat.UpcE)]
    [InlineData("21234565", BarcodeFormat.UpcE)]
    [InlineData("12345671", BarcodeFormat.Ean8)]
    [InlineData("12345x", BarcodeFormat.UpcE)]
    [InlineData("SYNTHETIC\nFOOD", BarcodeFormat.Code128)]
    public void Malformed_codes_never_produce_lookup_keys(string? text, BarcodeFormat format)
    {
        var barcode = Barcode.Create(text, format);

        Assert.False(barcode.IsValid);
        Assert.NotNull(barcode.ErrorMessage);
        Assert.Empty(barcode.GetExactLookupValues());
        Assert.False(barcode.MatchesExactLookupValue(text));
    }

    [Fact]
    public void Manual_auto_detection_preserves_ean8_and_trimmed_upca()
    {
        Assert.Equal(BarcodeFormat.Ean8, Barcode.Create("12345670").Format);
        Assert.True(Barcode.Create("12345670").IsValid);
        Assert.Equal(SyntheticEan13, Barcode.Create($"  {SyntheticUpcA}  ").NormalizedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("X0012345000065X")]
    [InlineData("00123450000650")]
    [InlineData("001234500006")]
    [InlineData(" 0012345000065 ")]
    public void Exact_match_rejects_missing_substring_prefix_suffix_and_padded_storage(string? stored)
    {
        Assert.False(Barcode.Create(SyntheticUpcA).MatchesExactLookupValue(stored));
    }

    [Fact]
    public void Code128_identity_is_ordinal_and_has_no_numeric_aliases()
    {
        var barcode = Barcode.Create(SyntheticCode128, BarcodeFormat.Code128);

        Assert.True(barcode.IsValid);
        Assert.Equal([SyntheticCode128], barcode.GetExactLookupValues());
        Assert.False(barcode.MatchesExactLookupValue(SyntheticCode128.ToLowerInvariant()));
    }

    [Fact]
    public void Code128_case_distinct_values_remain_distinct_in_equality_and_hash_collections()
    {
        var uppercase = Barcode.Create(SyntheticCode128, BarcodeFormat.Code128);
        var lowercase = Barcode.Create(SyntheticCode128.ToLowerInvariant(), BarcodeFormat.Code128);
        var identical = Barcode.Create(SyntheticCode128, BarcodeFormat.Code128);

        Assert.NotEqual(uppercase, lowercase);
        Assert.NotEqual(lowercase, uppercase);
        Assert.Equal(uppercase, identical);
        Assert.Equal(StringComparer.Ordinal.GetHashCode(uppercase.NormalizedValue), uppercase.GetHashCode());
        Assert.Equal(StringComparer.Ordinal.GetHashCode(lowercase.NormalizedValue), lowercase.GetHashCode());
        var identities = new HashSet<Barcode> { uppercase, lowercase };
        Assert.False(identities.Add(identical));
        Assert.True(identities.Remove(uppercase));
        Assert.Contains(lowercase, identities);
        Assert.DoesNotContain(uppercase, identities);
    }
}
