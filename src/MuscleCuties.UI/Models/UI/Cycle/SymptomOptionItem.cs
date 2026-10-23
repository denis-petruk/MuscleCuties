using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UI.Models.UI.Cycle;

public class SymptomOptionItem
{
    public SymptomType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconData { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
