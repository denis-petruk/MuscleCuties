namespace MuscleCuties.Core.Models.Enums.Quiz;

/// <summary>Whether the quiz has loaded, which the setup screen branches on.</summary>
public enum QuizLoadState
{
    Loading,
    Ready,
    Empty,
    Failed
}
