using System.Text.Json;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Workout.Injury;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class InjuryLogRoundTripTests
{
    private const string InjuriesRoute = "/api/v1/injuries";
    private const int OriginalPain = 3;
    private const int EditedPain = 5;

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NewInjuryDefaultsToUnansweredAndPreservesExplicitSwitchChanges(bool? clinicianAnswer)
    {
        var api = new InMemoryInjuryApiClient();
        var cloud = new CloudWorkoutInjuryStore(api);
        var viewModel = new InjuryLogViewModel(new WorkoutInjuryUseCase(cloud), () => Task.CompletedTask);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.ToggleAddFormCommand.Execute(null);
        viewModel.SelectedSiteFlag = WorkoutInjurySite.Shoulder;
        viewModel.SelectedStatus = WorkoutInjuryStatus.Acute;
        viewModel.NewPainLevel = OriginalPain;
        if (clinicianAnswer is not null)
        {
            viewModel.ClinicianAdvisedAvoid = true;
            if (clinicianAnswer == false)
                viewModel.ClinicianAdvisedAvoid = false;
        }

        await viewModel.SaveInjuryCommand.ExecuteAsync(null);

        Assert.Equal(nameof(InjuryWorseningTrigger.NotAnswered),
            api.LastPayload.GetProperty("worseningTrigger").GetString());
        var answer = api.LastPayload.GetProperty("clinicianAdvisedAvoid");
        Assert.Equal(clinicianAnswer, answer.ValueKind == JsonValueKind.Null ? null : answer.GetBoolean());
        var reloaded = Assert.Single(await cloud.GetAllInjuriesAsync());
        Assert.Equal(InjuryWorseningTrigger.NotAnswered, reloaded.WorseningTrigger);
        Assert.Equal(clinicianAnswer, reloaded.ClinicianAdvisedAvoid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task EditingOnlyPainPreservesBothSafetyAnswers(bool? clinicianAnswer)
    {
        var api = new InMemoryInjuryApiClient();
        var cloud = new CloudWorkoutInjuryStore(api);
        var today = DateOnly.FromDateTime(DateTime.Today);
        await cloud.SaveInjuryAsync(new SaveWorkoutInjuryInput(
            0, WorkoutInjurySite.Shoulder, WorkoutInjuryStatus.Acute, OriginalPain, today,
            InjuryWorseningTrigger.OverheadReach, clinicianAnswer));

        var viewModel = new InjuryLogViewModel(new WorkoutInjuryUseCase(cloud), () => Task.CompletedTask);
        await viewModel.LoadCommand.ExecuteAsync(null);
        var loaded = Assert.Single(viewModel.ActiveInjuries);
        Assert.Equal(InjuryWorseningTrigger.OverheadReach, loaded.WorseningTrigger);
        Assert.Equal(clinicianAnswer, loaded.ClinicianAdvisedAvoid);

        viewModel.EditInjuryCommand.Execute(loaded);
        Assert.Equal(InjuryWorseningTrigger.OverheadReach, viewModel.SelectedWorseningOption?.Trigger);
        viewModel.NewPainLevel = EditedPain;
        await viewModel.SaveInjuryCommand.ExecuteAsync(null);

        Assert.Equal(EditedPain, api.LastPayload.GetProperty("pain").GetInt32());
        Assert.Equal(nameof(InjuryWorseningTrigger.OverheadReach),
            api.LastPayload.GetProperty("worseningTrigger").GetString());
        var answer = api.LastPayload.GetProperty("clinicianAdvisedAvoid");
        Assert.Equal(clinicianAnswer, answer.ValueKind == JsonValueKind.Null ? null : answer.GetBoolean());
        var reloaded = Assert.Single(await cloud.GetAllInjuriesAsync());
        Assert.Equal(loaded.Id, reloaded.Id);
        Assert.Equal(EditedPain, reloaded.Pain);
        Assert.Equal(InjuryWorseningTrigger.OverheadReach, reloaded.WorseningTrigger);
        Assert.Equal(clinicianAnswer, reloaded.ClinicianAdvisedAvoid);
    }

    [Fact]
    public async Task EditingUnansweredRestrictionPersistsAnExplicitSwitchChange()
    {
        var api = new InMemoryInjuryApiClient();
        var cloud = new CloudWorkoutInjuryStore(api);
        await cloud.SaveInjuryAsync(new SaveWorkoutInjuryInput(
            0, WorkoutInjurySite.Shoulder, WorkoutInjuryStatus.Acute, OriginalPain,
            DateOnly.FromDateTime(DateTime.Today), InjuryWorseningTrigger.OverheadReach, null));
        var viewModel = new InjuryLogViewModel(new WorkoutInjuryUseCase(cloud), () => Task.CompletedTask);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.EditInjuryCommand.Execute(Assert.Single(viewModel.ActiveInjuries));
        viewModel.ClinicianAdvisedAvoid = true;
        await viewModel.SaveInjuryCommand.ExecuteAsync(null);

        Assert.True(api.LastPayload.GetProperty("clinicianAdvisedAvoid").GetBoolean());
        Assert.True(Assert.Single(await cloud.GetAllInjuriesAsync()).ClinicianAdvisedAvoid);
    }

    private sealed class InMemoryInjuryApiClient : IMuscleCutiesBackendApiClient
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
        private InjuryObservationRecord? _stored;
        public JsonElement LastPayload { get; private set; }

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(
            string relativePath, CancellationToken cancellationToken = default)
        {
            Assert.Equal(InjuriesRoute, relativePath);
            InjuryObservationRecord[] records = _stored is null ? [] : [_stored];
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, (TResponse)(object)records));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(
            HttpMethod method, string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(HttpMethod.Put, method);
            Assert.Equal(InjuriesRoute, relativePath);
            LastPayload = JsonSerializer.SerializeToElement(payload, Options);
            var status = LastPayload.GetProperty("status").GetString()!;
            var restriction = LastPayload.GetProperty("clinicianAdvisedAvoid");
            _stored = new InjuryObservationRecord(
                LastPayload.GetProperty("id").GetGuid(), LastPayload.GetProperty("siteFlag").GetInt32(), status,
                DateOnly.Parse(LastPayload.GetProperty("since").GetString()!),
                LastPayload.GetProperty("pain").GetInt32(), DateOnly.Parse(LastPayload.GetProperty("date").GetString()!),
                status != nameof(WorkoutInjuryStatus.Cleared),
                Enum.Parse<InjuryWorseningTrigger>(LastPayload.GetProperty("worseningTrigger").GetString()!),
                restriction.ValueKind == JsonValueKind.Null ? null : restriction.GetBoolean());
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, (TResponse)(object)_stored));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(
            string relativePath, string? idempotencyKey = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
