using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class CloudStrengthPlanProposalConflictTests
{
    [Fact]
    public async Task OpenPreviewConflictIsDistinguishedFromGenericFailure()
    {
        var client = new CloudStrengthPlanProposalStoreV020(new ConflictApi("WorkoutPreviewOpen"));
        var request = new StrengthPlanPreviewRequestV020(WorkoutActivityDto.GeneralStrength,
            new DateOnly(2026, 10, 27), null);

        await Assert.ThrowsAsync<WorkoutPreviewAlreadyOpenException>(() => client.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ChangedAnswersAreNotTreatedAsSaved()
    {
        var client = new CloudStrengthPlanProposalStoreV020(new ConflictApi("WorkoutPlanningInputsChanged"));
        await Assert.ThrowsAsync<WorkoutPlanningInputsChangedException>(() =>
            client.SaveAnswersAsync(new SaveActivityAnswersRequest(0, [], null), CancellationToken.None));
    }

    [Fact]
    public async Task AnswerSaveRecognizesPreviewOpenedOnAnotherDevice()
    {
        var client = new CloudStrengthPlanProposalStoreV020(new ConflictApi("WorkoutPreviewOpen"));
        await Assert.ThrowsAsync<WorkoutPreviewAlreadyOpenException>(() =>
            client.SaveAnswersAsync(new SaveActivityAnswersRequest(0, [], null), CancellationToken.None));
    }

    private sealed class ConflictApi(string code) : IMuscleCutiesBackendApiClient
    {
        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(HttpMethod method,
            string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Conflict, default, code));

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
