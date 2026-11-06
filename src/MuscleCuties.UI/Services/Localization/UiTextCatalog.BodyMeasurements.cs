namespace MuscleCuties.UI.Services.Localization;

public static partial class UiTextCatalog
{
    static partial void RegisterBodyMeasurementStrings()
    {
        English["Profile_Measurement_Title"] = "Body measurements";
        English["Profile_Measurement_ViewHistory"] = "Open measurements";
        English["Profile_Measurement_Add"] = "Add measurement";
        English["Profile_Measurement_Empty"] = "No measurements recorded yet.";
        English["Profile_Measurement_Working"] = "Updating measurements";
        English["Profile_Measurement_Site"] = "Measurement site";
        English["Profile_Measurement_Site_waist"] = "Waist";
        English["Profile_Measurement_Site_hips"] = "Hips";
        English["Profile_Measurement_Site_chest"] = "Chest";
        English["Profile_Measurement_Site_upper-arm"] = "Upper arm";
        English["Profile_Measurement_Site_thigh"] = "Thigh";
        English["Profile_Measurement_Site_calf"] = "Calf";
        English["Profile_Measurement_Side"] = "Side";
        English["Profile_Measurement_Side_left"] = "Left";
        English["Profile_Measurement_Side_right"] = "Right";
        English["Profile_Measurement_SiteAndSide"] = "{0} · {1}";
        English["Profile_Measurement_Date"] = "Measurement date";
        English["Profile_Measurement_DateNotAvailable"] = "This date can be saved when it starts in UTC. Your draft is still here.";
        English["Profile_Measurement_Unit"] = "Unit";
        English["Profile_Measurement_Value"] = "Circumference ({0})";
        English["Profile_Measurement_HistoryDetail"] = "{0} · {1} {2}";
        English["Profile_Measurement_EditReading"] = "Edit {0}, {1}";
        English["Profile_Measurement_ShowMore"] = "Show more measurements";
        English["Profile_Measurement_Save"] = "Save measurement";
        English["Profile_Measurement_Delete"] = "Delete measurement";
        English["Profile_Measurement_ConfirmDelete"] = "Delete this reading from your history?";
        English["Profile_Measurement_Saved"] = "Measurement saved.";
        English["Profile_Measurement_Deleted"] = "Measurement deleted.";
        English["Profile_Measurement_Conflict"] = "This reading changed or one already exists for this site, side, and date. Check your history before trying again.";
        English["Profile_Measurement_RecordChanged"] = "This reading changed or was removed. Your draft is still here. Reopen the reading from history to make changes.";
        English["Profile_Measurement_Invalid"] = "Check the site, side, date, and value. Enter a positive measurement with up to two decimal places.";
        English["Profile_Measurement_LoadFailed"] = "Measurements could not be loaded. Try again.";
        English["Profile_Measurement_WriteFailed"] = "The measurement change could not be confirmed. Retry to check or complete it.";
    }
}
