namespace MuscleCuties.Core.Models.Enums.Workout;

public static class EquipmentSetExtensions
{
    public static string ToFriendlyName(this EquipmentSet equipment)
    {
        if (equipment == EquipmentSet.None) return "None";

        var names = new List<string>();
        if (equipment.HasFlag(EquipmentSet.Barbell)) names.Add("Barbell");
        if (equipment.HasFlag(EquipmentSet.Dumbbell)) names.Add("Dumbbell");
        if (equipment.HasFlag(EquipmentSet.Machines)) names.Add("Machine");
        if (equipment.HasFlag(EquipmentSet.Cables)) names.Add("Cable");
        if (equipment.HasFlag(EquipmentSet.Bands)) names.Add("Bands");
        if (equipment.HasFlag(EquipmentSet.HipThrustBench)) names.Add("Hip Thrust Bench");
        if (equipment.HasFlag(EquipmentSet.BackExtension45)) names.Add("45\u00b0 Back Extension");
        if (equipment.HasFlag(EquipmentSet.PullUpBar)) names.Add("Pull-Up Bar");
        if (equipment.HasFlag(EquipmentSet.Kettlebell)) names.Add("Kettlebell");
        if (equipment.HasFlag(EquipmentSet.ClimbingWall)) names.Add("Climbing Wall");
        return string.Join(", ", names);
    }
}
