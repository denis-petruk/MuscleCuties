namespace MuscleCuties.Core.Domain.Cycle;

/// <summary>The supported cycle-length range, which every stored and predicted length is clamped to.</summary>
public static class CycleLengthLimits
{
    public const int Default = 28;
    public const int Minimum = 18;
    public const int Maximum = 60;
}
