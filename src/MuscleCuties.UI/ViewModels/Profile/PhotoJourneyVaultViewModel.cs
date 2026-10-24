using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Profile;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class PhotoJourneyVaultViewModel : ObservableObject, IPageLoadAware, IDisposable
{
    private readonly AppPageLoadGate _loadGate = new(AppPageLoadGate.PageFreshnessWindow);
    private readonly Func<Task> _navigateBackAsync;
    private readonly IPhotoVaultUseCase _useCase;
    private int? _firstComparisonId;
    private int? _secondComparisonId;
    private int _privacyRevision;
    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Unknown;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsPageLoading))]
    private bool _isBusy;

    [ObservableProperty] private bool _isEmpty;

    [ObservableProperty] private bool _isLoadError;
    [ObservableProperty] private bool _isPrivacyBlurEnabled = true;
    [ObservableProperty] private ObservableCollection<PhotoVaultItem> _photos = new();
    [ObservableProperty] private int _totalPhotosCount;
    [ObservableProperty] private bool _isComparisonOpen;
    [ObservableProperty] private string? _comparisonFirstImagePath;
    [ObservableProperty] private string? _comparisonSecondImagePath;

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

    public string PrivacyBlurButtonText => IsPrivacyBlurEnabled ? "Reveal All" : "Blur All";
    public AsyncRelayCommand LoadPhotosCommand => LoadDataCommand;
    public RelayCommand TogglePrivacyBlurCommand { get; }
    public RelayCommand<PhotoVaultItem> ToggleItemRevealCommand { get; }
    public AsyncRelayCommand<PhotoVaultItem> DeletePhotoCommand { get; }
    public AsyncRelayCommand<PhotoVaultItem> SelectFirstComparisonPhotoCommand { get; }
    public AsyncRelayCommand<PhotoVaultItem> SelectSecondComparisonPhotoCommand { get; }
    public RelayCommand CloseComparisonCommand { get; }
    public AsyncRelayCommand NavigateBackCommand { get; }

    public bool IsPageLoading => IsBusy && !_loadGate.HasLoaded;

    public AsyncRelayCommand LoadDataCommand { get; }

    public void Invalidate()
    {
        _loadGate.MarkStale();
    }

    private async Task LoadPhotosCoreAsync()
    {
        ResetPrivacy();
        ResetComparison();
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
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void TogglePrivacyBlur()
    {
        IsPrivacyBlurEnabled = !IsPrivacyBlurEnabled;
        foreach (var photo in Photos) photo.IsRevealed = !IsPrivacyBlurEnabled && photo.IsImageAvailable;
    }

    partial void OnIsPrivacyBlurEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(PrivacyBlurButtonText));
    }

    private void ToggleItemReveal(PhotoVaultItem? item)
    {
        if (item is null || !item.IsImageAvailable || !Photos.Contains(item)) return;
        item.IsRevealed = !item.IsRevealed;
    }

    public void ResetPrivacy()
    {
        _privacyRevision++;
        IsPrivacyBlurEnabled = true;
        foreach (var photo in Photos) photo.IsRevealed = false;
        ResetComparison();
    }

    private async Task SelectComparisonPhotoAsync(PhotoVaultItem? item, bool isFirst)
    {
        if (item is null || !item.IsImageAvailable || !Photos.Contains(item)) return;
        _privacyRevision++;
        if (isFirst) _firstComparisonId = item.Id;
        else _secondComparisonId = item.Id;
        ResetComparisonImages();
        if (_firstComparisonId is not { } firstId || _secondComparisonId is not { } secondId)
            return;

        var revision = _privacyRevision;
        var comparison = await _useCase.CompareAsync(firstId, secondId);
        if (revision != _privacyRevision || comparison is null) return;
        ComparisonFirstImagePath = comparison.First.ImagePath;
        ComparisonSecondImagePath = comparison.Second.ImagePath;
        IsComparisonOpen = true;
    }

    private void ResetComparison()
    {
        _privacyRevision++;
        _firstComparisonId = null;
        _secondComparisonId = null;
        ResetComparisonImages();
    }

    private void ResetComparisonImages()
    {
        IsComparisonOpen = false;
        ComparisonFirstImagePath = null;
        ComparisonSecondImagePath = null;
    }

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
            Photos.FirstOrDefault()?.IsRevealed = true;
            IsLoadError = false;
        }
        catch
        {
            IsLoadError = true;
        }
        return true;
    }

    private async Task DeletePhotoAsync(PhotoVaultItem? item)
    {
        if (item is null) return;

        var deleted = await _useCase.DeleteAsync(item.Id);
        if (deleted)
        {
            ResetComparison();
            Photos.Remove(item);
            TotalPhotosCount = Photos.Count;
            IsEmpty = Photos.Count == 0;
        }
    }

    private Task NavigateBackAsync()
    {
        return _navigateBackAsync();
    }

    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
