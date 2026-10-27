using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

public interface IActivePlanPhaseReviewSourceV010
{
    Task<PhaseReviewGateInput?> LoadAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Loads first-party accepted-plan and confirmed-phase context for a read-only review gate.
/// A separate versioned adjustment configuration is required before creating proposals.
/// </summary>
public sealed class ReviewActivePlanPhaseV010UseCase(IActivePlanPhaseReviewSourceV010 source)
{
    public async Task<PhaseReviewGateResult> ExecuteAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return new PhaseReviewGateResult(PhaseReviewGateStatus.InvalidInput,
                default, default, Array.Empty<Guid>(), null);

        var input = await source.LoadAsync(userId, cancellationToken).ConfigureAwait(false);
        return input is null
            ? new PhaseReviewGateResult(PhaseReviewGateStatus.NoActivePlan,
                default, default, Array.Empty<Guid>(), null)
            : PhaseReviewRule.Evaluate(input);
    }
}

public sealed record ActivePlanPhaseAdjustmentSourceV010(
    PhaseReviewGateInput GateInput,
    IReadOnlyList<PhaseAdjustmentSession> AcceptedSessions,
    string TimeZoneId = "UTC");

public interface IActivePlanPhaseAdjustmentSourceV010
{
    Task<ActivePlanPhaseAdjustmentSourceV010?> LoadAsync(Guid userId, string? timeZoneId,
        CancellationToken cancellationToken);
}

public sealed record CreatePhaseAdjustmentProposalRequestV010(string? TimeZoneId);

public sealed record ActivePlanPhaseAdjustmentPreviewV010(
    PhaseReviewGateResult Gate,
    PhaseAdjustmentPreview Adjustment);

public sealed record PhaseAdjustmentFieldChangeDtoV010(
    Guid SessionId,
    Guid? PrescriptionId,
    int? ExerciseId,
    string Field,
    decimal AcceptedValue,
    decimal ProposedValue);

public sealed record PhaseAdjustmentChoiceOptionDtoV010(
    string Choice,
    string? ExistingTemplateId,
    string? DisplayName);

public sealed record PhaseAdjustmentSessionChoicesDtoV010(
    Guid SessionId,
    IReadOnlyList<PhaseAdjustmentChoiceOptionDtoV010> Options);

public sealed record PhaseAdjustmentPreviewDtoV010(
    string PolicyVersion,
    string Tier,
    string Status,
    IReadOnlyList<PhaseAdjustmentFieldChangeDtoV010> FieldChanges,
    IReadOnlyList<PhaseAdjustmentSessionChoicesDtoV010> SessionChoices,
    IReadOnlyDictionary<Guid, DateOnly> ScheduleDates,
    bool RequiresStrengthDurationRecalculation);

public sealed record CreatePhaseAdjustmentProposalResultV010(
    Guid ProposalId,
    string GateStatus,
    PhaseAdjustmentPreviewDtoV010 Preview,
    bool IsPersisted);

public sealed record PhaseAdjustmentChoiceSelectionDtoV010(
    Guid SessionId,
    string Choice,
    string? ExistingTemplateId);

public sealed record DecidePhaseAdjustmentProposalRequestV010(
    Guid ProposalId,
    bool ApplyChanges,
    IReadOnlyList<PhaseAdjustmentChoiceSelectionDtoV010>? Selections);

public sealed record DecidePhaseAdjustmentProposalResultV010(
    string Status,
    bool PlanChanged,
    string? MessageCode);

public sealed record PhaseAdjustmentProposalCommandV010(
    Guid ProposalId,
    bool ApplyChanges,
    IReadOnlyList<PhaseAdjustmentChoiceSelection> Selections);

public sealed record PhaseAdjustmentProposalSnapshotV010(
    Guid PlanId,
    Guid SourceRevisionId,
    string TimeZoneId,
    DateOnly PlanEndDate,
    DateOnly LocalToday,
    PhaseReviewContext Context,
    PhaseBaseline? Baseline,
    PhaseBaselineScale BaselineScale,
    bool HasCurrentSymptomsOrRestrictions,
    IReadOnlyList<Guid> EligibleSessionIds,
    IReadOnlyList<PhaseReviewSession> Sessions,
    IReadOnlyList<PhaseAdjustmentSession> AcceptedSessions)
{
    public static PhaseAdjustmentProposalSnapshotV010 Create(
        ActivePlanPhaseAdjustmentSourceV010 source, PhaseReviewGateResult gate) => new(
        source.GateInput.PlanId,
        source.GateInput.SourceRevisionId,
        source.TimeZoneId,
        source.GateInput.PlanEndDate,
        source.GateInput.LocalToday,
        source.GateInput.Context,
        source.GateInput.Baseline,
        source.GateInput.BaselineScale,
        source.GateInput.HasCurrentSymptomsOrRestrictions,
        Array.AsReadOnly(gate.EligibleSessionIds.Order().ToArray()),
        Array.AsReadOnly(source.GateInput.Sessions.OrderBy(session => session.SessionId).ToArray()),
        Array.AsReadOnly(source.AcceptedSessions.OrderBy(session => session.SessionId).ToArray()));
}

public interface IPhaseAdjustmentProposalStoreV010
{
    Task<Guid> SaveAsync(Guid userId, PhaseAdjustmentProposalSnapshotV010 snapshot,
        PhaseAdjustmentPreview preview, CancellationToken cancellationToken);

    Task<DecidePhaseAdjustmentProposalResultV010> DecideAsync(Guid userId,
        PhaseAdjustmentProposalCommandV010 command, CancellationToken cancellationToken);
}

/// <summary>Stores an immutable preview before a user may apply it to future sessions.</summary>
public sealed class CreatePhaseAdjustmentProposalV010UseCase(
    IActivePlanPhaseAdjustmentSourceV010 source,
    IPhaseAdjustmentProposalStoreV010 store)
{
    public async Task<CreatePhaseAdjustmentProposalResultV010> ExecuteAsync(Guid userId,
        string? timeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return Empty(PhaseReviewGateStatus.InvalidInput);
        var loaded = await source.LoadAsync(userId, timeZoneId, cancellationToken).ConfigureAwait(false);
        if (loaded is null)
            return Empty(PhaseReviewGateStatus.NoActivePlan);

        var gate = PhaseReviewRule.Evaluate(loaded.GateInput);
        var preview = PhaseAdjustmentRule.Preview(gate, loaded.GateInput.Baseline,
            loaded.GateInput.BaselineScale, loaded.AcceptedSessions);
        if (gate.Status != PhaseReviewGateStatus.ReadyForAdjustmentPreview ||
            preview.Status != PhaseAdjustmentPreviewStatus.ChangesOrChoicesAvailable)
            return new CreatePhaseAdjustmentProposalResultV010(Guid.Empty, gate.Status.ToString(),
                ToDto(preview, loaded.AcceptedSessions, loaded.GateInput.Sessions), false);

        var snapshot = PhaseAdjustmentProposalSnapshotV010.Create(loaded, gate);
        var proposalId = await store.SaveAsync(userId, snapshot, preview, cancellationToken)
            .ConfigureAwait(false);
        return new CreatePhaseAdjustmentProposalResultV010(proposalId, gate.Status.ToString(),
            ToDto(preview, loaded.AcceptedSessions, loaded.GateInput.Sessions), proposalId != Guid.Empty);
    }

    private static CreatePhaseAdjustmentProposalResultV010 Empty(PhaseReviewGateStatus status) =>
        new(Guid.Empty, status.ToString(), ToDto(new PhaseAdjustmentPreview(
                PhaseAdjustmentRule.Version, PhaseAdjustmentTier.Unavailable,
                PhaseAdjustmentPreviewStatus.Unavailable, [], [], false), [], []), false);

    internal static PhaseAdjustmentPreviewDtoV010 ToDto(PhaseAdjustmentPreview preview,
        IReadOnlyList<PhaseAdjustmentSession> sessions,
        IReadOnlyList<PhaseReviewSession> reviewSessions)
    {
        var optionsBySession = preview.SessionChoices.Select(sessionChoices =>
        {
            var session = sessions.SingleOrDefault(item => item.SessionId == sessionChoices.SessionId);
            var options = new List<PhaseAdjustmentChoiceOptionDtoV010>();
            foreach (var choice in sessionChoices.Choices)
            {
                if (choice is PhaseAdjustmentChoice.SelectEligibleEasyCardioTemplate or
                    PhaseAdjustmentChoice.SelectEligibleGentleTemplate)
                {
                    foreach (var template in session?.ExistingTemplateOptions?.Where(option =>
                                 option.Choice == choice) ?? [])
                        options.Add(new PhaseAdjustmentChoiceOptionDtoV010(choice.ToString(),
                            template.TemplateId, template.DisplayName));
                }
                else
                {
                    options.Add(new PhaseAdjustmentChoiceOptionDtoV010(choice.ToString(), null, null));
                }
            }
            return new PhaseAdjustmentSessionChoicesDtoV010(sessionChoices.SessionId,
                Array.AsReadOnly(options.ToArray()));
        }).ToArray();

        return new PhaseAdjustmentPreviewDtoV010(preview.PolicyVersion, preview.Tier.ToString(),
            preview.Status.ToString(), Array.AsReadOnly(preview.FieldChanges.Select(change =>
                new PhaseAdjustmentFieldChangeDtoV010(change.SessionId, change.PrescriptionId,
                    change.ExerciseId, change.Field.ToString(), change.AcceptedValue,
                    change.ProposedValue)).ToArray()), Array.AsReadOnly(optionsBySession),
            reviewSessions.Where(session => optionsBySession.Any(item => item.SessionId == session.SessionId))
                .ToDictionary(session => session.SessionId, session => session.LocalDate),
            preview.RequiresStrengthDurationRecalculation);
    }
}

public sealed class DecidePhaseAdjustmentProposalV010UseCase(IPhaseAdjustmentProposalStoreV010 store)
{
    public Task<DecidePhaseAdjustmentProposalResultV010> ExecuteAsync(Guid userId,
        DecidePhaseAdjustmentProposalRequestV010? request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || request is null || request.ProposalId == Guid.Empty ||
            request.Selections is null)
            return Task.FromResult(new DecidePhaseAdjustmentProposalResultV010(
                "InvalidInput", false, "PhaseAdjustmentRequestInvalid"));

        var selections = new List<PhaseAdjustmentChoiceSelection>(request.Selections.Count);
        foreach (var selection in request.Selections)
        {
            if (selection is null || selection.SessionId == Guid.Empty ||
                !Enum.TryParse<PhaseAdjustmentChoice>(selection.Choice, true, out var choice) ||
                !Enum.IsDefined(choice))
                return Task.FromResult(new DecidePhaseAdjustmentProposalResultV010(
                    "InvalidInput", false, "PhaseAdjustmentChoiceInvalid"));
            selections.Add(new PhaseAdjustmentChoiceSelection(selection.SessionId, choice,
                selection.ExistingTemplateId));
        }

        return store.DecideAsync(userId, new PhaseAdjustmentProposalCommandV010(
            request.ProposalId, request.ApplyChanges, Array.AsReadOnly(selections.ToArray())),
            cancellationToken);
    }
}

/// <summary>Returns a read-only phase-adjustment preview from the accepted prescription.</summary>
public sealed class PreviewActivePlanPhaseAdjustmentV010UseCase(
    IActivePlanPhaseAdjustmentSourceV010 source)
{
    public async Task<ActivePlanPhaseAdjustmentPreviewV010> ExecuteAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var loaded = userId == Guid.Empty ? null :
            await source.LoadAsync(userId, null, cancellationToken).ConfigureAwait(false);
        var gate = loaded is null
            ? new PhaseReviewGateResult(userId == Guid.Empty
                    ? PhaseReviewGateStatus.InvalidInput : PhaseReviewGateStatus.NoActivePlan,
                default, default, Array.Empty<Guid>(), null)
            : PhaseReviewRule.Evaluate(loaded.GateInput);
        var preview = PhaseAdjustmentRule.Preview(gate,
            loaded?.GateInput.Baseline,
            loaded?.GateInput.BaselineScale ?? PhaseBaselineScale.Unknown,
            loaded?.AcceptedSessions);
        return new ActivePlanPhaseAdjustmentPreviewV010(gate, preview);
    }
}
