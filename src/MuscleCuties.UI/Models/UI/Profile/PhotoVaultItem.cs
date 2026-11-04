using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;

namespace MuscleCuties.UI.Models.UI.Profile;

public partial class PhotoVaultItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RevealedImagePath))]
    [NotifyPropertyChangedFor(nameof(IsImageShown))]
    [NotifyPropertyChangedFor(nameof(IsImageHidden))]
    private bool _isRevealed;
    public int Id { get; init; }
    public string ImagePath { get; init; } = string.Empty;
    public bool IsImageAvailable { get; init; }
    public string? RevealedImagePath => IsRevealed && IsImageAvailable ? ImagePath : null;
    public bool IsImageShown => IsRevealed && IsImageAvailable;
    public bool IsImageHidden => !IsRevealed || !IsImageAvailable;
    public DateTime Date { get; init; }
    public double? WeightKg { get; init; }
    public string Notes { get; init; } = string.Empty;
    public CyclePhase Phase { get; init; }

    public string DateFormatted => Date.ToString("d", CultureInfo.CurrentCulture);
    public string WeightFormatted => WeightKg.HasValue ? $"{WeightKg.Value:F1} kg" : string.Empty;
    public bool HasWeight => WeightKg.HasValue;
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
    public string PhaseName => Phase.ToString();
}
