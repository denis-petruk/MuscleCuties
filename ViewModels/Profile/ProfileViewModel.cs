using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace MuscleCuties.ViewModels.Profile;

public class PreferenceItem
{
    public string Title { get; set; } = string.Empty;
}

public partial class ProfileViewModel : ObservableObject
{
    [ObservableProperty]
    private string _userName = "Ana Rivera";

    [ObservableProperty]
    private string _userInitial = "A";

    [ObservableProperty]
    private string _memberSince = "Member since March 2025";

    [ObservableProperty]
    private string _sessionCount = "142";

    [ObservableProperty]
    private string _cycleDays = "28";

    [ObservableProperty]
    private string _phasesTracked = "4";

    public ObservableCollection<PreferenceItem> Preferences { get; } = new();

    public ProfileViewModel()
    {
        Preferences.Add(new PreferenceItem { Title = "Cycle tracking" });
        Preferences.Add(new PreferenceItem { Title = "Notifications" });
        Preferences.Add(new PreferenceItem { Title = "Connected devices" });
        Preferences.Add(new PreferenceItem { Title = "Units & privacy" });
        Preferences.Add(new PreferenceItem { Title = "Help & support" });
    }
}