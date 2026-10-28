using System.Text.Json;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

internal sealed record ActivityAnswersPendingSave(SaveActivityAnswersRequest Request, bool ConfirmCurrentWeeks)
{
    public bool Matches(SaveActivityAnswersRequest candidate, bool confirmCurrentWeeks) =>
        ConfirmCurrentWeeks == confirmCurrentWeeks &&
        JsonSerializer.Serialize(Normalize(Request)) == JsonSerializer.Serialize(Normalize(candidate));

    private static SaveActivityAnswersRequest Normalize(SaveActivityAnswersRequest request) =>
        request with { ExpectedVersion = default, HiitCaptureChallengeId = null };
}
