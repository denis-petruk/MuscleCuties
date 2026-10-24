using System.Text.Json;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Cycle.DailyLog;
using MuscleCuties.UseCases.Features.Cycle.PhaseEdit;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using Xunit;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.Core.Tests;

public sealed class CloudCycleAndCheckInContractTests
{
    private static readonly DateOnly SampleDate = new(2026, 10, 19);
    private static readonly DateTime SampleDateTime = SampleDate.ToDateTime(TimeOnly.MinValue);
    private const string HistoryRoute = "/api/v1/cycle/history";
    private const string PhaseRoute = "/api/v1/cycle/history/phases";
    private const string CheckInsRoute = "/api/v1/check-ins";
    private const int PlanningRating = 3;
    private const int SyntheticSleepHours = 8;
    private const int SyntheticEnergy = 3;
    private const int SyntheticPain = 1;

    private static SubmitDailyCheckInRequest ValidCheckInRequest()
    {
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
        return new SubmitDailyCheckInRequest(DateOnly.FromDateTime(localNow.DateTime),
            SyntheticSleepHours, SyntheticEnergy, SyntheticPain, false, null,
            PlanningRating, PlanningRating, PlanningRating, TimeZoneInfo.Local.Id);
    }

    [Fact]
    public async Task CheckInStatus_404IsAnEndpointFailure()
    {
        var api = new RecordingApiClient { GetResponse = (_, _) => (BackendApiStatus.NotFound, null) };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CloudDailyCheckInStore(api).GetStatusAsync(new DailyCheckInStatusRequest(SampleDate), CancellationToken.None));
    }

    [Fact]
    public async Task CheckInSave_DoesNotWriteWhenExistingReadFails()
    {
        var api = new RecordingApiClient { GetResponse = (_, _) => (BackendApiStatus.ServerError, null) };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CloudDailyCheckInStore(api).SubmitAsync(
                ValidCheckInRequest(), CancellationToken.None));

        Assert.Empty(api.Mutations);
    }

    [Fact]
    public async Task CheckInSave_DoesNotAcceptMismatchedServerRecord()
    {
        var api = new RecordingApiClient
        {
            GetResponse = (path, _) => path.StartsWith(CheckInsRoute, StringComparison.Ordinal)
                ? (BackendApiStatus.Success, "[]")
                : (BackendApiStatus.ServerError, null),
            MutationResponse = (_, _, _) => (BackendApiStatus.Success, "{}")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CloudDailyCheckInStore(api).SubmitAsync(
                ValidCheckInRequest(), CancellationToken.None));

        Assert.Single(api.Mutations);
    }

    [Fact]
    public async Task CycleLoad_404IsAnEndpointFailure()
    {
        var api = new RecordingApiClient { GetResponse = (_, _) => (BackendApiStatus.NotFound, null) };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CloudCycleDailyLogStore(api).LoadAsync(SampleDateTime, CancellationToken.None));
    }

    [Fact]
    public async Task DailyLog_RequiresSavedPhaseAcknowledgementBeforeHistoryRead()
    {
        var historyRead = false;
        var api = new RecordingApiClient
        {
            GetResponse = (_, _) =>
            {
                historyRead = true;
                return (BackendApiStatus.ServerError, null);
            },
            MutationResponse = (_, _, _) => (BackendApiStatus.Success, null)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CloudCycleDailyLogStore(api).SaveAsync(
                new CycleDailyLogRequest(SampleDateTime, true, false, FlowIntensity.Medium, []),
                CancellationToken.None));

        Assert.False(historyRead);
        Assert.Equal(PhaseRoute, Assert.Single(api.Mutations).Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"outcome\":\"Ignored\"}")]
    public async Task PhaseEdit_RequiresSavedAcknowledgement(string? responseJson)
    {
        var api = new RecordingApiClient
        {
            MutationResponse = (_, _, _) => (BackendApiStatus.Success, responseJson)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CloudCyclePhaseEditStore(api).SaveAsync(
                new CyclePhaseEditRequest(SampleDateTime, CyclePhase.Menstrual, null), CancellationToken.None));

        var mutation = Assert.Single(api.Mutations);
        Assert.Equal(PhaseRoute, mutation.Path);
        Assert.Equal("PUT", mutation.Method);
        Assert.Equal("2026-10-19", mutation.Payload.GetProperty("localDate").GetString());
    }

    [Fact]
    public async Task DailyLog_StartThenSymptom_UsesMappedRoutesAndLocalDate()
    {
        var cycleId = Guid.Parse("00000000-0000-0000-0000-000000000101");
        var api = new RecordingApiClient
        {
            GetResponse = (path, _) => path.StartsWith(HistoryRoute, StringComparison.Ordinal)
                ? (BackendApiStatus.Success, JsonSerializer.Serialize(new
                {
                    cycles = new[] { new { id = cycleId, startDate = SampleDate, endDate = (DateOnly?)null, cycleLength = 0 } },
                    symptoms = Array.Empty<object>()
                }))
                : (BackendApiStatus.NotFound, null),
            MutationResponse = (_, path, _) => path == PhaseRoute
                ? (BackendApiStatus.Success, "{\"outcome\":\"Saved\"}")
                : (BackendApiStatus.Success, JsonSerializer.Serialize(new
                {
                    id = Guid.Parse("00000000-0000-0000-0000-000000000102"),
                    cycleId,
                    date = SampleDate,
                    type = nameof(SymptomType.Cramps),
                    severity = 2,
                    notes = (string?)null
                }))
        };

        var saved = await new CloudCycleDailyLogStore(api).SaveAsync(
            new CycleDailyLogRequest(SampleDateTime, true, false, FlowIntensity.Medium, [SymptomType.Cramps]),
            CancellationToken.None);

        Assert.True(saved);
        Assert.Collection(api.Mutations,
            phase =>
            {
                Assert.Equal(PhaseRoute, phase.Path);
                Assert.Equal("2026-10-19", phase.Payload.GetProperty("localDate").GetString());
            },
            symptom =>
            {
                Assert.Equal($"{HistoryRoute}/symptoms", symptom.Path);
                Assert.Equal(cycleId.ToString(), symptom.Payload.GetProperty("cycleId").GetString());
                Assert.Equal("2026-10-19", symptom.Payload.GetProperty("date").GetString());
            });
    }

    [Fact]
    public async Task DailyLog_DoesNotAttachSymptomToExpiredCycle()
    {
        var api = new RecordingApiClient
        {
            GetResponse = (_, _) => (BackendApiStatus.Success, JsonSerializer.Serialize(new
            {
                cycles = new[]
                {
                    new
                    {
                        id = Guid.Parse("00000000-0000-0000-0000-000000000103"),
                        startDate = SampleDate.AddDays(-10), endDate = SampleDate.AddDays(-1), cycleLength = 0
                    }
                },
                symptoms = Array.Empty<object>()
            }))
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CloudCycleDailyLogStore(api).SaveAsync(
                new CycleDailyLogRequest(SampleDateTime, false, false, null, [SymptomType.Cramps]),
                CancellationToken.None));

        Assert.Empty(api.Mutations);
    }

    private sealed class RecordingApiClient : IMuscleCutiesBackendApiClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        public Func<string, Type, (BackendApiStatus Status, string? Json)> GetResponse { get; init; } =
            (_, _) => (BackendApiStatus.ServerError, null);
        public Func<string, string, JsonElement, (BackendApiStatus Status, string? Json)> MutationResponse { get; init; } =
            (_, _, _) => (BackendApiStatus.ServerError, null);
        public List<(string Method, string Path, JsonElement Payload)> Mutations { get; } = [];

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        {
            var result = GetResponse(relativePath, typeof(TResponse));
            return Task.FromResult(new BackendApiResponse<TResponse>(result.Status, Read<TResponse>(result.Json)));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method, string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.SerializeToElement(payload, JsonOptions);
            Mutations.Add((method.Method, relativePath, json));
            var result = MutationResponse(method.Method, relativePath, json);
            return Task.FromResult(new BackendApiResponse<TResponse>(result.Status, Read<TResponse>(result.Json)));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<bool>(BackendApiStatus.ServerError, false));

        private static T? Read<T>(string? json) => json is null ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
