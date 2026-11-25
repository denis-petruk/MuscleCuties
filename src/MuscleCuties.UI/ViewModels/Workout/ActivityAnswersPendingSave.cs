using System.Text.Json;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

/// <summary>
///     Carries activity answers that still need saving across a navigation step.
/// </summary>
internal sealed record ActivityAnswersPendingSave(SaveActivityAnswersRequest Request, bool ConfirmCurrentWeeks)
{
    /// <summary>True when the pending request matches the candidate.</summary>
    public bool Matches(SaveActivityAnswersRequest candidate, bool confirmCurrentWeeks) =>
        ConfirmCurrentWeeks == confirmCurrentWeeks &&
        JsonSerializer.Serialize(Normalize(Request)) == JsonSerializer.Serialize(Normalize(candidate));

    /// <summary>The request with its week list in a stable order.</summary>
    private static SaveActivityAnswersRequest Normalize(SaveActivityAnswersRequest request) =>
        request with { ExpectedVersion = default, HiitCaptureChallengeId = null };
}
