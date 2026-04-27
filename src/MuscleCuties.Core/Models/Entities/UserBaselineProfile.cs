namespace MuscleCuties.Core.Models.Entities;

public class UserBaselineProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public int PainMenstrual { get; set; }
    public int PainFollicular { get; set; }
    public int PainOvulatory { get; set; }
    public int PainLuteal { get; set; }

    public int EnergyMenstrual { get; set; }
    public int EnergyFollicular { get; set; }
    public int EnergyOvulatory { get; set; }
    public int EnergyLuteal { get; set; }

    public int MoodMenstrual { get; set; }
    public int MoodFollicular { get; set; }
    public int MoodOvulatory { get; set; }
    public int MoodLuteal { get; set; }

    public User? User { get; set; }
}
