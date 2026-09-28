namespace MuscleCuties.Core.Models.UI.Common;

public interface ILabeledOption
{
    string Label { get; }
}

public sealed record SelectionOption<T>(T Value, string Label, string IconGlyph = "") : ILabeledOption;
