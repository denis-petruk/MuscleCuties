using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class PhaseAdjustmentProposalUseCaseV010Tests
{
    private static readonly DateOnly LocalToday = new(2026, 10, 24);
    private static readonly DateTimeOffset ReviewAtUtc =
        new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Creates_Persisted_Proposal_Only_When_A_Fresh_Review_Has_Changes()
    {
        var sessionId = Guid.NewGuid();
        var prescriptionId = Guid.NewGuid();
        var input = ReadyInput(sessionId);
        var source = new SyntheticSource(new ActivePlanPhaseAdjustmentSourceV010(input,
        [
            new PhaseAdjustmentSession(sessionId, ActivityModality.Strength,
                SessionTrainingStyle.General,
                [new PhaseAdjustmentExercise(prescriptionId, 12, 2, true, true)], null)
        ]));
        var store = new SyntheticStore();
        var useCase = new CreatePhaseAdjustmentProposalV010UseCase(source, store);

        var result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.True(result.IsPersisted);
        Assert.NotEqual(Guid.Empty, result.ProposalId);
        Assert.Equal(nameof(PhaseReviewGateStatus.ReadyForAdjustmentPreview), result.GateStatus);
        Assert.Equal(nameof(PhaseAdjustmentPreviewStatus.ChangesOrChoicesAvailable), result.Preview.Status);
        Assert.Equal(2m, Assert.Single(result.Preview.FieldChanges).AcceptedValue);
        Assert.Equal(1m, result.Preview.FieldChanges[0].ProposedValue);
        Assert.NotNull(store.SavedSnapshot);
    }

    [Fact]
    public async Task Current_Symptoms_Stop_Proposal_Persistence()
    {
        var input = ReadyInput(Guid.NewGuid()) with { HasCurrentSymptomsOrRestrictions = true };
        var source = new SyntheticSource(new ActivePlanPhaseAdjustmentSourceV010(input,
            [new PhaseAdjustmentSession(input.Sessions[0].SessionId,
                ActivityModality.Strength, SessionTrainingStyle.General, [], null)]));
        var store = new SyntheticStore();
        var useCase = new CreatePhaseAdjustmentProposalV010UseCase(source, store);

        var result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsPersisted);
        Assert.Equal(nameof(PhaseReviewGateStatus.CurrentSafetyReviewRequired), result.GateStatus);
        Assert.Null(store.SavedSnapshot);
    }

    private static PhaseReviewGateInput ReadyInput(Guid sessionId) => new(
        Guid.NewGuid(), Guid.NewGuid(), LocalToday.AddDays(6), ReviewAtUtc, LocalToday,
        new PhaseReviewContext(Guid.NewGuid(), CyclePhase.Menstrual,
            PhaseReviewProvenance.UserConfirmed, null),
        new PhaseBaseline(3, 2), PhaseBaselineScale.QuizMappedOneToFive,
        null, null, null, true, true,
        [new PhaseReviewSession(sessionId, LocalToday.AddDays(1),
            ReviewAtUtc.AddDays(1), PlannedSessionState.Planned, false, false)],
        new HashSet<PhaseReviewDismissalKey>());

    private sealed class SyntheticSource(ActivePlanPhaseAdjustmentSourceV010 state)
        : IActivePlanPhaseAdjustmentSourceV010
    {
        public Task<ActivePlanPhaseAdjustmentSourceV010?> LoadAsync(Guid userId, string? timeZoneId,
            CancellationToken cancellationToken) => Task.FromResult<ActivePlanPhaseAdjustmentSourceV010?>(state);
    }

    private sealed class SyntheticStore : IPhaseAdjustmentProposalStoreV010
    {
        public PhaseAdjustmentProposalSnapshotV010? SavedSnapshot { get; private set; }

        public Task<Guid> SaveAsync(Guid userId, PhaseAdjustmentProposalSnapshotV010 snapshot,
            PhaseAdjustmentPreview preview, CancellationToken cancellationToken)
        {
            SavedSnapshot = snapshot;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<DecidePhaseAdjustmentProposalResultV010> DecideAsync(Guid userId,
            PhaseAdjustmentProposalCommandV010 command, CancellationToken cancellationToken) =>
            Task.FromResult(new DecidePhaseAdjustmentProposalResultV010("Conflict", false, null));
    }
}
