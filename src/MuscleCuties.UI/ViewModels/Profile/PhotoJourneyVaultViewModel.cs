using MuscleCuties.UI.Models.UI.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>
///     Photo vault state: the stored photos, the selected comparison, and the privacy toggles.
/// </summary>
public partial class PhotoJourneyVaultViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<Task> _navigateBackAsync;
    private readonly IPhotoVaultUseCase _useCase;
    private int? _firstComparisonId;
    private int? _secondComparisonId;
    private int _privacyRevision;
    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty] private bool _isEmpty;

    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isPrivacyBlurEnabled = true;
    [ObservableProperty] private ObservableCollection<PhotoVaultItem> _photos = new();
    [ObservableProperty] private int _totalPhotosCount;
    [ObservableProperty] private bool _isComparisonOpen;
    [ObservableProperty] private string? _comparisonFirstImagePath;
    [ObservableProperty] private string? _comparisonSecondImagePath;
    [ObservableProperty] private string _comparisonFirstDateText = string.Empty;
    [ObservableProperty] private string _comparisonSecondDateText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isStatusError;
    [ObservableProperty] private string _comparisonSelectionBannerText = string.Empty;
    [ObservableProperty] private bool _hasPendingComparisonSelection;

    /// <summary>Takes the photo use case and the back navigation callback.</summary>
    public PhotoJourneyVaultViewModel(
        IPhotoVaultUseCase useCase,
        Func<Task> navigateBackAsync)
    {
        _useCase = useCase;
        _navigateBackAsync = navigateBackAsync;

        LoadDataCommand = new AsyncRelayCommand(() => _loadGate.RunAsync(LoadPhotosCoreAsync, this));
        TogglePrivacyBlurCommand = new RelayCommand(TogglePrivacyBlur);
        ToggleItemRevealCommand = new RelayCommand<PhotoVaultItem>(ToggleItemReveal);
        DeletePhotoCommand = new AsyncRelayCommand<PhotoVaultItem>(DeletePhotoAsync);
        SelectFirstComparisonPhotoCommand = new AsyncRelayCommand<PhotoVaultItem>(item => SelectComparisonPhotoAsync(item, true));
        SelectSecondComparisonPhotoCommand = new AsyncRelayCommand<PhotoVaultItem>(item => SelectComparisonPhotoAsync(item, false));
        CloseComparisonCommand = new RelayCommand(ResetComparison);
        NavigateBackCommand = new AsyncRelayCommand(NavigateBackAsync);
    }

    /// <summary>The blur control names what tapping it will do, rather than the state it is in.</summary>
    public string PrivacyBlurButtonText => IsPrivacyBlurEnabled ? "Reveal All" : "Blur All";
    /// <summary>True when a status message is shown.</summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    /// <summary>Comparing needs two photos whose files are still on the device, so a missing file cannot open a comparison</summary>
    /// against a blank half.</summary>
    public bool CanComparePhotos => Photos.Count(p => p.IsImageAvailable) >= 2;
    /// <summary>Shows or hides the privacy blur.</summary>
    public RelayCommand TogglePrivacyBlurCommand { get; }
    /// <summary>Reveals or hides one photo.</summary>
    public RelayCommand<PhotoVaultItem> ToggleItemRevealCommand { get; }
    /// <summary>Deletes a photo.</summary>
    public AsyncRelayCommand<PhotoVaultItem> DeletePhotoCommand { get; }
    /// <summary>Chooses the first comparison photo.</summary>
    public AsyncRelayCommand<PhotoVaultItem> SelectFirstComparisonPhotoCommand { get; }
    /// <summary>Chooses the second comparison photo.</summary>
    public AsyncRelayCommand<PhotoVaultItem> SelectSecondComparisonPhotoCommand { get; }
    /// <summary>Closes the comparison.</summary>
    public RelayCommand CloseComparisonCommand { get; }
    /// <summary>Returns to the previous page.</summary>
    public AsyncRelayCommand NavigateBackCommand { get; }

    /// <summary>The first-load indicator; reloading the vault keeps the grid on screen.</summary>
    public bool IsPageLoading => _loadGate.IsLoading(IsBusy);

    /// <summary>Loads the photos.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }

    /// <summary>Marks the vault stale so the next appearance reloads it after a photo was added elsewhere.</summary>
    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    /// <summary>Reloads the grid and re-applies the privacy defaults, so a reload never inherits a reveal from the previous</summary>
    /// visit.</summary>
    private async Task LoadPhotosCoreAsync()
    {
        ResetPrivacy();
        ResetComparison();
        StatusMessage = string.Empty;
        IsStatusError = false;
        Photos = new ObservableCollection<PhotoVaultItem>();
        TotalPhotosCount = 0;
        var revision = _privacyRevision;
        IsBusy = true;
        try
        {
            var result = await _useCase.LoadAsync();
            if (revision != _privacyRevision) return;
            CurrentPhase = result.CurrentPhase;

            var items = result.Photos.Select(e => new PhotoVaultItem
            {
                Id = e.Id,
                ImagePath = e.ImagePath,
                IsImageAvailable = e.IsImageAvailable,
                Date = e.Date,
                WeightKg = e.WeightKg,
                Notes = e.Notes,
                Phase = e.Phase,
                IsRevealed = false
            }).ToList();

            Photos = new ObservableCollection<PhotoVaultItem>(items);
            TotalPhotosCount = Photos.Count;
            IsEmpty = Photos.Count == 0;
            OnPropertyChanged(nameof(CanComparePhotos));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Reveals or blurs the whole grid at once; a photo with no file stays hidden either way.</summary>
    private void TogglePrivacyBlur()
    {
        IsPrivacyBlurEnabled = !IsPrivacyBlurEnabled;
        foreach (var photo in Photos) photo.IsRevealed = !IsPrivacyBlurEnabled && photo.IsImageAvailable;
    }

    /// <summary>Applies the blur change to every photo.</summary>
    partial void OnIsPrivacyBlurEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(PrivacyBlurButtonText));
    }

    /// <summary>Flips one photo and re-derives whether the grid still counts as fully blurred, so the global control stays</summary>
    /// honest after a single reveal.</summary>
    private void ToggleItemReveal(PhotoVaultItem? item)
    {
        if (item is null || !item.IsImageAvailable || !Photos.Contains(item)) return;
        item.IsRevealed = !item.IsRevealed;
        IsPrivacyBlurEnabled = Photos.All(photo => !photo.IsRevealed);
    }

    /// <summary>Re-blurs every photo, closes the comparison and bumps the privacy revision, which makes any image load</summary>
    /// still in flight for a revealed photo discard its result.</summary>
    public void ResetPrivacy()
    {
        _privacyRevision++;
        IsPrivacyBlurEnabled = true;
        foreach (var photo in Photos) photo.IsRevealed = false;
        ResetComparison();
    }

    /// <summary>Fills one of the two comparison slots and renders the pair; it refuses a photo that is gone, so a deleted</summary>
    /// photo cannot be compared.</summary>
    private async Task SelectComparisonPhotoAsync(PhotoVaultItem? item, bool isFirst)
    {
        if (item is null || !item.IsImageAvailable || !Photos.Contains(item)) return;
        StatusMessage = string.Empty;
        IsStatusError = false;
        _privacyRevision++;
        if (isFirst) _firstComparisonId = item.Id;
        else _secondComparisonId = item.Id;

        if (_firstComparisonId is { } sameFirst && _secondComparisonId is { } sameSecond && sameFirst == sameSecond)
        {
            if (isFirst) _secondComparisonId = null;
            else _firstComparisonId = null;
            IsStatusError = true;
            StatusMessage = UiTextCatalog.Get("Profile_PhotoVault_ComparePickDifferent");
        }

        SyncComparisonSelectionState();
        ResetComparisonImages();
        if (_firstComparisonId is not { } firstId || _secondComparisonId is not { } secondId)
            return;

        var revision = _privacyRevision;
        try
        {
            var comparison = await _useCase.CompareAsync(firstId, secondId);
            if (revision != _privacyRevision) return;
            if (comparison is null)
            {
                IsStatusError = true;
                StatusMessage = UiTextCatalog.Get("Profile_PhotoVault_CompareFailed");
                return;
            }

            ComparisonFirstImagePath = comparison.First.ImagePath;
            ComparisonSecondImagePath = comparison.Second.ImagePath;
            var firstItem = Photos.FirstOrDefault(p => p.Id == firstId);
            var secondItem = Photos.FirstOrDefault(p => p.Id == secondId);
            ComparisonFirstDateText = firstItem?.DateFormatted ?? UiTextCatalog.Get("Profile_PhotoJourney_FirstComparisonLabel");
            ComparisonSecondDateText = secondItem?.DateFormatted ?? UiTextCatalog.Get("Profile_PhotoJourney_SecondComparisonLabel");
            HasPendingComparisonSelection = false;
            IsComparisonOpen = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PhotoJourneyVaultViewModel] Compare failed ({ex.GetType().Name}).");
            IsStatusError = true;
            StatusMessage = UiTextCatalog.Get("Profile_PhotoVault_CompareFailed");
        }
    }

    /// <summary>Marks which rows are currently chosen for comparison, using the same revision the images did.</summary>
    private void SyncComparisonSelectionState()
    {
        foreach (var photo in Photos)
        {
            photo.IsSelectedFirst = _firstComparisonId == photo.Id;
            photo.IsSelectedSecond = _secondComparisonId == photo.Id;
        }

        var selectedId = _firstComparisonId ?? _secondComparisonId;
        var hasOneSelected = (_firstComparisonId.HasValue ^ _secondComparisonId.HasValue) && selectedId.HasValue;
        HasPendingComparisonSelection = hasOneSelected;
        if (hasOneSelected && Photos.FirstOrDefault(p => p.Id == selectedId!.Value) is { } chosen)
        {
            ComparisonSelectionBannerText = UiTextCatalog.Format(
                "Profile_PhotoVault_CompareSelectionFormat", chosen.DateFormatted);
        }
        else
        {
            ComparisonSelectionBannerText = string.Empty;
        }
    }

    /// <summary>Forgets both comparison selections and the rendered pair.</summary>
    private void ResetComparison()
    {
        _privacyRevision++;
        _firstComparisonId = null;
        _secondComparisonId = null;
        SyncComparisonSelectionState();
        ResetComparisonImages();
    }

    /// <summary>Closes the comparison view and drops both rendered images.</summary>
    private void ResetComparisonImages()
    {
        IsComparisonOpen = false;
        ComparisonFirstImagePath = null;
        ComparisonSecondImagePath = null;
    }

    /// <summary>Adds a photo with the phase it was taken in and reloads the grid, reporting whether it was stored so the</summary>
    /// caller can keep the picker's result.</summary>
    public async Task<bool> AddPhotoAsync(string imagePath, DateTime date, double? weightKg, string notes)
    {
        var added = await _useCase.AddAsync(new AddPhotoVaultEntryRequest(
            imagePath, date, weightKg, notes, CurrentPhase));
        if (!added)
            return false;

        Invalidate();
        try
        {
            await LoadDataCommand.ExecuteAsync(null);
            if (Photos.FirstOrDefault() is { IsImageAvailable: true } newest)
            {
                newest.IsRevealed = true;
                IsPrivacyBlurEnabled = false;
            }
            IsLoadError = false;
        }
        catch
        {
            IsLoadError = true;
        }
        return true;
    }

    /// <summary>Deletes one photo and reloads, refusing while another write is running or when the row no longer exists.</summary>
    private async Task DeletePhotoAsync(PhotoVaultItem? item)
    {
        if (item is null || IsBusy) return;

        if (Shell.Current is { } shell)
        {
            var confirmed = await shell.DisplayAlertAsync(
                UiTextCatalog.Get("Profile_PhotoVault_DeleteConfirmTitle"),
                UiTextCatalog.Format("Profile_PhotoVault_DeleteConfirmMessage", item.DateFormatted),
                UiTextCatalog.Get("Common_Delete"),
                UiTextCatalog.Get("Common_Cancel"));
            if (!confirmed)
                return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        IsStatusError = false;
        try
        {
            var deleted = await _useCase.DeleteAsync(item.Id);
            if (deleted)
            {
                ResetComparison();
                Photos.Remove(item);
                TotalPhotosCount = Photos.Count;
                IsEmpty = Photos.Count == 0;
                OnPropertyChanged(nameof(CanComparePhotos));
                StatusMessage = UiTextCatalog.Get("Profile_PhotoVault_DeletedStatus");
            }
            else
            {
                IsStatusError = true;
                StatusMessage = UiTextCatalog.Get("Profile_PhotoVault_DeleteFailed");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[PhotoJourneyVaultViewModel] Delete failed ({ex.GetType().Name}).");
            IsStatusError = true;
            StatusMessage = UiTextCatalog.Get("Profile_PhotoVault_DeleteFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Leaves the vault through the navigation the page was given.</summary>
    private Task NavigateBackAsync()
    {
        return _navigateBackAsync();
    }

    /// <summary>Releases the view model's subscriptions.</summary>
    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
