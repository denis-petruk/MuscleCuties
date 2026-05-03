using MuscleCuties.App.Pages.Auth;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Pages.Workout;

namespace MuscleCuties.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(QuizPage), typeof(QuizPage));
        Routing.RegisterRoute(nameof(ProfileSetupPage), typeof(ProfileSetupPage));
        Routing.RegisterRoute(nameof(WorkoutDetailPage), typeof(WorkoutDetailPage));
    }
}
