namespace MuscleCuties.Core.Domain.Hydration;

/// <summary>Whole-millilitre storage limits, not intake recommendations.</summary>
public sealed class WaterPortion
{
    public const int MinimumMillilitres = 1;
    public const int MaximumMillilitres = 10_000;
    private const double MillilitresPerLitre = 1000;

    private WaterPortion(int millilitres) => Millilitres = millilitres;

    public int Millilitres { get; }

    public static WaterPortion FromMillilitres(double millilitres)
    {
        if (!double.IsFinite(millilitres) || millilitres < MinimumMillilitres ||
            millilitres > MaximumMillilitres || Math.Truncate(millilitres) != millilitres)
            throw new ArgumentOutOfRangeException(nameof(millilitres), "Enter a supported whole-millilitre volume.");
        return new WaterPortion(checked((int)millilitres));
    }

    public static WaterPortion FromLitres(double litres) => FromMillilitres(litres * MillilitresPerLitre);
}
