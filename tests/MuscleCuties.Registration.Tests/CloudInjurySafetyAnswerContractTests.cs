using System.Text.Json;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.Registration.Tests;

/// <summary>
///     Regression coverage for the two injury safety questions: they were once collected in the modal
///     and silently dropped in transit, so the cloud projection is asserted here on the wire shape,
///     on the read mapping and on the clear path.
/// </summary>
public sealed class CloudInjurySafetyAnswerContractTests
{
    private const string InjuriesRoute = "/api/v1/injuries";
    private const string ClearedStatus = "Cleared";

    [Xunit.Fact]
    public async Task CloudInjuryStoreSendsReadsAndPreservesBothSafetyAnswers()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var stored = new InjuryObservationRecord(
            Guid.NewGuid(),
            (int)WorkoutInjurySite.Knee,
            "Acute",
            today.AddDays(-2),
            3,
            today,
            true,
            InjuryWorseningTrigger.OverheadReach,
            true);
        var apiClient = new PayloadCapturingInjuryApiClient(stored);
        var store = new CloudWorkoutInjuryStore(apiClient);

        // Read path: both answers must survive the projection into the client DTO.
        var loaded = Xunit.Assert.Single(await store.GetAllInjuriesAsync(CancellationToken.None));
        Xunit.Assert.Equal(InjuryWorseningTrigger.OverheadReach, loaded.WorseningTrigger);
        Xunit.Assert.Equal<bool?>(true, loaded.ClinicianAdvisedAvoid);

        // Write path: both answers must reach the request body.
        await store.SaveInjuryAsync(
            new SaveWorkoutInjuryInput(
                0,
                WorkoutInjurySite.Knee,
                WorkoutInjuryStatus.Acute,
                4,
                today.AddDays(-1),
                InjuryWorseningTrigger.GripAndWristLoad,
                true),
            CancellationToken.None);

        var saved = Xunit.Assert.IsType<JsonElement>(apiClient.LastPayload);
        Xunit.Assert.Equal(
            nameof(InjuryWorseningTrigger.GripAndWristLoad),
            saved.GetProperty("worseningTrigger").GetString());
        Xunit.Assert.True(saved.GetProperty("clinicianAdvisedAvoid").GetBoolean());

        // Clear path: clearing must not erase the answers the user already gave.
        await store.ClearInjuryAsync(loaded.Id, CancellationToken.None);

        var cleared = Xunit.Assert.IsType<JsonElement>(apiClient.LastPayload);
        Xunit.Assert.Equal(ClearedStatus, cleared.GetProperty("status").GetString());
        Xunit.Assert.Equal(
            nameof(InjuryWorseningTrigger.OverheadReach),
            cleared.GetProperty("worseningTrigger").GetString());
        Xunit.Assert.True(cleared.GetProperty("clinicianAdvisedAvoid").GetBoolean());
    }

    [Xunit.Fact]
    public async Task CloudInjuryStoreOmitsNoAnswerAsClosedSetDefault()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var apiClient = new PayloadCapturingInjuryApiClient(
            new InjuryObservationRecord(
                Guid.NewGuid(),
                (int)WorkoutInjurySite.Wrist,
                "Acute",
                today,
                2,
                today,
                true));
        var store = new CloudWorkoutInjuryStore(apiClient);

        await store.SaveInjuryAsync(
            new SaveWorkoutInjuryInput(
                0,
                WorkoutInjurySite.Wrist,
                WorkoutInjuryStatus.Acute,
                2,
                today),
            CancellationToken.None);

        var saved = Xunit.Assert.IsType<JsonElement>(apiClient.LastPayload);
        Xunit.Assert.Equal(
            nameof(InjuryWorseningTrigger.NotAnswered),
            saved.GetProperty("worseningTrigger").GetString());
    }

    private sealed class PayloadCapturingInjuryApiClient(InjuryObservationRecord stored)
        : IMuscleCutiesBackendApiClient
    {
        private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

        public object? LastPayload { get; private set; }

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            object data = relativePath == InjuriesRoute
                ? new[] { stored }
                : stored;
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, (TResponse)data));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method,
            string relativePath,
            TRequest payload,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            LastPayload = payload is null
                ? null
                : JsonSerializer.SerializeToElement(payload, PayloadOptions);
            return Task.FromResult(
                new BackendApiResponse<TResponse>(BackendApiStatus.Success, (TResponse)(object)stored));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<bool>(BackendApiStatus.Success, true));
    }
}
