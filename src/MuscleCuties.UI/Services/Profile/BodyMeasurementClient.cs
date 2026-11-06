using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

namespace MuscleCuties.UI.Services.Profile;

public sealed class BodyMeasurementClient(IMuscleCutiesBackendApiClient apiClient)
{
    private const string Route = "/api/v1/progress/measurements";

    public Task<BackendApiResponse<BodyMeasurementHistoryOutputDto>> LoadAsync(
        DateOnly through, CancellationToken cancellationToken) =>
        apiClient.GetAsync<BodyMeasurementHistoryOutputDto>(
            FormattableString.Invariant($"{Route}?from={DateOnly.MinValue:yyyy-MM-dd}&through={through:yyyy-MM-dd}"),
            cancellationToken);

    public Task<BackendApiResponse<BodyMeasurementOutputDto>> CreateAsync(
        CreateBodyMeasurementInputDto input, CancellationToken cancellationToken) =>
        apiClient.SendMutationAsync<CreateBodyMeasurementInputDto, BodyMeasurementOutputDto>(
            HttpMethod.Post, Route, input, input.CommandId.ToString("N"), cancellationToken);

    public Task<BackendApiResponse<BodyMeasurementOutputDto>> CorrectAsync(
        CorrectBodyMeasurementInputDto input, CancellationToken cancellationToken) =>
        apiClient.SendMutationAsync<CorrectBodyMeasurementInputDto, BodyMeasurementOutputDto>(
            HttpMethod.Put, $"{Route}/{input.Id:D}", input, input.CommandId.ToString("N"), cancellationToken);

    public async Task<BackendApiResponse<BodyMeasurementOutputDto>> DeleteAsync(
        DeleteBodyMeasurementInputDto input, CancellationToken cancellationToken)
    {
        var route = FormattableString.Invariant(
            $"{Route}/{input.Id:D}?commandId={input.CommandId:D}&expectedVersion={input.ExpectedVersion}");
        var response = await apiClient.DeleteAsync(route, input.CommandId.ToString("N"), cancellationToken);
        return new BackendApiResponse<BodyMeasurementOutputDto>(
            response.Status, null, response.ErrorCode, response.ServerConcurrencyVersion,
            ResponseStatusCode: response.ResponseStatusCode, RequestId: response.RequestId);
    }
}
