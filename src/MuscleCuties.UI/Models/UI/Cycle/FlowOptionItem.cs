using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.UI.Models.UI.Cycle;

public class FlowOptionItem
{
    public FlowIntensity Intensity { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
