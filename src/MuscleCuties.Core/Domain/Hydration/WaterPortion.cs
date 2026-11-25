namespace MuscleCuties.Core.Domain.Hydration;

/// <summary>Whole-millilitre storage limits, not intake recommendations.</summary>
public sealed class WaterPortion
{
    public const int MinimumMillilitres = 1;
    public const int MaximumMillilitres = 10_000;
    private const double MillilitresPerLitre = 1000;

    /// <summary>Holds a portion already converted to whole millilitres.</summary>
    private WaterPortion(int millilitres) => Millilitres = millilitres;

    /// <summary>Size of the portion in millilitres.</summary>
    public int Millilitres { get; }

    /// <summary>Builds a portion from millilitres, refusing a value the column cannot hold.</summary>
    public static WaterPortion FromMillilitres(double millilitres)
    {
        if (!double.IsFinite(millilitres) || millilitres < MinimumMillilitres ||
            millilitres > MaximumMillilitres || Math.Truncate(millilitres) != millilitres)
            throw new ArgumentOutOfRangeException(nameof(millilitres), "Enter a supported whole-millilitre volume.");
        return new WaterPortion(checked((int)millilitres));
    }

    /// <summary>Builds a portion from a volume in litres.</summary>
    public static WaterPortion FromLitres(double litres) => FromMillilitres(litres * MillilitresPerLitre);
}
