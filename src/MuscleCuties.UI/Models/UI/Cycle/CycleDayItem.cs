using Microsoft.Maui.Graphics;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Models.UI.Cycle;

public class CycleDayItem
{
    public int Day { get; set; }
    public int CycleDay { get; set; }
    public DateTime? Date { get; set; }
    public CyclePhase Phase { get; set; }
    public string IconSource { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool IsNeutral { get; set; }
    public bool HasPhaseShiftLog { get; set; }
    public bool HasSymptomLog { get; set; }
    public bool HasRecordedObservation => HasPhaseShiftLog || HasSymptomLog;
    public bool IsEstimated { get; set; }
    public bool IsPredictedFuture { get; set; }
    public bool IsPredicted => IsPredictedFuture;
    public Color BackgroundColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.Black;
    public Color StrokeColor { get; set; } = Colors.Transparent;
    public double StrokeThickness { get; set; }
    public string DayOfWeekShort { get; set; } = string.Empty;
    public Color PhaseMarkerColor { get; set; } = Colors.Transparent;
    public bool IsSelected { get; set; }

    public string DisplayDayLabel =>
        IsEstimated || IsPredictedFuture ? $"{Day}*" : Day.ToString(CultureInfo.InvariantCulture);

    public string AccessibilityDescription
    {
        get
        {
            if (LocalizationService.ActiveLanguage == "ru")
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
