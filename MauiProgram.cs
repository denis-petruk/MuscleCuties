using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MuscleCuties.Data;
using MuscleCuties.Pages.Auth;
using MuscleCuties.Pages.Cycle;
using MuscleCuties.Pages.Dashboard;
using MuscleCuties.Pages.Nutrition;
using MuscleCuties.Pages.Onboarding;
using MuscleCuties.Pages.Profile;
using MuscleCuties.Pages.Workout;
using MuscleCuties.Repositories;
using MuscleCuties.Services;
using MuscleCuties.ViewModels.Auth;
using MuscleCuties.ViewModels.Cycle;
using MuscleCuties.ViewModels.Dashboard;
using MuscleCuties.ViewModels.Nutrition;
using MuscleCuties.ViewModels.Onboarding;
using MuscleCuties.ViewModels.Profile;
using MuscleCuties.ViewModels.Workout;

namespace MuscleCuties;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Nunito-Variable.ttf", "NunitoRegular");
                fonts.AddFont("Fraunces-Variable.ttf", "FrauncesDisplay");
            });

        // Database
        builder.Services.AddDbContext<AppDatabase>();

        // Repositories
        builder.Services.AddTransient<IUserRepository, UserRepository>();
        builder.Services.AddTransient<ICycleRepository, CycleRepository>();
        builder.Services.AddTransient<ISymptomRepository, SymptomRepository>();
        builder.Services.AddTransient<IQuizRepository, QuizRepository>();
        builder.Services.AddTransient<IWorkoutRepository, WorkoutRepository>();
        builder.Services.AddTransient<INutritionRepository, NutritionRepository>();

        // Services
        builder.Services.AddTransient<IAuthService, AuthService>();
        builder.Services.AddTransient<ICycleService, CycleService>();
        builder.Services.AddTransient<INutritionService, NutritionService>();
        builder.Services.AddTransient<IQuizService, QuizService>();

        // Shell
        builder.Services.AddTransient<AppShell>();

        // Auth pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();

        // Onboarding pages
        builder.Services.AddTransient<QuizPage>();
        builder.Services.AddTransient<ProfileSetupPage>();
        builder.Services.AddTransient<QuizViewModel>();
        builder.Services.AddTransient<ProfileSetupViewModel>();

        // Main tab pages + ViewModels
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<DashboardViewModel>();

        builder.Services.AddTransient<CyclePage>();
        builder.Services.AddTransient<CycleViewModel>();

        builder.Services.AddTransient<WorkoutPage>();
        builder.Services.AddTransient<WorkoutViewModel>();

        builder.Services.AddTransient<NutritionPage>();
        builder.Services.AddTransient<NutritionViewModel>();

        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<ProfileViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDatabase>();
        db.InitializeAsync().GetAwaiter().GetResult();
        return app;
    }
}