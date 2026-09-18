namespace MuscleCuties.Core.Models.Enums.Workout;

[Flags]
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

public enum InjuryStatus
{
    Acute,
    Recovering,
    Cleared
}
