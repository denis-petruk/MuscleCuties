using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuscleCuties.App.Pages.Auth;
using MuscleCuties.App.Pages.Cycle;
using MuscleCuties.App.Pages.Dashboard;
using MuscleCuties.App.Pages.Nutrition;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Pages.Profile;
using MuscleCuties.App.Pages.Workout;
using MuscleCuties.App.Services;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Repositories;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.ViewModels;

namespace MuscleCuties.App;

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

        var services = builder.Services;

        // Infrastructure
        services.AddSingleton<IDbPathProvider, MauiDbPathProvider>();
        services.AddDbContext<AppDatabase>((sp, opts) =>
        {
            var path = sp.GetRequiredService<IDbPathProvider>().GetDatabasePath();
            opts.UseSqlite($"Filename={path}");
        });

        // Platform services
        services.AddSingleton<ISecureStorage, SecureStorageService>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICycleRepository, CycleRepository>();
        services.AddScoped<INutritionRepository, NutritionRepository>();
        services.AddScoped<ISymptomRepository, SymptomRepository>();
        services.AddScoped<IWorkoutRepository, WorkoutRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();

        // Services
        services.AddScoped<ICalorieCalculator, CalorieCalculator>();
        services.AddScoped<ICyclePhaseCalculator, CyclePhaseCalculator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICycleService, CycleService>();
        services.AddScoped<INutritionService, NutritionService>();
        services.AddScoped<IQuizService, QuizService>();

        // ViewModels — transient so each page gets a fresh instance
        services.AddTransient<LoginViewModel>(sp => new LoginViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IQuizService>(),
            () => Shell.Current.GoToAsync("//DashboardPage"),
            () => Shell.Current.GoToAsync(nameof(QuizPage)),
            () => Shell.Current.GoToAsync(nameof(RegisterPage))));

        services.AddTransient<RegisterViewModel>(sp => new RegisterViewModel(
            sp.GetRequiredService<IAuthService>(),
            () => Shell.Current.GoToAsync(nameof(QuizPage)),
            () => Shell.Current.GoToAsync("..")));

        services.AddTransient<QuizViewModel>(sp => new QuizViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IQuizService>(),
            () => Shell.Current.GoToAsync(nameof(ProfileSetupPage))));

        services.AddTransient<ProfileSetupViewModel>(sp => new ProfileSetupViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            () => Shell.Current.GoToAsync("//DashboardPage")));

        services.AddTransient<DashboardViewModel>(sp => new DashboardViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<INutritionService>(),
            sp.GetRequiredService<IWorkoutRepository>(),
            () => Shell.Current.GoToAsync("//CyclePage"),
            () => Shell.Current.GoToAsync("//WorkoutPage"),
            () => Shell.Current.GoToAsync("//NutritionPage")));

        services.AddTransient<CycleViewModel>(sp => new CycleViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<ICycleService>()));

        services.AddTransient<NutritionViewModel>(sp => new NutritionViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<INutritionService>()));

        services.AddTransient<WorkoutViewModel>(sp => new WorkoutViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<IWorkoutRepository>()));

        services.AddTransient<ProfileViewModel>(sp => new ProfileViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            () => Shell.Current.GoToAsync("//LoginPage")));

        // Pages
        services.AddTransient<LoginPage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<QuizPage>();
        services.AddTransient<ProfileSetupPage>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<CyclePage>();
        services.AddTransient<NutritionPage>();
        services.AddTransient<WorkoutPage>();
        services.AddTransient<ProfilePage>();

        // Shell
        services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
