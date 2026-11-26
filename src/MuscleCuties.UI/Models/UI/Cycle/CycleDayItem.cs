using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;


namespace MuscleCuties.UI.Models.UI.Cycle;

/// <summary>One cell of the cycle month grid: the day's phase, where that phase came from, whether</summary>
/// anything was logged, and the colours the cell draws itself with.</summary>
public class CycleDayItem
{
    /// <summary>Day of the month as printed in the cell.</summary>
    public int Day { get; set; }
    /// <summary>Day number inside the cycle, counted from the cycle start; not the day of the month.</summary>
    public int CycleDay { get; set; }
    /// <summary>Calendar date of the cell, or null for leading and trailing padding cells.</summary>
    public DateTime? Date { get; set; }
    /// <summary>Phase the day belongs to, whether recorded, estimated or predicted.</summary>
    public CyclePhase Phase { get; set; }
    /// <summary>Phase illustration drawn in the cell, when the cell is large enough to show one.</summary>
    public string IconSource { get; set; } = string.Empty;
    /// <summary>Marks the current local date so the grid can ring it.</summary>
    public bool IsToday { get; set; }
    /// <summary>True when the account has no cycle history at all, so the cell is drawn uncoloured</summary>
    /// rather than claiming an estimated phase.</summary>
    public bool IsNeutral { get; set; }
    /// <summary>A phase or flow entry was recorded on this day.</summary>
    public bool HasPhaseShiftLog { get; set; }
    /// <summary>A symptom was logged on this day.</summary>
    public bool HasSymptomLog { get; set; }
    /// <summary>True when the day has a logged phase or symptom.</summary>
    public bool HasRecordedObservation => HasPhaseShiftLog || HasSymptomLog;
    /// <summary>The phase was inferred from history instead of recorded.</summary>
    public bool IsEstimated { get; set; }
    /// <summary>The day is after today, so its phase is a forecast.</summary>
    public bool IsPredictedFuture { get; set; }
    /// <summary>Cell fill for the day's phase, or transparent for a neutral cell.</summary>
    public Color BackgroundColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>Colour of the day number, chosen to stay readable on the cell fill.</summary>
    public Color TextColor { get; set; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    /// <summary>Border colour of the cell; transparent when the cell needs no outline.</summary>
    public Color StrokeColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>Border width in device-independent units; zero hides the border.</summary>
    public double StrokeThickness { get; set; }
    /// <summary>First letter of the weekday, localized, printed above the number.</summary>
    public string DayOfWeekShort { get; set; } = string.Empty;
    /// <summary>Colour of the dot under the number that repeats the phase at a glance.</summary>
    public Color PhaseMarkerColor { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>Draws the selection ring, which the grid applies after the cells are populated.</summary>
    public bool IsSelected { get; set; }

    /// <summary>Day number, marked with an asterisk when the phase shown is only estimated or forecast.</summary>
    public string DisplayDayLabel =>
        !HasRecordedObservation && (IsEstimated || IsPredictedFuture)
            ? $"{Day}*"
            : Day.ToString(CultureInfo.InvariantCulture);

    /// <summary>Spoken cell label: the date, where the phase came from (recorded, predicted or</summary>
    /// estimated), whether it is today, and the edit gesture.</summary>
    public string AccessibilityDescription
    {
        get
        {
            if (LocalizationService.ActiveLanguage == LocalizationService.RussianLanguageCode)
            {
                var russianDate = Date?.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("ru-RU")) ?? $"День {Day}";
                var russianPhase = Phase switch
                {
                    CyclePhase.Menstrual => "менструальная",
                    CyclePhase.Follicular => "фолликулярная",
                    CyclePhase.Ovulatory => "овуляторная",
                    CyclePhase.Luteal => "лютеиновая",
                    _ => "неизвестная"
                };
                var russianSource = IsNeutral
                    ? "нет записей о цикле"
                    : $"{russianPhase} фаза: " +
                      (HasRecordedObservation ? "записана" : IsPredictedFuture ? "прогноз" : "оценка");
                return
                    $"{russianDate}, {russianSource}{(IsToday ? ", сегодня" : string.Empty)}. Дважды нажмите, чтобы изменить.";
            }

            var date = Date?.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture) ?? $"Day {Day}";
            string source;
            if (IsNeutral)
                source = "no recorded cycle history";
            else if (HasRecordedObservation)
                source = $"{Phase} phase recorded";
            else if (IsPredictedFuture)
                source = $"{Phase} phase predicted";
            else
                source = $"{Phase} phase estimated";

            return $"{date}, {source}{(IsToday ? ", today" : string.Empty)}. Double tap to edit.";
        }
    }
}
