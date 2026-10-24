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

    public HiitEligibilityEvidenceInputV020 ToRequestInput(
        DateOnly localDate,
        WorkoutActivityDto underlyingCardioActivity) => new(
        TryReadUnderlyingCardioHistory(localDate, underlyingCardioActivity),
        TryReadWeeklyEvidence(),
        SelectedClearanceScreeningChoice?.Value);

    public void Reset()
    {
        UnderlyingCardioConsistentWeeks = string.Empty;
        UnderlyingCardioAverageSessionsPerWeek = string.Empty;
        UnderlyingCardioDaysSinceLastSession = string.Empty;
        SelectedClearanceScreeningChoice = null;
        foreach (var week in PreviousFourWeeksAerobicEvidence)
            week.QualifyingManageableSessionCount = string.Empty;
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
}
