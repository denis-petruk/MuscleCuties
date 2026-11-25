using System.Diagnostics.CodeAnalysis;

namespace MuscleCuties.Core.Models.Enums.Workout;

[Flags]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Represents bitwise injury flags.")]
/// <summary>A known injury site, which restricts the exercises a plan may select.</summary>
public enum InjuryFlag
{
    None = 0,
    Metatarsal = 1,
    Knee = 2,
    Ankle = 4,
    Shoulder = 8,
    LowBack = 16,
    Wrist = 32,
    Hip = 64,
    Neck = 128
}

/// <summary>How an injury stands, which decides whether it still restricts planning.</summary>
public enum InjuryStatus
{
    Acute,
    Recovering,
    Cleared
}
