namespace MuscleCuties.UI.Models.UI.Cycle;

/// <summary>One bullet in the phase detail list: a heading, its explanation and a leading glyph.</summary>
public class CyclePhaseDetailPoint
{
    /// <summary>Localized heading of the bullet.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Localized explanation under the heading.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>Glyph drawn before the heading.</summary>
    public string IconGlyph { get; set; } = string.Empty;
}
