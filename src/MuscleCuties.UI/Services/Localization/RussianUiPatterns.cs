using System.Text.RegularExpressions;

namespace MuscleCuties.UI.Services.Localization;

internal static class RussianUiPatterns
{
    private static readonly Pattern[] Patterns =
    [
        New("Resend in {0}s", "Повторная отправка через {0} с"),
        New("Today, {0}", "Сегодня, {0}"),
        New("Yesterday, {0}", "Вчера, {0}"),
        New("Tomorrow, {0}", "Завтра, {0}"),
        New("{0} kcal remaining", "Осталось {0} ккал"),
        New("{0} kcal over", "Превышение на {0} ккал"),
        New("{0} kcal remaining today", "Сегодня осталось {0} ккал"),
        New("{0} / {1} kcal consumed", "Потреблено {0} из {1} ккал"),
        New("{0} kcal remaining · {1} kcal daily target", "Осталось {0} ккал · дневная цель {1} ккал"),
        New("{0} kcal target", "Цель: {0} ккал"),
        New("Breakfast ({0} kcal)", "Завтрак ({0} ккал)"),
        New("Lunch ({0} kcal)", "Обед ({0} ккал)"),
        New("Dinner ({0} kcal)", "Ужин ({0} ккал)"),
        New("Snack ({0} kcal)", "Перекус ({0} ккал)"),
        New("Remaining: P {0}g  C {1}g  F {2}g", "Осталось: Б {0} г  У {1} г  Ж {2} г"),
        New("This meal is {0}% of your daily target", "Этот приём пищи составляет {0}% дневной цели"),
        New("{0} of {1} daily targets reached", "Достигнуто {0} из {1} дневных целей"),
        New("{0}g fiber", "Клетчатка: {0} г"),
        New("{0} breakdown", "Состав: {0}"),
        New("{0} logged at {1}.", "{0} записано в {1}."),
        New("{0} updated at {1}.", "{0} обновлено в {1}."),
        New("{0} saved. Add it to this meal when ready.",
            "{0} сохранено. Добавьте в этот приём пищи, когда будете готовы."),
        New("Logged {0}.", "Записано: {0}."),
        New("{0} of 7 days checked in", "Отметки за {0} из 7 дней"),
        New("7-day median {0} kg", "Медиана за 7 дней: {0} кг"),
        New("{0} day session streak", "Серия тренировок: {0} дн."),
        New("{0} day log streak", "Серия записей: {0} дн."),
        New("{0} cycles", "Записано циклов: {0}"),
        New("{0} photos", "Фото: {0}"),
        New("{0} Active", "Активно: {0}"),
        New("Member since {0}", "В приложении с {0}"),
        New("{0} available days / week", "Доступно дней в неделю: {0}"),
        New("This week · {0}", "Эта неделя · {0}"),
        New("{0} · Day {1} / {2}", "{0} · день {1} из {2}"),
        New("{0} · Start tracking", "{0} · начните отслеживание"),
        New("Phase focus · {0}", "Акцент фазы · {0}"),
        New("Today · {0}", "Сегодня · {0}"),
        New("Today {0}", "Сегодня · {0}"),
        New("Good morning, {0}", "Доброе утро, {0}"),
        New("Good afternoon, {0}", "Добрый день, {0}"),
        New("Good evening, {0}", "Добрый вечер, {0}"),
        New("Day {0} of an estimated {1}-day cycle", "День {0} предполагаемого цикла длительностью {1} дн."),
        New("Day {0} of {1}", "День {0} из {1}"),
        New("Period in {0}d", "Предполагаемая менструация через {0} дн."),
        New("Prediction from {0}d cycle", "Прогноз на основе цикла длительностью {0} дн."),
        New("Estimate using a {0}-day baseline; personal cycle length unavailable",
            "Оценка по базовому циклу длительностью {0} дн.; личная длительность неизвестна"),
        New("{0} phase (recorded)", "{0} фаза (записана)"),
        New("{0} phase (predicted)", "{0} фаза (прогноз)"),
        New("{0} phase (estimated)", "{0} фаза (оценка)"),
        New("{0} logged {1}", "{0}: запись от {1}"),
        New("{0} · Recorded", "{0} · запись"),
        New("{0} · Predicted", "{0} · прогноз"),
        New("{0} phase", "{0} фаза"),
        New("{0} plan", "План · {0}"),
        New("Exercise {0} of {1}", "Упражнение {0} из {1}"),
        New("{0} of {1} logged", "Записано {0} из {1}"),
        New("{0} skipped", "Пропущено: {0}"),
        New("{0} activities", "Активности: {0}"),
        New("{0} exercises", "Упражнения: {0}"),
        NewNumeric("{0}d", "{0} дн."),
        NewNumeric("{0}h", "{0} ч"),
        NewNumeric("{0} min", "{0} мин"),
        NewNumeric("{0} kcal", "{0} ккал"),
        NewNumeric("{0} kg", "{0} кг"),
        NewNumeric("{0} g", "{0} г"),
        NewNumeric("{0}g", "{0} г"),
        New("<1 min", "<1 мин"),
        New("{0} training days", "Тренировочных дней: {0}"),
        New("{0} full rest days", "Дней полного отдыха: {0}"),
        New("No {0} workouts", "Нет тренировок: {0}"),
        New("Target RPE {0}", "Целевая нагрузка RPE {0}"),
        New("{0} logged", "Записано: {0}"),
        New("{0}: new weight best!", "{0}: новый рекорд по весу!"),
        New("First {0} log. Great start!", "Первая запись: {0}. Отличное начало!"),
        New("{0} sign in could not finish. Please try again.",
            "Не удалось завершить вход через {0}. Повторите попытку."),
        New("{0} keeps the cycle in order. Tap Save phase to log it.",
            "Фаза {0} сохраняет порядок цикла. Нажмите «Сохранить фазу», чтобы записать её."),
        New("This phase would skip a step. Log {0} first.", "Эта фаза пропустит этап. Сначала запишите {0}."),
        New("{0} · time zone unknown", "{0} · часовой пояс неизвестен"),
        New("{0} · {1}", "{0} · {1}")
    ];

    internal static string? Translate(string english)
    {
        foreach (var pattern in Patterns)
        {
            var match = pattern.Matcher.Match(english);
            if (!match.Success)
                continue;

            var values = match.Groups.Cast<Group>().Skip(1)
                .Select(group => UiTextCatalog.Translate(group.Value)).ToArray();
            return string.Format(CultureInfo.CurrentCulture, pattern.Russian, values);
        }

        return null;
    }

    private static Pattern New(string english, string russian)
    {
        var pieces = Regex.Split(english, @"\{\d+\}");
        var regex = "^" + string.Join("(.+?)", pieces.Select(Regex.Escape)) + "$";
        return new Pattern(new Regex(regex, RegexOptions.CultureInvariant), russian);
    }

    private static Pattern NewNumeric(string english, string russian)
    {
        var pieces = Regex.Split(english, @"\{\d+\}");
        var regex = "^" + string.Join(@"(\d+(?:[.,]\d+)?)", pieces.Select(Regex.Escape)) + "$";
        return new Pattern(new Regex(regex, RegexOptions.CultureInvariant), russian);
    }

    private sealed record Pattern(Regex Matcher, string Russian);
}
