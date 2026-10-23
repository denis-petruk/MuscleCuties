namespace MuscleCuties.UI.Services.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly Dictionary<string, string> RuStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navigation / Tabs
        ["Tab_Today"] = "Сегодня",
        ["Tab_Cycle"] = "Цикл",
        ["Tab_Train"] = "Тренировки",
        ["Tab_Nutrition"] = "Питание",
        ["Tab_You"] = "Профиль",

        // Common
        ["Common_Save"] = "Сохранить",
        ["Common_Cancel"] = "Отмена",
        ["Common_Back"] = "Назад",
        ["Common_Done"] = "Готово",
        ["Common_Delete"] = "Удалить",
        ["Common_Edit"] = "Редактировать",
        ["Common_Loading"] = "Загрузка...",
        ["Common_LocalOnly"] = "Локально",
        ["Common_Days"] = "дн.",
        ["Common_Active"] = "Активно",

        // Auth / Onboarding
        ["Auth_SignIn"] = "Вход",
        ["Auth_Register"] = "Регистрация",
        ["Auth_Email"] = "Электронная почта",
        ["Auth_Password"] = "Пароль",
        ["Auth_AgeConsent"] = "Мне исполнилось 18 лет",
        ["Auth_WellnessDisclaimer"] =
            "Только для оздоровительных и фитнес целей. Не является медицинской консультацией.",
        ["Auth_UnderageError"] = "Приложение предназначено для пользователей старше 18 лет.",

        // Dashboard / Today
        ["Today_Readiness"] = "Готовность к тренировке",
        ["Today_Hydration"] = "Водный баланс",
        ["Today_Nutrition"] = "Питание",
        ["Today_Streak"] = "Серия",
        ["Today_CheckIn"] = "Ежедневная отметка",
        ["Today_WorkoutScheduled"] = "Тренировка на сегодня",
        ["Today_RestDay"] = "День отдыха",

        // Cycle & Symptoms
        ["Cycle_Menstrual"] = "Менструальная фаза",
        ["Cycle_Follicular"] = "Фолликулярная фаза",
        ["Cycle_Ovulatory"] = "Овуляторная фаза",
        ["Cycle_Luteal"] = "Лютеиновая фаза",
        ["Cycle_PeriodStart"] = "Начало цикла",
        ["Cycle_PeriodEnd"] = "Конец цикла",
        ["Cycle_Flow"] = "Интенсивность выделений",
        ["Cycle_Flow_Spotting"] = "Мажущие",
        ["Cycle_Flow_Light"] = "Скудные",
        ["Cycle_Flow_Medium"] = "Умеренные",
        ["Cycle_Flow_Heavy"] = "Обильные",
        ["Cycle_Cramps"] = "Спазмы",
        ["Cycle_Bloating"] = "Вздутие",
        ["Cycle_BreastTenderness"] = "Чувствительность груди",
        ["Cycle_Acne"] = "Акне",
        ["Cycle_Headache"] = "Головная боль",
        ["Cycle_MoodSwings"] = "Перепады настроения",
        ["Cycle_Fatigue"] = "Усталость",
        ["Cycle_Insomnia"] = "Бессонница",
        ["Cycle_Cravings"] = "Тяга к сладкому/соленому",
        ["Cycle_HotFlashes"] = "Приливы",
        ["Cycle_Backache"] = "Боль в пояснице",
        ["Cycle_Nausea"] = "Тошнота",
        ["Cycle_Disclaimer"] = "Прогнозы являются ориентировочными и не предназначены для контрацепции или зачатия.",

        // Workouts & Injuries
        ["Workout_Sets"] = "Подходы",
        ["Workout_Reps"] = "Повторения",
        ["Workout_Weight"] = "Вес",
        ["Workout_Duration"] = "Длительность",
        ["Workout_Rest"] = "Отдых",
        ["Workout_SwapExercise"] = "Заменить упражнение",
        ["Workout_LogInjury"] = "Отметить травму / дискомфорт",
        ["Workout_Injury_Ankle"] = "Лодыжка",
        ["Workout_Injury_Knee"] = "Колено",
        ["Workout_Injury_Hip"] = "Тазобедренный сустав",
        ["Workout_Injury_LowBack"] = "Поясница",
        ["Workout_Injury_Shoulder"] = "Плечо",
        ["Workout_Injury_Elbow"] = "Локоть",
        ["Workout_Injury_Wrist"] = "Запястье",
        ["Workout_Injury_Neck"] = "Шея",

        // Nutrition & Meals
        ["Nutrition_Breakfast"] = "Завтрак",
        ["Nutrition_Lunch"] = "Обед",
        ["Nutrition_Dinner"] = "Ужин",
        ["Nutrition_Snack"] = "Перекус",
        ["Nutrition_Calories"] = "Калории",
        ["Nutrition_Protein"] = "Белки",
        ["Nutrition_Carbs"] = "Углеводы",
        ["Nutrition_Fats"] = "Жиры",
        ["Nutrition_CustomFood"] = "Своя еда",
        ["Nutrition_AddFood"] = "Добавить продукт",

        // Profile & Privacy
        ["Profile_PersonalInfo"] = "Личные данные",
        ["Profile_Units"] = "Единицы измерения",
        ["Profile_NutritionSettings"] = "Цели питания",
        ["Profile_WorkoutPreferences"] = "Предпочтения тренировок",
        ["Profile_PhotoVault"] = "Личный фотоальбом",
        ["Profile_PrivacySecurity"] = "Конфиденциальность и безопасность",
        ["Profile_EncryptedStorage"] =
            "Записи о здоровье хранятся в личном облачном аккаунте; фотографии — в приватном медиа-хранилище.",
        ["Profile_PregnancyQuestion"] = "Беременность или грудное вскармливание",
        ["Profile_ContraceptionQuestion"] = "Контрацепция или особенности цикла",
        ["Profile_PreferNotToSay"] = "Не указывать",
        ["Profile_No"] = "Нет",
        ["Profile_Yes"] = "Да",
        ["Profile_RegularCycle"] = "Регулярный цикл (без гормональной контрацепции)",
        ["Profile_HormonalContraception"] = "Гормональная контрацепция",
        ["Profile_IrregularCycle"] = "Нерегулярный цикл"
    };

    private static readonly Dictionary<string, string> EnStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navigation / Tabs
        ["Tab_Today"] = "Today",
        ["Tab_Cycle"] = "Cycle",
        ["Tab_Train"] = "Train",
        ["Tab_Nutrition"] = "Nutrition",
        ["Tab_You"] = "You",

        // Common
        ["Common_Save"] = "Save",
        ["Common_Cancel"] = "Cancel",
        ["Common_Back"] = "Back",
        ["Common_Done"] = "Done",
        ["Common_Delete"] = "Delete",
        ["Common_Edit"] = "Edit",
        ["Common_Loading"] = "Loading...",
        ["Common_LocalOnly"] = "Local only",
        ["Common_Days"] = "days",
        ["Common_Active"] = "Active",

        // Auth / Onboarding
        ["Auth_SignIn"] = "Sign in",
        ["Auth_Register"] = "Register",
        ["Auth_Email"] = "Email",
        ["Auth_Password"] = "Password",
        ["Auth_AgeConsent"] = "I am at least 18 years old",
        ["Auth_WellnessDisclaimer"] = "For wellness and fitness purposes only. Not medical advice.",
        ["Auth_UnderageError"] = "MuscleCuties is designed for adults aged 18 and older.",

        // Dashboard / Today
        ["Today_Readiness"] = "Readiness",
        ["Today_Hydration"] = "Hydration",
        ["Today_Nutrition"] = "Nutrition",
        ["Today_Streak"] = "Streak",
        ["Today_CheckIn"] = "Daily check-in",
        ["Today_WorkoutScheduled"] = "Scheduled workout",
        ["Today_RestDay"] = "Rest day",

        // Cycle & Symptoms
        ["Cycle_Menstrual"] = "Menstrual",
        ["Cycle_Follicular"] = "Follicular",
        ["Cycle_Ovulatory"] = "Ovulatory",
        ["Cycle_Luteal"] = "Luteal",
        ["Cycle_PeriodStart"] = "Period start",
        ["Cycle_PeriodEnd"] = "Period end",
        ["Cycle_Flow"] = "Flow intensity",
        ["Cycle_Flow_Spotting"] = "Spotting",
        ["Cycle_Flow_Light"] = "Light",
        ["Cycle_Flow_Medium"] = "Medium",
        ["Cycle_Flow_Heavy"] = "Heavy",
        ["Cycle_Cramps"] = "Cramps",
        ["Cycle_Bloating"] = "Bloating",
        ["Cycle_BreastTenderness"] = "Breast tenderness",
        ["Cycle_Acne"] = "Acne",
        ["Cycle_Headache"] = "Headache",
        ["Cycle_MoodSwings"] = "Mood swings",
        ["Cycle_Fatigue"] = "Fatigue",
        ["Cycle_Insomnia"] = "Insomnia",
        ["Cycle_Cravings"] = "Cravings",
        ["Cycle_HotFlashes"] = "Hot flashes",
        ["Cycle_Backache"] = "Backache",
        ["Cycle_Nausea"] = "Nausea",
        ["Cycle_Disclaimer"] = "Cycle projections are wellness estimates only, not for contraception or conception.",

        // Workouts & Injuries
        ["Workout_Sets"] = "Sets",
        ["Workout_Reps"] = "Reps",
        ["Workout_Weight"] = "Weight",
        ["Workout_Duration"] = "Duration",
        ["Workout_Rest"] = "Rest",
        ["Workout_SwapExercise"] = "Swap exercise",
        ["Workout_LogInjury"] = "Log discomfort / injury",
        ["Workout_Injury_Ankle"] = "Ankle",
        ["Workout_Injury_Knee"] = "Knee",
        ["Workout_Injury_Hip"] = "Hip",
        ["Workout_Injury_LowBack"] = "Low back",
        ["Workout_Injury_Shoulder"] = "Shoulder",
        ["Workout_Injury_Elbow"] = "Elbow",
        ["Workout_Injury_Wrist"] = "Wrist",
        ["Workout_Injury_Neck"] = "Neck",

        // Nutrition & Meals
        ["Nutrition_Breakfast"] = "Breakfast",
        ["Nutrition_Lunch"] = "Lunch",
        ["Nutrition_Dinner"] = "Dinner",
        ["Nutrition_Snack"] = "Snack",
        ["Nutrition_Calories"] = "Calories",
        ["Nutrition_Protein"] = "Protein",
        ["Nutrition_Carbs"] = "Carbs",
        ["Nutrition_Fats"] = "Fats",
        ["Nutrition_CustomFood"] = "Custom food",
        ["Nutrition_AddFood"] = "Add food",

        // Profile & Privacy
        ["Profile_PersonalInfo"] = "Personal info",
        ["Profile_Units"] = "Units and display",
        ["Profile_NutritionSettings"] = "Nutrition goals",
        ["Profile_WorkoutPreferences"] = "Workout preferences",
        ["Profile_PhotoVault"] = "Photo vault",
        ["Profile_PrivacySecurity"] = "Privacy and security",
        ["Profile_EncryptedStorage"] =
            "Health records are stored securely in your private cloud account; photos are in private media storage.",
        ["Profile_PregnancyQuestion"] = "Currently pregnant or breastfeeding",
        ["Profile_ContraceptionQuestion"] = "Hormonal contraception or cycle pattern",
        ["Profile_PreferNotToSay"] = "Prefer not to say",
        ["Profile_No"] = "No",
        ["Profile_Yes"] = "Yes",
        ["Profile_RegularCycle"] = "Regular cycle (no hormonal contraception)",
        ["Profile_HormonalContraception"] = "Hormonal contraception",
        ["Profile_IrregularCycle"] = "Irregular cycle"
    };

    public LocalizationService()
    {
        CurrentLanguage = ActiveLanguage;
    }

    internal static string ActiveLanguage { get; private set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? "ru"
            : "en";

    public string CurrentLanguage { get; private set; }

    public void SetLanguage(string languageCode)
    {
        if (string.Equals(languageCode, "ru", StringComparison.OrdinalIgnoreCase))
            CurrentLanguage = "ru";
        else
            CurrentLanguage = "en";
        ActiveLanguage = CurrentLanguage;
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var dict = CurrentLanguage == "ru" ? RuStrings : EnStrings;
        if (dict.TryGetValue(key, out var val))
            return val;

        // Fallback to English
        if (EnStrings.TryGetValue(key, out var enVal))
            return enVal;

        return key;
    }

    public string this[string key] => GetString(key);
}
