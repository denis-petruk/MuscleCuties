using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Services.Dashboard;
using MuscleCuties.UseCases.Features.Cycle.Overview;

namespace MuscleCuties.UI.ViewModels.Cycle;

/// <summary>
///     Turns the loaded cycle days into the calendar strip's items.
/// </summary>
public static class CycleCalendarItemBuilder
{
    private const int DaysInWeek = 7;
    private const int MondayDayOfWeekIndex = 1;
    private const int SundayOffsetDays = -6;
    private const int MaxStaleCyclesMultiplier = 3;

    /// <summary>The strip's day items around the anchor date.</summary>
    public static ObservableCollection<CycleDayItem> BuildCalendarStrip(
        IReadOnlyList<CycleDayItem> allMonthDays,
        DateTime anchorDate,
        DateTime today,
        DateTime selectedDate,
        bool useDarkTheme,
        Func<DateTime, CycleDayItem?>? fallbackDayFactory = null)
    {
        var dayOfWeek = (int)anchorDate.DayOfWeek;
        var diffToMonday = dayOfWeek == 0 ? SundayOffsetDays : MondayDayOfWeekIndex - dayOfWeek;
        var monday = anchorDate.Date.AddDays(diffToMonday);

        var strip = new ObservableCollection<CycleDayItem>();
        for (var i = 0; i < DaysInWeek; i++)
        {
            var date = monday.AddDays(i);
            var existingDay = allMonthDays.FirstOrDefault(d => d.Date?.Date == date.Date);
            if (existingDay is null && fallbackDayFactory is not null) existingDay = fallbackDayFactory(date);

            strip.Add(BuildStripDayItem(date, existingDay, today, selectedDate, useDarkTheme));
        }

        return strip;
    }

    /// <summary>One strip day, reusing the existing item when there is one.</summary>
    public static CycleDayItem BuildStripDayItem(
        DateTime date,
        CycleDayItem? existingDay,
        DateTime today,
        DateTime selectedDate,
        bool useDarkTheme)
    {
        var phase = existingDay?.Phase ?? CyclePhase.Unknown;
        var isNeutral = existingDay?.IsNeutral ?? true;
        var isToday = date.Date == today.Date;
        var isSelected = date.Date == selectedDate.Date;

        var markerColor = isNeutral
            ? PhaseThemePalette.GetNeutralDividerColor(useDarkTheme)
            : PhaseThemePalette.GetPhaseTextColor(phase, useDarkTheme, default);

        return new CycleDayItem
        {
            Day = date.Day,
            CycleDay = existingDay?.CycleDay ?? 0,
            Date = date,
            Phase = phase,
            IconSource = CyclePhaseAssets.GetIconSource(phase),
            IsToday = isToday,
            IsNeutral = isNeutral,
            IsSelected = isSelected,
            HasPhaseShiftLog = existingDay?.HasPhaseShiftLog ?? false,
            HasSymptomLog = existingDay?.HasSymptomLog ?? false,
            IsEstimated = existingDay?.IsEstimated ?? false,
            IsPredictedFuture = existingDay?.IsPredictedFuture ?? false,
            DayOfWeekShort = DisplayFormat.DayOfWeekInitial(date),
            PhaseMarkerColor = markerColor,
            BackgroundColor = isSelected
                ? PhaseThemePalette.GetSelectedDayBackgroundColor(useDarkTheme)
                : existingDay?.BackgroundColor ?? AppThemeResources.GetRequired<Color>("Transparent"),
            TextColor = isSelected
                ? GetSelectedDayTextColor(useDarkTheme)
                : isToday
                    ? GetTodayDayTextColor(isNeutral, useDarkTheme)
                    : existingDay?.TextColor ?? GetPhaseTextColor(phase, useDarkTheme),
            StrokeColor = isSelected
                ? PhaseThemePalette.GetSelectedDayStrokeColor(useDarkTheme)
                : (existingDay?.StrokeColor != default && existingDay?.StrokeColor is not null
                    ? existingDay.StrokeColor
                    : AppThemeResources.GetColor(
                        useDarkTheme ? "CycleStripDefaultStrokeDark" : "CycleStripDefaultStrokeLight",
                        default)),
            StrokeThickness = AppThemeResources.GetDouble(isSelected ? "UiStrokeWidth2" : "UiStrokeWidth1")
        };
    }

    /// <summary>One calendar day item for the date.</summary>
    public static CycleDayItem CreateCalendarDayItem(
        DateTime date,
        CyclePredictionSummary prediction,
        IReadOnlyCollection<CyclePhaseEntry> orderedPhaseLogs,
        IReadOnlyCollection<CycleHistoryItem> orderedCycles,
        IReadOnlyCollection<CycleSymptomEntry>? symptomLogs,
        DateTime today,
        DateTime selectedDate,
        bool useDarkTheme,
        ICyclePhaseProjectionUseCase cycleProjection)
    {
        var targetDate = date.Date;

        var cycle = orderedCycles.LastOrDefault(item => item.StartDate.Date <= targetDate &&
                                                        (item.EndDate is null || targetDate < item.EndDate.Value.Date));
        var cycleStartDate = cycle?.StartDate.Date;
        var rawCycleLength = cycle?.CycleLength is > 0 ? cycle.CycleLength : prediction.PredictedCycleLength;
        var dayProjection = cycleProjection.Project(new CyclePhaseProjectionInputDto(
            targetDate,
            today,
            rawCycleLength,
            cycleStartDate,
            CurrentPhase: prediction.CurrentPhase,
            HasActiveCycle: prediction.HasActiveCycle));
        var dayCycleLength = dayProjection.NormalizedCycleLength;
        var cycleDay = dayProjection.CycleDay;
        var isToday = targetDate == today;

        var latestShift = orderedPhaseLogs.LastOrDefault(log => log.LoggedAt.Date <= targetDate &&
                                                                (cycleStartDate is null ||
                                                                 log.LoggedAt.Date >= cycleStartDate.Value));
        var hasPhaseShiftLog = latestShift?.LoggedAt.Date == targetDate;
        var hasSymptomLog = symptomLogs?.Any(s => s.Date.Date == targetDate) ?? false;
        var hasFlowLog = orderedPhaseLogs.Any(log => log.LoggedAt.Date == targetDate && log.FlowIntensity.HasValue);
        var hasRecordedObservation = hasPhaseShiftLog || hasSymptomLog || hasFlowLog;

        var hasRecordAnchor = cycleStartDate is not null || latestShift is not null;
        var lastAnchor = latestShift is not null &&
                         (cycleStartDate is null || latestShift.LoggedAt.Date >= cycleStartDate)
            ? latestShift.LoggedAt.Date
            : cycleStartDate;
        var isStale = lastAnchor is not null && (targetDate - lastAnchor.Value).Days > dayCycleLength * MaxStaleCyclesMultiplier;
        var isNeutral = !hasRecordAnchor || isStale;
        var isEstimated = !isNeutral && !hasPhaseShiftLog;
        var isPredictedFuture = !isNeutral && targetDate > today;
        var phaseProjection = cycleProjection.Project(new CyclePhaseProjectionInputDto(
            targetDate,
            today,
            dayCycleLength,
            cycleStartDate,
            cycleDay,
            CurrentPhase: prediction.CurrentPhase,
            HasActiveCycle: prediction.HasActiveCycle,
            LoggedPhase: latestShift?.Phase,
            LoggedAt: latestShift?.LoggedAt));
        var phase = isNeutral
            ? CyclePhase.Unknown
            : phaseProjection.Phase;

        return new CycleDayItem
        {
            Day = date.Day,
            CycleDay = cycleDay,
            Date = date,
            Phase = phase,
            IconSource = CyclePhaseAssets.GetIconSource(phase),
            IsToday = isToday,
            IsSelected = targetDate == selectedDate.Date,
            IsNeutral = isNeutral,
            HasPhaseShiftLog = hasPhaseShiftLog,
            HasSymptomLog = hasSymptomLog,
            IsEstimated = isEstimated,
            IsPredictedFuture = isPredictedFuture,
            BackgroundColor = isNeutral
                ? GetNeutralDayBackground(isToday, useDarkTheme)
                : GetDayBackground(phase, isToday, useDarkTheme),
            TextColor = isToday ? GetTodayDayTextColor(isNeutral, useDarkTheme) :
                isNeutral ? GetNeutralDayTextColor(useDarkTheme) : GetPhaseTextColor(phase, useDarkTheme),
            StrokeColor = ResolveDayStrokeColor(phase, isToday, hasRecordedObservation, isEstimated, useDarkTheme),
            StrokeThickness = AppThemeResources.GetDouble(isToday
                ? "UiStrokeWidth2"
                : hasRecordedObservation
                    ? "UiStrokeWidth1_25"
                    : isEstimated
                        ? "UiStrokeWidth1_15"
                        : "UiStrokeWidth0"),
            DayOfWeekShort = DisplayFormat.DayOfWeekInitial(date),
            PhaseMarkerColor = isNeutral
                ? PhaseThemePalette.GetNeutralDividerColor(useDarkTheme)
                : PhaseThemePalette.GetPhaseTextColor(phase, useDarkTheme, default)
        };
    }

    /// <summary>The stroke colour for a calendar day.</summary>
    public static Color ResolveDayStrokeColor(
        CyclePhase phase,
        bool isToday,
        bool hasRecordedObservation,
        bool isEstimated,
        bool useDarkTheme)
    {
        if (isToday || hasRecordedObservation)
            return GetPhaseTextColor(phase, useDarkTheme);

        return isEstimated
            ? GetPredictedDayStrokeColor(phase, useDarkTheme)
            : AppThemeResources.GetRequired<Color>("Transparent");
    }

    /// <summary>A day cell's fill; today's is drawn from the phase's own ink so it stands out whatever phase it falls</summary>
    /// in.</summary>
    public static Color GetDayBackground(CyclePhase phase, bool isToday, bool useDarkTheme)
    {
        if (isToday) return GetPhaseTextColor(phase, useDarkTheme);

        return GetPhaseBackgroundColor(phase, useDarkTheme);
    }

    /// <summary>The fill that stands for a phase, taken from the shared palette so the grid and the legend cannot</summary>
    /// disagree.</summary>
    public static Color GetPhaseBackgroundColor(CyclePhase phase, bool useDarkTheme)
    {
        return PhaseThemePalette.GetPhaseBackgroundColor(phase, useDarkTheme, default);
    }

    /// <summary>The fill for a day with no logged phase, with today given its own surface so it is still findable.</summary>
    public static Color GetNeutralDayBackground(bool isToday, bool useDarkTheme)
    {
        return isToday
            ? AppThemeResources.GetColor("CycleNeutralTodaySurface", default)
            : AppThemeResources.GetColor(useDarkTheme ? "SurfaceSunkenDark" : "CycleNeutralSurfaceLight", default);
    }

    /// <summary>The number colour on a neutral fill, chosen to stay readable against it in either theme.</summary>
    public static Color GetNeutralDayTextColor(bool useDarkTheme)
    {
        return AppThemeResources.GetColor(useDarkTheme ? "TextPrimaryDark" : "CycleNeutralTextLight", default);
    }

    /// <summary>The number colour on the selected cell.</summary>
    public static Color GetSelectedDayTextColor(bool useDarkTheme)
    {
        return AppThemeResources.GetColor(useDarkTheme ? "TextPrimaryDark" : "TextPrimary", default);
    }

    /// <summary>The text colour used on today's cell.</summary>
    public static Color GetTodayDayTextColor(bool useDarkTheme) =>
        GetTodayDayTextColor(isNeutral: false, useDarkTheme);

    /// <summary>Today's number colour: a neutral today sits on a dark surface while a phased today in dark mode sits on a</summary>
    /// bright pastel, so the ink is chosen from the background rather than the theme alone.</summary>
    public static Color GetTodayDayTextColor(bool isNeutral, bool useDarkTheme)
    {
        // Neutral Today sits on CycleNeutralTodaySurface (#1F2937) in both themes; phased Today in Dark
        // mode sits on bright pastel CyclePhase*TextDark so contrast requires dark ink.
        return useDarkTheme && !isNeutral
            ? AppThemeResources.GetColor("PageBackgroundDark", default)
            : AppThemeResources.GetColor("ContrastInk", default);
    }

    /// <summary>The outline that marks a day the prediction produced, never the fill, so a guess cannot look like a log.</summary>
    public static Color GetPredictedDayStrokeColor(CyclePhase phase, bool useDarkTheme)
    {
        return PhaseThemePalette.GetPredictedDayStrokeColor(phase, useDarkTheme);
    }

    /// <summary>The ink on a phase-filled cell, falling back to the theme's primary text when the phase has no colour of</summary>
    /// its own.</summary>
    public static Color GetPhaseTextColor(CyclePhase phase, bool useDarkTheme)
    {
        return PhaseThemePalette.GetPhaseTextColor(phase, useDarkTheme,
            AppThemeResources.GetColor(useDarkTheme ? "TextPrimaryDark" : "CycleFallbackTextLight", default));
    }
}
