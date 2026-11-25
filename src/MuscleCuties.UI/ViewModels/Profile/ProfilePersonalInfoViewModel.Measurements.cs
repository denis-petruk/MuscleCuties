// Measurement history and unit conversion.
// Second half of ProfilePersonalInfoViewModel; see its declaration for the full contract.

using System.Diagnostics.CodeAnalysis;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Services.Api;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>The measurements half of the personal-info page: the paged history, the single-record editor, and the</summary>
/// retry path that keeps a refused write rather than making the user re-enter it.</summary>
public partial class ProfilePersonalInfoViewModel
{
    private const int MeasurementPageSize = 8;
    private const string LoadFailure = "Profile_Measurement_LoadFailed";
    private const string WriteFailure = "Profile_Measurement_WriteFailed";
    private readonly BodyMeasurementClient? _measurementClient;
    private CancellationTokenSource? _measurementVisit;
    private MeasurementHistoryRow[] _measurementHistory = [];
    private BodyMeasurementOutputDto? _editingMeasurement;
    /// <summary>The write that is about to be sent, held as a call rather than a request so a refused attempt can be</summary>
    /// replayed unchanged and its outcome reported in its own words.</summary>
    private sealed record PendingMeasurement(
        string SuccessKey,
        Func<CancellationToken, Task<BackendApiResponse<BodyMeasurementOutputDto>>> SendAsync);

    private PendingMeasurement? _pendingMeasurement;
    private bool _measurementDefaultsLoaded;
    private bool _preferMetricMeasurements = true;
    private int _measurementVisibleCount;

    [ObservableProperty] private bool _isMeasurementHistoryOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseMeasurementHistory))]
    [NotifyPropertyChangedFor(nameof(CanEditMeasurement))]
    [NotifyPropertyChangedFor(nameof(CanSaveMeasurement))]
    private bool _isMeasurementBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseMeasurementHistory))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyMeasurements))]
    private bool _isMeasurementHistoryLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyMeasurements))]
    [NotifyPropertyChangedFor(nameof(HasMoreMeasurements))]
    private IReadOnlyList<MeasurementHistoryRow> _visibleMeasurements = [];

    [ObservableProperty] private bool _isMeasurementEditorOpen;
    [ObservableProperty] private bool _isMeasurementExisting;
    [ObservableProperty] private bool _isMeasurementDeleteConfirmationOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditMeasurement))]
    [NotifyPropertyChangedFor(nameof(CanSaveMeasurement))]
    private bool _canRetryMeasurementMutation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveMeasurement))]
    private bool _measurementRecordChanged;

    [ObservableProperty] private string _measurementStatus = string.Empty;
    [ObservableProperty] private string _measurementReference = string.Empty;
    [ObservableProperty] private string _measurementValue = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMeasurementSideRequired))]
    private SelectionOption<string>? _measurementSite;

    [ObservableProperty] private SelectionOption<string>? _measurementSide;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MeasurementValueLabel))]
    private SelectionOption<string>? _measurementUnit;

    [ObservableProperty] private DateTime _measurementDate = DateTime.Today;
    [ObservableProperty] private DateTime _maxMeasurementDate = DateTime.Today;

    /// <summary>The earliest date a measurement can take.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in XAML.")]
    public DateTime MinMeasurementDate => DateTime.MinValue;

    /// <summary>The body sites a measurement can record.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in XAML.")]
    public IReadOnlyList<SelectionOption<string>> MeasurementSites => BodyMeasurementPresentation.Sites;

    /// <summary>The sides a measurement can record.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in XAML.")]
    public IReadOnlyList<SelectionOption<string>> MeasurementSides => BodyMeasurementPresentation.Sides;

    /// <summary>The units a measurement can use.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in XAML.")]
    public IReadOnlyList<SelectionOption<string>> MeasurementUnits => BodyMeasurementPresentation.Units;

    /// <summary>Only sites that can be measured on either side ask for one, so the control is hidden for the rest.</summary>
    public bool IsMeasurementSideRequired => BodyMeasurementPresentation.NeedsSide(MeasurementSite?.Value);

    /// <summary>The value field's label names the unit currently selected, which is what the number will be stored in.</summary>
    public string MeasurementValueLabel => UiTextCatalog.Format("Profile_Measurement_Value",
        UiTextCatalog.Get(MeasurementUnit?.Label ?? "Common_Unit_Cm"));

    /// <summary>True when the history can be opened.</summary>
    public bool CanUseMeasurementHistory => IsMeasurementHistoryLoaded && !IsMeasurementBusy;
    /// <summary>True when a measurement can be edited.</summary>
    public bool CanEditMeasurement => !IsMeasurementBusy && !CanRetryMeasurementMutation;
    /// <summary>True when the measurement can be saved.</summary>
    public bool CanSaveMeasurement => CanEditMeasurement && !MeasurementRecordChanged;
    /// <summary>True when the history is empty.</summary>
    public bool ShowEmptyMeasurements => IsMeasurementHistoryLoaded && VisibleMeasurements.Count == 0;
    /// <summary>Whether the paged list still has rows to reveal, which is what shows the show-more action.</summary>
    public bool HasMoreMeasurements => VisibleMeasurements.Count < _measurementHistory.Length;

    /// <summary>Opens the measurement history.</summary>
    [RelayCommand]
    private async Task OpenMeasurementHistoryAsync()
    {
        if (IsMeasurementHistoryOpen)
            return;

        IsMeasurementHistoryOpen = true;
        await RefreshMeasurementHistoryAsync();
    }

    /// <summary>Reloads the measurement history.</summary>
    [RelayCommand]
    private async Task RefreshMeasurementHistoryAsync()
    {
        if (IsMeasurementBusy || IsMeasurementEditorOpen)
            return;

        var token = MeasurementVisitToken();
        IsMeasurementBusy = true;
        ClearMeasurementNotice();
        try
        {
            await ReadMeasurementsAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            if (!token.IsCancellationRequested)
                IsMeasurementBusy = false;
        }
    }

    /// <summary>Starts a new measurement.</summary>
    [RelayCommand]
    private async Task AddMeasurementAsync()
    {
        if (!CanUseMeasurementHistory || IsMeasurementEditorOpen)
            return;

        var token = MeasurementVisitToken();
        IsMeasurementBusy = true;
        ClearMeasurementNotice();
        try
        {
            if (!_measurementDefaultsLoaded)
            {
                var result = await _units.LoadAsync(token);
                token.ThrowIfCancellationRequested();
                _preferMetricMeasurements = result.Settings?.UseMetricSystem ?? true;
                _measurementDefaultsLoaded = result.Settings is not null;
            }
            StartMeasurementEditor(null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            // The unit picker is always available, even when profile settings cannot be fetched.
            if (!token.IsCancellationRequested)
                StartMeasurementEditor(null);
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsMeasurementBusy = false;
        }
    }

    /// <summary>Opens one stored reading for editing and reports whether it did; it refuses while the history is still</summary>
    /// loading or busy, and while another record is already open.</summary>
    public bool BeginEditMeasurement(MeasurementHistoryRow reading)
    {
        if (!CanUseMeasurementHistory || IsMeasurementEditorOpen)
            return false;

        StartMeasurementEditor(reading.Reading);
        return true;
    }

    /// <summary>Cancels the measurement being entered.</summary>
    [RelayCommand]
    private async Task CancelMeasurementAsync()
    {
        if (IsMeasurementBusy)
            return;

        var refreshNeeded = _pendingMeasurement is not null;
        ResetMeasurementEditor();
        ClearMeasurementNotice();
        if (refreshNeeded)
            await RefreshMeasurementHistoryAsync();
    }

    /// <summary>Shows more of the history.</summary>
    [RelayCommand]
    private void ShowMoreMeasurements()
    {
        _measurementVisibleCount += MeasurementPageSize;
        UpdateVisibleMeasurements();
    }

    /// <summary>Asks before deleting the measurement.</summary>
    [RelayCommand]
    private void RequestMeasurementDelete()
    {
        if (CanSaveMeasurement && _editingMeasurement is not null)
            IsMeasurementDeleteConfirmationOpen = true;
    }

    /// <summary>Closes the delete confirmation.</summary>
    [RelayCommand]
    private void CancelMeasurementDelete() => IsMeasurementDeleteConfirmationOpen = false;

    /// <summary>Deletes the measurement.</summary>
    [RelayCommand]
    private Task DeleteMeasurementAsync()
    {
        if (_measurementClient is null || !CanSaveMeasurement || !IsMeasurementDeleteConfirmationOpen || _editingMeasurement is not { } reading)
            return Task.CompletedTask;

        var input = new DeleteBodyMeasurementInputDto(reading.Id, Guid.CreateVersion7(), reading.Version);
        IsMeasurementDeleteConfirmationOpen = false;
        _pendingMeasurement = new("Profile_Measurement_Deleted", token => _measurementClient.DeleteAsync(input, token));
        return SendMeasurementAsync();
    }

    /// <summary>Saves the measurement.</summary>
    [RelayCommand]
    private Task SaveMeasurementAsync()
    {
        if (_measurementClient is null || !CanSaveMeasurement || !IsMeasurementEditorOpen || IsMeasurementDeleteConfirmationOpen)
            return Task.CompletedTask;

        var site = MeasurementSite?.Value;
        var side = IsMeasurementSideRequired ? MeasurementSide?.Value : BodyMeasurementPresentation.NoSide;
        var unit = MeasurementUnit?.Value;
        var date = DateOnly.FromDateTime(MeasurementDate);
        if (date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            MeasurementStatus = UiTextCatalog.Get("Profile_Measurement_DateNotAvailable");
            MeasurementReference = string.Empty;
            return Task.CompletedTask;
        }
        var id = _editingMeasurement?.Id ?? Guid.CreateVersion7();
        if (!BodyMeasurementPresentation.TryParseDraft(MeasurementValue, site, side, unit, date, id, out var value))
        {
            MeasurementStatus = UiTextCatalog.Get("Profile_Measurement_Invalid");
            MeasurementReference = string.Empty;
            return Task.CompletedTask;
        }

        var commandId = Guid.CreateVersion7();
        if (_editingMeasurement is { } reading)
        {
            var input = new CorrectBodyMeasurementInputDto(id, commandId, reading.Version, site!, side!, date, value, unit!);
            _pendingMeasurement = new("Profile_Measurement_Saved", token => _measurementClient.CorrectAsync(input, token));
        }
        else
        {
            var input = new CreateBodyMeasurementInputDto(id, commandId, site!, side!, date, value, unit!);
            _pendingMeasurement = new("Profile_Measurement_Saved", token => _measurementClient.CreateAsync(input, token));
        }

        return SendMeasurementAsync();
    }

    /// <summary>Retries the failed measurement change.</summary>
    [RelayCommand]
    private Task RetryMeasurementAsync() => SendMeasurementAsync();

    /// <summary>Cancels the page's measurement work when it goes away, so answers that arrive afterwards are ignored instead</summary>
    /// of writing into a closed page.</summary>
    public void ResetMeasurementVisit()
    {
        _measurementVisit?.Cancel();
        _measurementVisit?.Dispose();
        _measurementVisit = null;
        ResetMeasurementEditor();
        ClearMeasurementHistory();
        ClearMeasurementNotice();
        IsMeasurementHistoryOpen = false;
        IsMeasurementBusy = false;
        _measurementDefaultsLoaded = false;
        _preferMetricMeasurements = true;
    }

    /// <summary>Clears the side when the site changes.</summary>
    partial void OnMeasurementSiteChanged(SelectionOption<string>? value) => MeasurementSide = null;

    /// <summary>The token that cancels the current history visit.</summary>
    private CancellationToken MeasurementVisitToken() => (_measurementVisit ??= new CancellationTokenSource()).Token;

    /// <summary>Opens the editor on a reading, or empty for a new one, and clears any notice left by the previous attempt.</summary>
    private void StartMeasurementEditor(BodyMeasurementOutputDto? reading)
    {
        ClearMeasurementNotice();
        _editingMeasurement = reading;
        var today = DateTime.Today;
        var draftDate = reading?.LocalDate.ToDateTime(TimeOnly.MinValue) ?? today;
        MaxMeasurementDate = draftDate > today ? draftDate : today;
        MeasurementDate = draftDate;
        MeasurementSite = MeasurementSites.FirstOrDefault(site => site.Value == reading?.Site);
        MeasurementSide = MeasurementSides.FirstOrDefault(side => side.Value == reading?.Side);
        MeasurementUnit = BodyMeasurementPresentation.FindUnit(reading?.EnteredUnit, _preferMetricMeasurements);
        MeasurementValue = reading?.EnteredValue.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;
        IsMeasurementExisting = reading is not null;
        IsMeasurementEditorOpen = true;
    }

    /// <summary>Closes the editor and drops its draft together with the write that was waiting on it.</summary>
    private void ResetMeasurementEditor()
    {
        IsMeasurementEditorOpen = false;
        IsMeasurementExisting = false;
        IsMeasurementDeleteConfirmationOpen = false;
        CanRetryMeasurementMutation = false;
        MeasurementRecordChanged = false;
        _editingMeasurement = null;
        _pendingMeasurement = null;
        MeasurementValue = string.Empty;
    }

    /// <summary>Sends the pending write once and keeps it pending when it is refused, so the retry action can resend the</summary>
    /// same values.</summary>
    private async Task SendMeasurementAsync()
    {
        if (IsMeasurementBusy || _pendingMeasurement is not { } pending)
            return;

        var token = MeasurementVisitToken();
        IsMeasurementBusy = true;
        ClearMeasurementNotice();
        try
        {
            var response = await pending.SendAsync(token);
            token.ThrowIfCancellationRequested();
            switch (response.Status)
            {
                case BackendApiStatus.Success:
                    await CompleteMeasurementAttemptAsync(pending.SuccessKey, token);
                    break;
                case BackendApiStatus.Conflict:
                case BackendApiStatus.NotFound:
                    _pendingMeasurement = null;
                    CanRetryMeasurementMutation = false;
                    await ReadMeasurementsAsync(token);
                    MeasurementRecordChanged = _editingMeasurement is { } editing && !_measurementHistory.Any(row =>
                        row.Reading.Id == editing.Id && row.Reading.Version == editing.Version);
                    MeasurementStatus = UiTextCatalog.Get(MeasurementRecordChanged
                        ? "Profile_Measurement_RecordChanged"
                        : "Profile_Measurement_Conflict");
                    break;
                case BackendApiStatus.Unauthorized:
                    ResetMeasurementEditor();
                    ClearMeasurementHistory();
                    ShowMeasurementFailure(ApiFailurePresenter.FromResponse(response, WriteFailure));
                    break;
                case BackendApiStatus.ValidationFailed:
                    _pendingMeasurement = null;
                    CanRetryMeasurementMutation = false;
                    ShowMeasurementFailure(ApiFailurePresenter.FromResponse(response, "Profile_Measurement_Invalid"));
                    break;
                default:
                    CanRetryMeasurementMutation = true;
                    ShowMeasurementFailure(ApiFailurePresenter.FromResponse(response, WriteFailure));
                    break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (token.IsCancellationRequested)
                return;

            CanRetryMeasurementMutation = true;
            ShowMeasurementFailure(ApiFailurePresenter.FromException(exception, WriteFailure));
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsMeasurementBusy = false;
        }
    }

    /// <summary>After a write, reloads the history and reports the outcome plus whatever the reload itself had to say.</summary>
    private async Task CompleteMeasurementAttemptAsync(string outcomeKey, CancellationToken token)
    {
        ResetMeasurementEditor();
        await ReadMeasurementsAsync(token);
        MeasurementStatus = UiTextCatalog.Get(outcomeKey) +
            (string.IsNullOrWhiteSpace(MeasurementStatus) ? string.Empty : $" {MeasurementStatus}");
    }

    /// <summary>Loads the first page of history and marks it loaded, or reports the failure and leaves it unloaded so it can</summary>
    /// be asked for again.</summary>
    private async Task ReadMeasurementsAsync(CancellationToken token)
    {
        if (_measurementClient is null)
        {
            ClearMeasurementHistory();
            return;
        }

        BackendApiResponse<BodyMeasurementHistoryOutputDto> response;
        try
        {
            response = await _measurementClient.LoadAsync(DateOnly.FromDateTime(DateTime.UtcNow), token);
        }
        catch (Exception exception)
        {
            token.ThrowIfCancellationRequested();
            ClearMeasurementHistory();
            ShowMeasurementFailure(ApiFailurePresenter.FromException(exception, LoadFailure));
            return;
        }
        token.ThrowIfCancellationRequested();
        if (response.Status != BackendApiStatus.Success || response.Data?.Readings is not { } readings)
        {
            ClearMeasurementHistory();
            ShowMeasurementFailure(ApiFailurePresenter.FromResponse(response, LoadFailure));
            return;
        }

        _measurementHistory = readings.OrderByDescending(reading => reading.LocalDate)
            .ThenBy(reading => reading.Site, StringComparer.Ordinal)
            .ThenBy(reading => reading.Side, StringComparer.Ordinal)
            .Select(reading => new MeasurementHistoryRow(reading))
            .ToArray();
        _measurementVisibleCount = Math.Max(_measurementVisibleCount, MeasurementPageSize);
        UpdateVisibleMeasurements();
        IsMeasurementHistoryLoaded = true;
    }

    /// <summary>Refreshes the shown slice of the history.</summary>
    private void UpdateVisibleMeasurements() => VisibleMeasurements = _measurementHistory.Take(_measurementVisibleCount).ToArray();

    /// <summary>Forgets the loaded rows, so reopening the history fetches fresh ones instead of showing stale measurements.</summary>
    private void ClearMeasurementHistory()
    {
        IsMeasurementHistoryLoaded = false;
        _measurementHistory = [];
        _measurementVisibleCount = 0;
        UpdateVisibleMeasurements();
    }

    /// <summary>Drops the status and support reference from a previous attempt before a new one starts.</summary>
    private void ClearMeasurementNotice()
    {
        MeasurementStatus = string.Empty;
        MeasurementReference = string.Empty;
    }

    /// <summary>Shows the API's own message and reference for a refused measurement, so a report can name the exact</summary>
    /// request.</summary>
    private void ShowMeasurementFailure(ApiFailureNotice notice)
    {
        MeasurementStatus = notice.MessageText;
        MeasurementReference = notice.ReferenceText;
    }
}
