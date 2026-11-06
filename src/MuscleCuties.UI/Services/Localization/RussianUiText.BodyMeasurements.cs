namespace MuscleCuties.UI.Services.Localization;

internal static partial class RussianUiText
{
    static partial void RegisterBodyMeasurementStrings()
    {
        Values["Profile_Measurement_Title"] = "Замеры тела";
        Values["Profile_Measurement_ViewHistory"] = "Открыть замеры";
        Values["Profile_Measurement_Add"] = "Добавить замер";
        Values["Profile_Measurement_Empty"] = "Замеров пока нет.";
        Values["Profile_Measurement_Working"] = "Обновление замеров";
        Values["Profile_Measurement_Site"] = "Область замера";
        Values["Profile_Measurement_Site_waist"] = "Талия";
        Values["Profile_Measurement_Site_hips"] = "Бёдра";
        Values["Profile_Measurement_Site_chest"] = "Грудь";
        Values["Profile_Measurement_Site_upper-arm"] = "Плечо";
        Values["Profile_Measurement_Site_thigh"] = "Бедро";
        Values["Profile_Measurement_Site_calf"] = "Голень";
        Values["Profile_Measurement_Side"] = "Сторона";
        Values["Profile_Measurement_Side_left"] = "Слева";
        Values["Profile_Measurement_Side_right"] = "Справа";
        Values["Profile_Measurement_SiteAndSide"] = "{0} · {1}";
        Values["Profile_Measurement_Date"] = "Дата замера";
        Values["Profile_Measurement_DateNotAvailable"] = "Замер можно сохранить, когда наступит эта дата по UTC. Введённые данные сохранены в форме.";
        Values["Profile_Measurement_Unit"] = "Единица измерения";
        Values["Profile_Measurement_Value"] = "Обхват ({0})";
        Values["Profile_Measurement_HistoryDetail"] = "{0} · {1} {2}";
        Values["Profile_Measurement_EditReading"] = "Изменить замер: {0}, {1}";
        Values["Profile_Measurement_ShowMore"] = "Показать ещё замеры";
        Values["Profile_Measurement_Save"] = "Сохранить замер";
        Values["Profile_Measurement_Delete"] = "Удалить замер";
        Values["Profile_Measurement_ConfirmDelete"] = "Удалить этот замер из истории?";
        Values["Profile_Measurement_Saved"] = "Замер сохранён.";
        Values["Profile_Measurement_Deleted"] = "Замер удалён.";
        Values["Profile_Measurement_Conflict"] = "Запись изменилась или замер для этой области, стороны и даты уже есть. Проверьте историю и попробуйте снова.";
        Values["Profile_Measurement_RecordChanged"] = "Замер изменился или был удалён. Введённые данные остались в форме. Чтобы внести изменения, откройте замер заново из истории.";
        Values["Profile_Measurement_Invalid"] = "Проверьте область, сторону, дату и значение. Введите положительное число с точностью до двух знаков после запятой.";
        Values["Profile_Measurement_LoadFailed"] = "Не удалось загрузить замеры. Попробуйте ещё раз.";
        Values["Profile_Measurement_WriteFailed"] = "Не удалось подтвердить изменение. Повторите действие, чтобы проверить или завершить его.";
    }
}
