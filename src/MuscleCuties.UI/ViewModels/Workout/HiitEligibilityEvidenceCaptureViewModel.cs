using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

public sealed partial class HiitEligibilityEvidenceCaptureViewModel : ObservableObject
{
    private const int FirstPriorWeekOffset = 1;
    private const int NoElapsedDays = 0;
    private const decimal MinimumAverageSessionsPerWeek = 0m;

    [ObservableProperty] private string _underlyingCardioConsistentWeeks = string.Empty;
    [ObservableProperty] private string _underlyingCardioAverageSessionsPerWeek = string.Empty;
    [ObservableProperty] private string _underlyingCardioDaysSinceLastSession = string.Empty;
    [ObservableProperty] private ActivityEvidenceAnswerChoice? _selectedClearanceScreeningChoice;
    private bool _showErrors;
    private DateOnly _validationDate;

    public HiitEligibilityEvidenceCaptureViewModel()
    {
        ClearanceScreeningChoices =
        [
            new(UiTextCatalog.Get("Workout_Generation_EvidenceYes"), true),
            new(UiTextCatalog.Get("Workout_Generation_EvidenceNo"), false)
        ];
        PreviousFourWeeksAerobicEvidence = new ObservableCollection<HiitAerobicWeekEvidenceCapture>(
            Enumerable.Range(FirstPriorWeekOffset, WorkoutPlanningConstants.HiitRequiredPriorWeeks)
                .Select(weekOffset => new HiitAerobicWeekEvidenceCapture(weekOffset)));
        UnderlyingCardioExperienceHelp = string.Format(
            CultureInfo.CurrentCulture,
            CompositeFormat.Parse(UiTextCatalog.Get("Workout_Generation_HiitCardioHistoryHelp")),
            WorkoutPlanningConstants.MinimumConsistentWeeksForEstablished,
            WorkoutPlanningConstants.EstablishedSessionsPerWeek,
            WorkoutPlanningConstants.ReturningInactivityDays);
        AerobicSessionThresholdHelp = string.Format(
            CultureInfo.CurrentCulture,
            CompositeFormat.Parse(UiTextCatalog.Get("Workout_Generation_HiitAerobicThresholdHelp")),
            WorkoutPlanningConstants.HiitMinimumManageableSessionMinutes,
            WorkoutPlanningConstants.HiitRequiredAerobicSessionsPerWeek);
    }

    public ObservableCollection<ActivityEvidenceAnswerChoice> ClearanceScreeningChoices { get; }

    public ObservableCollection<HiitAerobicWeekEvidenceCapture> PreviousFourWeeksAerobicEvidence { get; }

    public string UnderlyingCardioExperienceHelp { get; }

    public string AerobicSessionThresholdHelp { get; }

    public bool InvalidConsistentWeeks => _showErrors && !ValidNonNegativeInteger(UnderlyingCardioConsistentWeeks);
    public bool InvalidAverageSessions => _showErrors && !ValidAverage(UnderlyingCardioAverageSessionsPerWeek);
    public bool InvalidDaysSinceLastSession => _showErrors &&
        (!int.TryParse(UnderlyingCardioDaysSinceLastSession, NumberStyles.None, CultureInfo.InvariantCulture,
            out var days) || days < NoElapsedDays || days > _validationDate.DayNumber);
    public bool MissingClearance => _showErrors && SelectedClearanceScreeningChoice is null;

    public bool IsComplete(DateOnly localDate, WorkoutActivityDto? activity) =>
        activity is { } selected && TryReadUnderlyingCardioHistory(localDate, selected) is not null &&
        TryReadWeeklyEvidence() is not null && SelectedClearanceScreeningChoice is not null;

    public void ShowValidationErrors(DateOnly localDate)
    {
        _validationDate = localDate;
        _showErrors = true;
        NotifyValidation();
        foreach (var week in PreviousFourWeeksAerobicEvidence)
            week.ShowValidationError();
    }

    private static bool ValidNonNegativeInteger(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= NoElapsedDays;

    private static bool ValidAverage(string value) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) &&
        parsed >= MinimumAverageSessionsPerWeek;

    private void NotifyValidation()
    {
        OnPropertyChanged(nameof(InvalidConsistentWeeks));
        OnPropertyChanged(nameof(InvalidAverageSessions));
        OnPropertyChanged(nameof(InvalidDaysSinceLastSession));
        OnPropertyChanged(nameof(MissingClearance));
    }

    partial void OnUnderlyingCardioConsistentWeeksChanged(string value) => NotifyValidation();
    partial void OnUnderlyingCardioAverageSessionsPerWeekChanged(string value) => NotifyValidation();
    partial void OnUnderlyingCardioDaysSinceLastSessionChanged(string value) => NotifyValidation();
    partial void OnSelectedClearanceScreeningChoiceChanged(ActivityEvidenceAnswerChoice? value) => NotifyValidation();

    public HiitEligibilityEvidenceInputV020 ToRequestInput(
        DateOnly localDate,
        WorkoutActivityDto underlyingCardioActivity) => new(
        TryReadUnderlyingCardioHistory(localDate, underlyingCardioActivity),
        TryReadWeeklyEvidence(),
        SelectedClearanceScreeningChoice?.Value);

    public void Reset()
    {
        _showErrors = false;
        UnderlyingCardioConsistentWeeks = string.Empty;
        UnderlyingCardioAverageSessionsPerWeek = string.Empty;
        UnderlyingCardioDaysSinceLastSession = string.Empty;
        SelectedClearanceScreeningChoice = null;
        foreach (var week in PreviousFourWeeksAerobicEvidence)
            week.Reset();
        NotifyValidation();
    }

    public void Prefill(HiitEligibilityEvidenceInputV020? saved, DateOnly localDate)
    {
        Reset();
        if (saved is null)
            return;

        var history = saved.UnderlyingCardioHistory;
        UnderlyingCardioConsistentWeeks = history?.ConsistentParticipationWeeks?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        UnderlyingCardioAverageSessionsPerWeek = history?.AverageSessionsPerWeekInRecentFourWeeks?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        if (history?.LastCompletedSessionDate is { } lastDate && lastDate <= localDate)
            UnderlyingCardioDaysSinceLastSession = (localDate.DayNumber - lastDate.DayNumber).ToString(CultureInfo.InvariantCulture);
        SelectedClearanceScreeningChoice = ClearanceScreeningChoices
            .FirstOrDefault(choice => choice.Value == saved.ExistingClearanceScreeningSatisfied);
        foreach (var week in PreviousFourWeeksAerobicEvidence)
        {
            var recorded = saved.PreviousFourWeeksAerobicEvidence?
                .FirstOrDefault(value => value.WeekOffsetFromCurrent == week.WeekOffsetFromCurrent);
            week.QualifyingManageableSessionCount = recorded?.QualifyingManageableAerobicSessionCount
                .ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }

    private ActivityTrainingHistoryInputV020? TryReadUnderlyingCardioHistory(
        DateOnly localDate,
        WorkoutActivityDto underlyingCardioActivity)
    {
        if (!WorkoutPlanningConstants.IsSupportedHiitUnderlyingCardioActivity(underlyingCardioActivity) ||
            !int.TryParse(UnderlyingCardioConsistentWeeks, NumberStyles.None, CultureInfo.InvariantCulture,
                out var consistentWeeks) || consistentWeeks < NoElapsedDays ||
            !decimal.TryParse(UnderlyingCardioAverageSessionsPerWeek, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var averageSessions) ||
            averageSessions < MinimumAverageSessionsPerWeek ||
            !int.TryParse(UnderlyingCardioDaysSinceLastSession, NumberStyles.None, CultureInfo.InvariantCulture,
                out var daysSinceLastSession) || daysSinceLastSession < NoElapsedDays ||
            daysSinceLastSession > localDate.DayNumber)
            return null;

        return new ActivityTrainingHistoryInputV020(
            underlyingCardioActivity.ToString(), consistentWeeks, averageSessions,
            localDate.AddDays(-daysSinceLastSession), true);
    }

    private List<HiitAerobicWeekInputV020>? TryReadWeeklyEvidence()
    {
        var weeks = new List<HiitAerobicWeekInputV020>(PreviousFourWeeksAerobicEvidence.Count);
        foreach (var week in PreviousFourWeeksAerobicEvidence)
        {
            if (!int.TryParse(week.QualifyingManageableSessionCount, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var sessionCount) || sessionCount < NoElapsedDays)
                return null;

            weeks.Add(new HiitAerobicWeekInputV020(week.WeekOffsetFromCurrent, sessionCount));
        }

        return weeks;
    }
}

public sealed partial class HiitAerobicWeekEvidenceCapture : ObservableObject
{
    private const int MinimumSessionCount = 0;
    private bool _showError;
    [ObservableProperty] private string _qualifyingManageableSessionCount = string.Empty;

    public HiitAerobicWeekEvidenceCapture(int weekOffsetFromCurrent)
    {
        WeekOffsetFromCurrent = weekOffsetFromCurrent;
        WeekLabel = string.Format(
            CultureInfo.CurrentCulture,
            CompositeFormat.Parse(UiTextCatalog.Get("Workout_Generation_HiitPriorWeekLabel")),
            weekOffsetFromCurrent);
        SessionCountAccessibilityDescription = string.Format(
            CultureInfo.CurrentCulture,
            CompositeFormat.Parse(UiTextCatalog.Get("Workout_Generation_HiitWeekCountAccessibility")),
            WeekLabel);
    }

    public int WeekOffsetFromCurrent { get; }

    public string WeekLabel { get; }

    public string SessionCountAccessibilityDescription { get; }

    public bool InvalidSessionCount => _showError &&
        (!int.TryParse(QualifyingManageableSessionCount, NumberStyles.None, CultureInfo.InvariantCulture,
            out var count) || count < MinimumSessionCount);

    public void ShowValidationError()
    {
        _showError = true;
        OnPropertyChanged(nameof(InvalidSessionCount));
    }

    public void Reset()
    {
        _showError = false;
        QualifyingManageableSessionCount = string.Empty;
        OnPropertyChanged(nameof(InvalidSessionCount));
    }

    partial void OnQualifyingManageableSessionCountChanged(string value) =>
        OnPropertyChanged(nameof(InvalidSessionCount));
}
