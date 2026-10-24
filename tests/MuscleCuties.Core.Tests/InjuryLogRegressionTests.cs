using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Tests;

public class InjuryLogRegressionTests
{
    [Fact]
    public async Task New_Injury_Requires_An_Explicit_Site_Choice()
    {
        var store = new RecordingWorkoutInjuryStore();
        var useCase = new WorkoutInjuryUseCase(store);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.SaveInjuryAsync(
            new SaveWorkoutInjuryInput(
                0,
                WorkoutInjurySite.None,
                WorkoutInjuryStatus.Recovering,
                3,
                DateOnly.FromDateTime(DateTime.Today))));

        await useCase.SaveInjuryAsync(new SaveWorkoutInjuryInput(
            0,
            WorkoutInjurySite.Ankle,
            WorkoutInjuryStatus.Recovering,
            3,
            DateOnly.FromDateTime(DateTime.Today)));

        Assert.Single(store.SavedInputs);
        Assert.Equal(WorkoutInjurySite.Ankle, store.SavedInputs[0].Site);
        Assert.Equal(WorkoutInjuryStatus.Recovering, store.SavedInputs[0].Status);
    }

    [Fact]
    public async Task SaveInjury_Clamps_Pain_And_Future_Since_Date()
    {
        var store = new RecordingWorkoutInjuryStore();
        var useCase = new WorkoutInjuryUseCase(store);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await useCase.SaveInjuryAsync(new SaveWorkoutInjuryInput(
            42,
            WorkoutInjurySite.Knee,
            WorkoutInjuryStatus.Acute,
            99,
            today.AddDays(5)));

        var saved = Assert.Single(store.SavedInputs);
        Assert.Equal(42, saved.Id);
        Assert.Equal(WorkoutInjuryEntryRule.MaximumPain, saved.Pain);
        Assert.Equal(today, saved.Since);
    }

    [Fact]
    public void Entry_Rule_Rejects_Missing_Site_And_Normalizes_Pain_And_Date()
    {
        var today = new DateOnly(2026, 10, 24);

        Assert.False(WorkoutInjuryEntryRule.TryNormalize(
            WorkoutInjuryEntryRule.NoInjurySite,
            WorkoutInjuryEntryRule.MinimumPain,
            today,
            today,
            out _));
        Assert.True(WorkoutInjuryEntryRule.TryNormalize(
            (int)WorkoutInjurySite.Knee,
            int.MaxValue,
            today.AddDays(1),
            today,
            out var normalized));

        Assert.Equal(WorkoutInjuryEntryRule.MaximumPain, normalized.Pain);
        Assert.Equal(today, normalized.Since);
    }

    [Fact]
    public async Task Clear_And_Delete_Reject_NonPositive_Injury_Ids()
    {
        var store = new RecordingWorkoutInjuryStore();
        var useCase = new WorkoutInjuryUseCase(store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => useCase.ClearInjuryAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => useCase.DeleteInjuryAsync(-1));
    }

    private sealed class RecordingWorkoutInjuryStore : IWorkoutInjuryStore
    {
        public List<SaveWorkoutInjuryInput> SavedInputs { get; } = [];

        public Task<WorkoutInjuryImpactDto> GetImpactAsync(CancellationToken ct = default) =>
            Task.FromResult(new WorkoutInjuryImpactDto(false, "None", [], [], []));

        public Task<IReadOnlyList<WorkoutInjuryLogDto>> GetAllInjuriesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkoutInjuryLogDto>>([]);

        public Task SaveInjuryAsync(SaveWorkoutInjuryInput input, CancellationToken ct = default)
        {
            SavedInputs.Add(input);
            return Task.CompletedTask;
        }

        public Task ClearInjuryAsync(int injuryId, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteInjuryAsync(int injuryId, CancellationToken ct = default) => Task.CompletedTask;

        public Task RefreshPlanAfterInjuryChangeAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
