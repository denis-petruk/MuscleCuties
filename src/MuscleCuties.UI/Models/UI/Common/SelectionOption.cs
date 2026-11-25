namespace MuscleCuties.UI.Models.UI.Common;

/// <summary>Anything a picker or chip can show by label without knowing its value type.</summary>
public interface ILabeledOption
{
    string Label { get; }
}

/// <summary>One choice in a picker: the value to store, the key of its visible label,</summary>
/// and an optional glyph.</summary>
public sealed record SelectionOption<T>(T Value, string Label, string IconGlyph = "") : ILabeledOption;
