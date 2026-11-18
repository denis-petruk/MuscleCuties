using MuscleCuties.Core.Services.Health;
using MuscleCuties.UI.ViewModels.Dashboard;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class DailyCheckInViewModelTests
{
    [Fact]
    public async Task SameDayStatusReloadPreservesTheUnsubmittedRatings()
    {
        var checkIn = new CheckInUseCase();
        var viewModel = new DailyCheckInViewModel(checkIn, () => { }, () => Task.CompletedTask,
            clock: new CheckInClock());
        await viewModel.CheckIfAlreadyCompletedAsync();
        viewModel.EnergyLevel = 2;
        viewModel.PlanningSleepQuality = 3;
        viewModel.PlanningFatigue = 4;
        viewModel.PlanningSoreness = 5;

        await viewModel.CheckIfAlreadyCompletedAsync();

        Assert.Equal(2, viewModel.EnergyLevel);
        Assert.Equal(3, viewModel.PlanningSleepQuality);
        Assert.Equal(4, viewModel.PlanningFatigue);
        Assert.Equal(5, viewModel.PlanningSoreness);
        Assert.True(viewModel.CanSubmit);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NewLocalDayClearsPreviousAnswersBeforeTheyCanBeSaved(bool reopen)
    {
        var checkIn = new CheckInUseCase();
        var clock = new CheckInClock();
        var viewModel = new DailyCheckInViewModel(checkIn, () => { }, () => Task.CompletedTask, clock: clock);
        await viewModel.CheckIfAlreadyCompletedAsync();
        viewModel.EnergyLevel = 2;
        viewModel.PlanningSleepQuality = 3;
        viewModel.PlanningFatigue = 4;
        viewModel.PlanningSoreness = 5;
        viewModel.SleepHours = 7;
        viewModel.PainLevel = 1;
        viewModel.HasBloating = true;
        viewModel.BodyWeight = 64;

        clock.UtcNow = clock.UtcNow.AddDays(1);
        if (reopen)
            await viewModel.CheckIfAlreadyCompletedAsync();
        else
            await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(DateOnly.FromDateTime(clock.UtcNow.DateTime), checkIn.LastStatusRequest?.Date);
        Assert.Null(checkIn.LastSubmission);
        Assert.False(viewModel.CanSubmit);
        Assert.Equal("—", viewModel.SleepDisplay);
        Assert.Null(viewModel.PlanningSoreness);
        Assert.Null(viewModel.PainLevel);
        Assert.Null(viewModel.HasBloating);
        Assert.Null(viewModel.BodyWeight);
    }

    [Fact]
    public async Task AStatusReadStartedBeforeSaveCannotUndoTheSuccessfulSubmission()
    {
        var checkIn = new CheckInUseCase();
        var viewModel = new DailyCheckInViewModel(checkIn, () => { }, () => Task.CompletedTask,
            clock: new CheckInClock());
        await viewModel.CheckIfAlreadyCompletedAsync();
        viewModel.EnergyLevel = 2;
        viewModel.PlanningSleepQuality = 3;
        viewModel.PlanningFatigue = 4;

        var pendingStatus = new TaskCompletionSource<DailyCheckInStatusResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        checkIn.PendingStatus = pendingStatus.Task;
        var statusReload = viewModel.CheckIfAlreadyCompletedAsync();

        await viewModel.SubmitCommand.ExecuteAsync(null);
        pendingStatus.SetResult(new DailyCheckInStatusResponse(false, "", ""));
        await statusReload;

        Assert.NotNull(checkIn.LastSubmission);
        Assert.True(viewModel.IsCompleted);
        Assert.False(viewModel.CanSubmit);
    }

    [Fact]
    public async Task SigningOutClearsTheDraftAndDiscardsThePreviousAccountsSaveResult()
    {
        var checkIn = new CheckInUseCase();
        var completions = 0;
        var viewModel = new DailyCheckInViewModel(checkIn, () => { }, () =>
        {
            completions++;
            return Task.CompletedTask;
        }, clock: new CheckInClock());
        await viewModel.CheckIfAlreadyCompletedAsync();
        viewModel.EnergyLevel = 2;
        viewModel.PlanningSleepQuality = 3;
        viewModel.PlanningFatigue = 4;
        viewModel.BodyWeight = 64;
        var pending = new TaskCompletionSource<SubmitDailyCheckInResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        checkIn.PendingSubmission = pending.Task;
        var save = viewModel.SubmitCommand.ExecuteAsync(null);

        viewModel.ResetForAccountChange();
        await viewModel.CheckIfAlreadyCompletedAsync();
        viewModel.EnergyLevel = 5;
        pending.SetResult(new SubmitDailyCheckInResponse(70, "", "", false));
        await save;

        Assert.True(checkIn.LastSubmissionToken.IsCancellationRequested);
        Assert.Null(viewModel.BodyWeight);
        Assert.Null(viewModel.PlanningFatigue);
        Assert.Equal(5, viewModel.EnergyLevel);
        Assert.False(viewModel.IsCompleted);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(0, completions);
    }

    [Fact]
    public async Task AppleHealthCallbackFromPreviousAccountCannotReloadTheNewDraft()
    {
        var checkIn = new CheckInUseCase();
        var healthSync = new PendingHealthSync();
        var viewModel = new DailyCheckInViewModel(checkIn, () => { }, () => Task.CompletedTask,
            healthSync, new ProfileUserIdProvider(), new CheckInClock());
        await viewModel.CheckIfAlreadyCompletedAsync();
        var connect = viewModel.ConnectHealthCommand.ExecuteAsync(null);

        viewModel.ResetForAccountChange();
        await viewModel.CheckIfAlreadyCompletedAsync();
        healthSync.Complete();
        await connect;

        Assert.Equal(2, checkIn.StatusRequestCount);
    }

    private sealed class CheckInClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class CheckInUseCase : IDailyCheckInUseCase
    {
        public DailyCheckInPresentationSettings PresentationSettings { get; } = new(false, false);
        public DailyCheckInStatusRequest? LastStatusRequest { get; private set; }
        public SubmitDailyCheckInRequest? LastSubmission { get; private set; }
        public Task<DailyCheckInStatusResponse>? PendingStatus { get; set; }
        public Task<SubmitDailyCheckInResponse>? PendingSubmission { get; set; }
        public CancellationToken LastSubmissionToken { get; private set; }
        public int StatusRequestCount { get; private set; }

        public Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request,
            CancellationToken ct = default)
        {
            LastStatusRequest = request;
            StatusRequestCount++;
            return PendingStatus ?? Task.FromResult(new DailyCheckInStatusResponse(false, "", ""));
        }

        public Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request,
            CancellationToken ct = default)
        {
            LastSubmission = request;
            LastSubmissionToken = ct;
            return PendingSubmission ?? Task.FromResult(new SubmitDailyCheckInResponse(0, "", "", false));
        }
    }

    private sealed class ProfileUserIdProvider : ICurrentProfileUserIdProvider
    {
        public Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(42);
    }

    private sealed class PendingHealthSync : IHealthSyncService
    {
        private readonly TaskCompletionSource<HealthSyncResult> _sync =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HealthDataSource DeviceHealthSource => HealthDataSource.AppleHealth;
        public string DeviceHealthDisplayName => "Apple Health";
        public bool IsAvailable => true;
        public Task<HealthSyncStatus> GetStatusAsync(int userId) =>
            Task.FromResult(new HealthSyncStatus(null, false, false, null, ""));
        public Task<bool> ShouldShowPromptAsync(int userId) => Task.FromResult(true);
        public Task DismissPromptAsync(int userId) => Task.CompletedTask;
        public Task DisconnectAsync(int userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<HealthSyncResult> SyncAsync(int userId, HealthDataSource source,
            CancellationToken cancellationToken = default) => _sync.Task;
        public Task<HealthWeeklySummary?> GetCachedWeeklySummaryAsync(int userId) =>
            Task.FromResult<HealthWeeklySummary?>(null);

        public void Complete() => _sync.SetResult(new HealthSyncResult(DeviceHealthSource, true, null, ""));
    }
}
