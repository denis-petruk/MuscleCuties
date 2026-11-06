using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UI.Services.Api;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfilePersonalInfoViewModel
{
    private const int MeasurementPageSize = 8;
    private const string LoadFailure = "Profile_Measurement_LoadFailed";
    private const string WriteFailure = "Profile_Measurement_WriteFailed";
    private readonly BodyMeasurementClient? _measurementClient;
    private CancellationTokenSource? _measurementVisit;
    private MeasurementHistoryRow[] _measurementHistory = [];
    private BodyMeasurementOutputDto? _editingMeasurement;
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

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in XAML.")]
    public DateTime MinMeasurementDate => DateTime.MinValue;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in XAML.")]
    public IReadOnlyList<SelectionOption<string>> MeasurementSites => BodyMeasurementPresentation.Sites;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in XAML.")]
    public IReadOnlyList<SelectionOption<string>> MeasurementSides => BodyMeasurementPresentation.Sides;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound in XAML.")]
    public IReadOnlyList<SelectionOption<string>> MeasurementUnits => BodyMeasurementPresentation.Units;

    public bool IsMeasurementSideRequired => BodyMeasurementPresentation.NeedsSide(MeasurementSite?.Value);

    public string MeasurementValueLabel => UiTextCatalog.Format("Profile_Measurement_Value",
        UiTextCatalog.Get(MeasurementUnit?.Label ?? "Common_Unit_Cm"));

    public bool CanUseMeasurementHistory => IsMeasurementHistoryLoaded && !IsMeasurementBusy;
    public bool CanEditMeasurement => !IsMeasurementBusy && !CanRetryMeasurementMutation;
    public bool CanSaveMeasurement => CanEditMeasurement && !MeasurementRecordChanged;
    public bool ShowEmptyMeasurements => IsMeasurementHistoryLoaded && VisibleMeasurements.Count == 0;
    public bool HasMoreMeasurements => VisibleMeasurements.Count < _measurementHistory.Length;

    [RelayCommand]
    private async Task OpenMeasurementHistoryAsync()
    {
        if (IsMeasurementHistoryOpen)
            return;

        IsMeasurementHistoryOpen = true;
        await RefreshMeasurementHistoryAsync();
    }

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

    public bool BeginEditMeasurement(MeasurementHistoryRow reading)
    {
        if (!CanUseMeasurementHistory || IsMeasurementEditorOpen)
            return false;

        StartMeasurementEditor(reading.Reading);
        return true;
    }

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

    [RelayCommand]
    private void ShowMoreMeasurements()
    {
        _measurementVisibleCount += MeasurementPageSize;
        UpdateVisibleMeasurements();
    }

    [RelayCommand]
    private void RequestMeasurementDelete()
    {
        if (CanSaveMeasurement && _editingMeasurement is not null)
            IsMeasurementDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void CancelMeasurementDelete() => IsMeasurementDeleteConfirmationOpen = false;

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

    [RelayCommand]
    private Task RetryMeasurementAsync() => SendMeasurementAsync();

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

    partial void OnMeasurementSiteChanged(SelectionOption<string>? value) => MeasurementSide = null;

    private CancellationToken MeasurementVisitToken() => (_measurementVisit ??= new CancellationTokenSource()).Token;

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

    private async Task CompleteMeasurementAttemptAsync(string outcomeKey, CancellationToken token)
    {
        ResetMeasurementEditor();
        await ReadMeasurementsAsync(token);
        MeasurementStatus = UiTextCatalog.Get(outcomeKey) +
            (string.IsNullOrWhiteSpace(MeasurementStatus) ? string.Empty : $" {MeasurementStatus}");
    }

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

    private void UpdateVisibleMeasurements() => VisibleMeasurements = _measurementHistory.Take(_measurementVisibleCount).ToArray();

    private void ClearMeasurementHistory()
    {
        IsMeasurementHistoryLoaded = false;
        _measurementHistory = [];
        _measurementVisibleCount = 0;
        UpdateVisibleMeasurements();
    }

    private void ClearMeasurementNotice()
    {
        MeasurementStatus = string.Empty;
        MeasurementReference = string.Empty;
    }

    private void ShowMeasurementFailure(ApiFailureNotice notice)
    {
        MeasurementStatus = notice.MessageText;
        MeasurementReference = notice.ReferenceText;
    }
}
