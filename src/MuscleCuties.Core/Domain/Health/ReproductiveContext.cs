namespace MuscleCuties.Core.Domain.Health;

/// <summary>Whether she is pregnant, which withholds load and certain planning paths entirely.</summary>
public enum PregnancyStatus
{
    Unknown = 0,
    No = 1,
    Yes = 2
}

/// <summary>The contraception state, which the cycle projection may read.</summary>
public enum ContraceptionStatus
{
    Unknown = 0,
    No = 1,
    HormonalContraception = 2,
    IrregularCycle = 3
}

/// <summary>The reproductive context as stored, kept separate from the cycle history it modifies.</summary>
public sealed record ReproductiveContext(
    PregnancyStatus Pregnancy = PregnancyStatus.Unknown,
    ContraceptionStatus Contraception = ContraceptionStatus.Unknown)
{
    public static readonly ReproductiveContext Default = new();

    /// <summary>
    ///     Automatic calorie deficit (weight loss targets) is allowed only when user explicitly confirms
    ///     they are not pregnant or breastfeeding. Refused or unknown status disables deficit targets.
    /// </summary>
    public bool AllowsCalorieDeficit => Pregnancy == PregnancyStatus.No;

    /// <summary>
    ///     Automatic cycle-phase training adjustments are allowed only when user explicitly confirms
    ///     negative pregnancy and regular cycle without hormonal contraception.
    /// </summary>
    public bool AllowsCyclePhaseTrainingAdjustments =>
        Pregnancy == PregnancyStatus.No && Contraception == ContraceptionStatus.No;
}
