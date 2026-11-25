namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>A titled group of activity options in the generation chooser.</summary>
public sealed record WorkoutActivityGroupSection(
    string GroupTitle,
    string GroupDescription,
    string GroupIconGlyph,
    IReadOnlyList<WorkoutActivityOptionItem> Items);
