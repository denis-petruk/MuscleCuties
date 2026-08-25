using Microsoft.Maui.Graphics;

namespace MuscleCuties.Core.Services.Workout.Planning;

public sealed record WorkoutListItem(
    int WorkoutDayId,
    string Tag,
    string DayLabel,
    string Title,
    string Duration,
    string ExerciseCountText,
    string DetailsText,
    Color ActivityBackground,
    Color ActivityTextColor,
    bool IsRestDay = false);
