namespace MuscleCuties.Core.Models.Enums.Workout;

[Flags]
public enum EquipmentSet
{
    None = 0,
    Barbell = 1,
    Dumbbell = 2,
    Machines = 4,
    Cables = 8,
    Bands = 16,
    HipThrustBench = 32,
    BackExtension45 = 64,
    PullUpBar = 128,
    Kettlebell = 256,
    ClimbingWall = 512
}
