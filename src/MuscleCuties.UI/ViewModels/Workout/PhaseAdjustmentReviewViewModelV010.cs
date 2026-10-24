using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

public sealed record PhaseAdjustmentChoiceOptionDisplayV010(
    string Choice,
    string? ExistingTemplateId,
    string Label);

public partial class PhaseAdjustmentSessionChoiceRowV010 : ObservableObject
{
    public PhaseAdjustmentSessionChoiceRowV010(Guid sessionId, string title,
        IReadOnlyList<PhaseAdjustmentChoiceOptionDisplayV010> options,
        PhaseAdjustmentChoiceOptionDisplayV010? selectedOption)
    {
        SessionId = sessionId;
        Title = title;
        Options = new ObservableCollection<PhaseAdjustmentChoiceOptionDisplayV010>(options);
        SelectedOption = selectedOption;
    }

    public Guid SessionId { get; }
    public string Title { get; }
    public ObservableCollection<PhaseAdjustmentChoiceOptionDisplayV010> Options { get; }

    [ObservableProperty] private PhaseAdjustmentChoiceOptionDisplayV010? _selectedOption;
}

public partial class PhaseAdjustmentReviewViewModelV010 : ObservableObject
{
    private const int NoDisplayItems = 0;
    private static readonly ConcurrentDictionary<string, CompositeFormat> FormatCache = new(StringComparer.Ordinal);
    private readonly IPhaseAdjustmentProposalClientV010 _proposalClient;
    private readonly Func<Task> _navigateBackAsync;
    private CreatePhaseAdjustmentProposalResultV010? _proposal;

    [ObservableProperty] private ObservableCollection<string> _fieldChanges = [];
    [ObservableProperty] private bool _hasProposal;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<PhaseAdjustmentSessionChoiceRowV010> _sessionChoices = [];

    public PhaseAdjustmentReviewViewModelV010(IPhaseAdjustmentProposalClientV010 proposalClient,
        Func<Task> navigateBackAsync)
    {
        _proposalClient = proposalClient;
        _navigateBackAsync = navigateBackAsync;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ApplyChangesCommand = new AsyncRelayCommand(ApplyChangesAsync, CanApplyChanges);
        KeepPlanCommand = new AsyncRelayCommand(KeepPlanAsync, CanKeepPlan);
        BackCommand = new AsyncRelayCommand(_navigateBackAsync);
    }

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand ApplyChangesCommand { get; }
    public AsyncRelayCommand KeepPlanCommand { get; }
    public AsyncRelayCommand BackCommand { get; }

    public bool HasFieldChanges => FieldChanges.Count > NoDisplayItems;
    public bool HasSessionChoices => SessionChoices.Count > NoDisplayItems;

    private async Task LoadAsync()
    {
        IsBusy = true;
        HasProposal = false;
        _proposal = null;
        FieldChanges.Clear();
        SessionChoices.Clear();
        StatusMessage = UiTextCatalog.Get("Workout_PhaseAdjustment_Loading");
        try
        {
            _proposal = await _proposalClient.CreateAsync(TimeZoneInfo.Local.Id);
            if (!_proposal.IsPersisted || _proposal.ProposalId == Guid.Empty)
            {
                StatusMessage = GetGateMessage(_proposal.GateStatus, _proposal.Preview.Status);
                return;
            }

            foreach (var change in _proposal.Preview.FieldChanges)
                FieldChanges.Add(FormatChange(change));
            foreach (var session in _proposal.Preview.SessionChoices)
            {
                var options = session.Options.Select(option => new PhaseAdjustmentChoiceOptionDisplayV010(
                    option.Choice, option.ExistingTemplateId, FormatChoice(option))).ToArray();
                var initialChoice = options.FirstOrDefault(option => option.Choice == "KeepCurrentSession") ??
                                    options.FirstOrDefault();
                var date = _proposal.Preview.ScheduleDates.GetValueOrDefault(session.SessionId);
                var title = date is { } localDate
                    ? FormatLocalized("Workout_PhaseAdjustment_SessionDateFormat",
                        localDate.ToString("D", CultureInfo.CurrentCulture))
                    : UiTextCatalog.Get("Workout_PhaseAdjustment_SessionTitle");
                SessionChoices.Add(new PhaseAdjustmentSessionChoiceRowV010(session.SessionId,
                    title, options, initialChoice));
            }

            HasProposal = true;
            StatusMessage = UiTextCatalog.Get("Workout_PhaseAdjustment_ConfirmationPrompt");
        }
        catch (Exception)
        {
            StatusMessage = UiTextCatalog.Get("Workout_PhaseAdjustment_LoadFailed");
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
            OnPropertyChanged(nameof(HasFieldChanges));
            OnPropertyChanged(nameof(HasSessionChoices));
        }
    }

    private bool CanApplyChanges() => !IsBusy && HasProposal && _proposal is { IsPersisted: true } &&
        SessionChoices.All(session => session.SelectedOption is not null);

    private bool CanKeepPlan() => !IsBusy && HasProposal && _proposal is { IsPersisted: true };

    private async Task ApplyChangesAsync()
    {
        if (!CanApplyChanges() || _proposal is null)
            return;
        IsBusy = true;
        NotifyCommands();
        try
        {
            var selections = SessionChoices.Select(session => new PhaseAdjustmentChoiceSelectionDtoV010(
                session.SessionId, session.SelectedOption!.Choice,
                session.SelectedOption.ExistingTemplateId)).ToArray();
            var result = await _proposalClient.DecideAsync(new DecidePhaseAdjustmentProposalRequestV010(
                _proposal.ProposalId, true, selections));
            StatusMessage = result.Status == "Accepted"
                ? UiTextCatalog.Get("Workout_PhaseAdjustment_Applied")
                : GetDecisionMessage(result);
            if (result.Status is "Accepted" or "NoChanges")
                await _navigateBackAsync();
        }
        catch (Exception)
        {
            StatusMessage = UiTextCatalog.Get("Workout_PhaseAdjustment_ApplyFailed");
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    private async Task KeepPlanAsync()
    {
        if (!CanKeepPlan() || _proposal is null)
            return;
        IsBusy = true;
        NotifyCommands();
        try
        {
            var result = await _proposalClient.DecideAsync(new DecidePhaseAdjustmentProposalRequestV010(
                _proposal.ProposalId, false, Array.Empty<PhaseAdjustmentChoiceSelectionDtoV010>()));
            if (result.Status == "Rejected")
                await _navigateBackAsync();
            else
                StatusMessage = GetDecisionMessage(result);
        }
        catch (Exception)
        {
            StatusMessage = UiTextCatalog.Get("Workout_PhaseAdjustment_KeepFailed");
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    private static string FormatChange(PhaseAdjustmentFieldChangeDtoV010 change) =>
        change.Field switch
        {
            "ExerciseSets" => FormatLocalized("Workout_PhaseAdjustment_SetChangeFormat",
                change.AcceptedValue, change.ProposedValue),
            "ContinuousMainMinutes" => FormatLocalized("Workout_PhaseAdjustment_DurationChangeFormat",
                change.AcceptedValue, change.ProposedValue),
            _ => UiTextCatalog.Get("Workout_PhaseAdjustment_ChangeUnavailable")
        };

    private static string FormatChoice(PhaseAdjustmentChoiceOptionDtoV010 option)
    {
        var key = option.Choice switch
        {
            "KeepCurrentSession" => "Workout_PhaseAdjustment_Keep",
            "SkipFutureSession" => "Workout_PhaseAdjustment_Skip",
            "SelectEligibleEasyCardioTemplate" => "Workout_PhaseAdjustment_EasyCardio",
            "SelectEligibleGentleTemplate" => "Workout_PhaseAdjustment_Gentle",
            _ => "Workout_PhaseAdjustment_ChoiceUnavailable"
        };
        var label = UiTextCatalog.Get(key);
        return string.IsNullOrWhiteSpace(option.DisplayName)
            ? label
            : FormatLocalized("Workout_PhaseAdjustment_TemplateFormat", label, option.DisplayName);
    }

    private static string FormatLocalized(string key, params object?[] arguments)
    {
        var format = UiTextCatalog.Get(key);
        var compositeFormat = FormatCache.GetOrAdd(format,
            static value => CompositeFormat.Parse(value));
        return string.Format(CultureInfo.CurrentCulture, compositeFormat, arguments);
    }

    private static string GetGateMessage(string gateStatus, string previewStatus)
    {
        var key = gateStatus switch
        {
            "TrackingDisabled" => "Workout_PhaseAdjustment_TrackingDisabled",
            "UnknownPhase" => "Workout_PhaseAdjustment_UnknownPhase",
            "NeedsPhaseConfirmation" => "Workout_PhaseAdjustment_ConfirmPhase",
            "MissingBaseline" => "Workout_PhaseAdjustment_MissingBaseline",
            "CurrentSafetyReviewRequired" => "Workout_PhaseAdjustment_CurrentSafetyReviewRequired",
            "AlreadyReviewed" or "Dismissed" => "Workout_PhaseAdjustment_AlreadyReviewed",
            "NoEligibleFutureSessions" => "Workout_PhaseAdjustment_NoEligibleSessions",
            "NoActivePlan" => "Workout_PhaseAdjustment_NoActivePlan",
            "InvalidInput" => "Workout_PhaseAdjustment_LoadFailed",
            _ => previewStatus == "NoMeaningfulChange"
                ? "Workout_PhaseAdjustment_NoChanges"
                : "Workout_PhaseAdjustment_NoEligibleSessions"
        };
        return UiTextCatalog.Get(key);
    }

    private static string GetDecisionMessage(DecidePhaseAdjustmentProposalResultV010 result) =>
        result.Status == "Expired"
            ? UiTextCatalog.Get("Workout_PhaseAdjustment_ReviewExpired")
            : result.Status == "NoChanges"
                ? UiTextCatalog.Get("Workout_PhaseAdjustment_NoChanges")
            : UiTextCatalog.Get("Workout_PhaseAdjustment_ApplyFailed");

    private void NotifyCommands()
    {
        ApplyChangesCommand.NotifyCanExecuteChanged();
        KeepPlanCommand.NotifyCanExecuteChanged();
    }
}
