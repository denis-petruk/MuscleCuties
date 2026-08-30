namespace MuscleCuties.Core.Models.UI.Workout;

public sealed record WorkoutActivityGroupSection(
    string GroupTitle,
    string GroupDescription,
    string GroupIconGlyph,
    IReadOnlyList<WorkoutActivityOptionItem> Items);
