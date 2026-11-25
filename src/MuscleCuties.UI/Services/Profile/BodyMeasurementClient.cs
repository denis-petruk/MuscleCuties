using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

namespace MuscleCuties.UI.Services.Profile;

/// <summary>Reads and writes body measurements for the progress screen.</summary>
public sealed class BodyMeasurementClient(IMuscleCutiesBackendApiClient apiClient)
{
    private const string Route = "/api/v1/progress/measurements";

    /// <summary>Reads the measurement history through the given date.</summary>
    public Task<BackendApiResponse<BodyMeasurementHistoryOutputDto>> LoadAsync(
        DateOnly through, CancellationToken cancellationToken) =>
        apiClient.GetAsync<BodyMeasurementHistoryOutputDto>(
            FormattableString.Invariant($"{Route}?from={DateOnly.MinValue:yyyy-MM-dd}&through={through:yyyy-MM-dd}"),
            cancellationToken);

    /// <summary>Creates a measurement.</summary>
    public Task<BackendApiResponse<BodyMeasurementOutputDto>> CreateAsync(
        CreateBodyMeasurementInputDto input, CancellationToken cancellationToken) =>
        apiClient.SendMutationAsync<CreateBodyMeasurementInputDto, BodyMeasurementOutputDto>(
            HttpMethod.Post, Route, input, input.CommandId.ToString("N"), cancellationToken);

    /// <summary>Corrects a measurement.</summary>
    public Task<BackendApiResponse<BodyMeasurementOutputDto>> CorrectAsync(
        CorrectBodyMeasurementInputDto input, CancellationToken cancellationToken) =>
        apiClient.SendMutationAsync<CorrectBodyMeasurementInputDto, BodyMeasurementOutputDto>(
            HttpMethod.Put, $"{Route}/{input.Id:D}", input, input.CommandId.ToString("N"), cancellationToken);

    /// <summary>Deletes a measurement.</summary>
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
