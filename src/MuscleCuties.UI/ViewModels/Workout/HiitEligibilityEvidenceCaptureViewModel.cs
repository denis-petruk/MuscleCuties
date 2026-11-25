using System.Text;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

/// <summary>
///     Captures the evidence a HIIT session needs before one can be offered.
/// </summary>
public sealed partial class HiitEligibilityEvidenceCaptureViewModel : ObservableObject
{
    [ObservableProperty] private bool _confirmCurrentWeeks;
    private const int FirstPriorWeekOffset = 1;
    private const int NoElapsedDays = 0;
    private const decimal MinimumAverageSessionsPerWeek = 0m;

    [ObservableProperty] private string _underlyingCardioConsistentWeeks = string.Empty;
    [ObservableProperty] private string _underlyingCardioAverageSessionsPerWeek = string.Empty;
    [ObservableProperty] private string _underlyingCardioDaysSinceLastSession = string.Empty;
    [ObservableProperty] private ActivityEvidenceAnswerChoice? _selectedClearanceScreeningChoice;
    private bool _showErrors;
    private DateOnly _validationDate;

    /// <summary>Prepares the yes/no clearance choices and the four prior-week rows, so the form is complete before it</summary>
    /// is shown.</summary>
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

    /// <summary>The clearance answers to choose from.</summary>
    public ObservableCollection<ActivityEvidenceAnswerChoice> ClearanceScreeningChoices { get; }

    /// <summary>The evidence rows for the previous four weeks.</summary>
    public ObservableCollection<HiitAerobicWeekEvidenceCapture> PreviousFourWeeksAerobicEvidence { get; }

    /// <summary>The help text for the cardio experience field.</summary>
    public string UnderlyingCardioExperienceHelp { get; }

    /// <summary>The help text for the aerobic session threshold.</summary>
    public string AerobicSessionThresholdHelp { get; }

    /// <summary>Whether the weeks field is in error; errors stay hidden until the form has been submitted once, so a</summary>
    /// fresh form is never covered in red.</summary>
    public bool InvalidConsistentWeeks => _showErrors && !ValidNonNegativeInteger(UnderlyingCardioConsistentWeeks);
    /// <summary>Same deferral for the average-sessions field.</summary>
    public bool InvalidAverageSessions => _showErrors && !ValidAverage(UnderlyingCardioAverageSessionsPerWeek);
    /// <summary>The elapsed-days field is in error once shown and the value is not a whole number of days between zero and</summary>
    /// today.</summary>
    public bool InvalidDaysSinceLastSession => _showErrors &&
        (!int.TryParse(UnderlyingCardioDaysSinceLastSession, NumberStyles.None, CultureInfo.InvariantCulture,
            out var days) || days < NoElapsedDays || days > _validationDate.DayNumber);
    /// <summary>True when the required clearance answer is missing.</summary>
    public bool MissingClearance => _showErrors && SelectedClearanceScreeningChoice is null;

    /// <summary>True when the evidence is complete for the activity.</summary>
    public bool IsComplete(DateOnly localDate, WorkoutActivityDto? activity) =>
        activity is { } selected && TryReadUnderlyingCardioHistory(localDate, selected) is not null &&
        TryReadWeeklyEvidence() is not null && SelectedClearanceScreeningChoice is not null;

    /// <summary>Switches the form into its error state, validating against the given day so a future date cannot be</summary>
    /// entered, and flags every empty prior-week row.</summary>
    public void ShowValidationErrors(DateOnly localDate)
    {
        _validationDate = localDate;
        _showErrors = true;
        NotifyValidation();
        foreach (var week in PreviousFourWeeksAerobicEvidence)
            week.ShowValidationError();
    }

    /// <summary>True when the text is a whole number of zero or more.</summary>
    private static bool ValidNonNegativeInteger(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= NoElapsedDays;

    /// <summary>True when the text is an average of zero or more.</summary>
    private static bool ValidAverage(string value) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) &&
        parsed >= MinimumAverageSessionsPerWeek;

    /// <summary>Re-evaluates the four visible errors after a change, so they appear and clear as the user types.</summary>
    private void NotifyValidation()
    {
        OnPropertyChanged(nameof(InvalidConsistentWeeks));
        OnPropertyChanged(nameof(InvalidAverageSessions));
        OnPropertyChanged(nameof(InvalidDaysSinceLastSession));
        OnPropertyChanged(nameof(MissingClearance));
    }

    /// <summary>Refreshes the validation when the field changes.</summary>
    partial void OnUnderlyingCardioConsistentWeeksChanged(string value) => NotifyValidation();
    /// <summary>Refreshes the validation when the average changes.</summary>
    partial void OnUnderlyingCardioAverageSessionsPerWeekChanged(string value) => NotifyValidation();
    /// <summary>Refreshes the validation when the days since the last session change.</summary>
    partial void OnUnderlyingCardioDaysSinceLastSessionChanged(string value) => NotifyValidation();
    /// <summary>Refreshes the validation when the clearance answer changes.</summary>
    partial void OnSelectedClearanceScreeningChoiceChanged(ActivityEvidenceAnswerChoice? value) => NotifyValidation();

    /// <summary>The evidence as the request input.</summary>
    public HiitEligibilityEvidenceInputV020 ToRequestInput(
        DateOnly localDate,
        WorkoutActivityDto underlyingCardioActivity) => new(
        TryReadUnderlyingCardioHistory(localDate, underlyingCardioActivity),
        TryReadWeeklyEvidence(),
        SelectedClearanceScreeningChoice?.Value);

    /// <summary>Clears the row and hides its error, used when the form is reopened.</summary>
    public void Reset()
    {
        ConfirmCurrentWeeks = false;
        _showErrors = false;
        UnderlyingCardioConsistentWeeks = string.Empty;
        UnderlyingCardioAverageSessionsPerWeek = string.Empty;
        UnderlyingCardioDaysSinceLastSession = string.Empty;
        SelectedClearanceScreeningChoice = null;
        foreach (var week in PreviousFourWeeksAerobicEvidence)
            week.Reset();
        NotifyValidation();
    }

    /// <summary>Restores previously saved evidence, leaving an untouched form when there is none to restore.</summary>
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

    /// <summary>The cardio history the entered answers describe, when they are valid.</summary>
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

    /// <summary>The four weeks as contract rows, or null when any row is not a whole session count, which is what makes</summary>
    /// the form incomplete rather than partially sent.</summary>
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

/// <summary>One prior week's qualifying session count, labelled with which week it is.</summary>
public sealed partial class HiitAerobicWeekEvidenceCapture : ObservableObject
{
    private const int MinimumSessionCount = 0;
    private bool _showError;
    [ObservableProperty] private string _qualifyingManageableSessionCount = string.Empty;

    /// <summary>Labels the row from its distance back from the current week, in the reader's language.</summary>
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

    /// <summary>How many weeks back the row is.</summary>
    public int WeekOffsetFromCurrent { get; }

    /// <summary>The row's week label.</summary>
    public string WeekLabel { get; }

    /// <summary>The session count read out to the screen reader.</summary>
    public string SessionCountAccessibilityDescription { get; }

    /// <summary>Whether the count is in error, only after this row has been asked to show its error.</summary>
    public bool InvalidSessionCount => _showError &&
        (!int.TryParse(QualifyingManageableSessionCount, NumberStyles.None, CultureInfo.InvariantCulture,
            out var count) || count < MinimumSessionCount);

    /// <summary>Marks the row as invalid and reveals the message.</summary>
    public void ShowValidationError()
    {
        _showError = true;
        OnPropertyChanged(nameof(InvalidSessionCount));
    }

    /// <summary>Clears the whole form, including the four week rows and the clearance answer, and re-hides the errors.</summary>
    public void Reset()
    {
        _showError = false;
        QualifyingManageableSessionCount = string.Empty;
        OnPropertyChanged(nameof(InvalidSessionCount));
    }

    /// <summary>Refreshes the validation when the session count changes.</summary>
    partial void OnQualifyingManageableSessionCountChanged(string value) =>
        OnPropertyChanged(nameof(InvalidSessionCount));
}
