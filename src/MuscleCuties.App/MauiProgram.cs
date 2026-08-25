using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Handlers;
using MuscleCuties.App.Pages.Auth;
using MuscleCuties.App.Pages.Cycle;
using MuscleCuties.App.Pages.Dashboard;
using MuscleCuties.App.Pages.Nutrition;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Pages.Profile;
using MuscleCuties.App.Pages.Startup;
using MuscleCuties.App.Pages.Workout;
using MuscleCuties.App.Services;
using MuscleCuties.App.Services.Notifications;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.ViewModels.Auth;
using MuscleCuties.Core.ViewModels.Cycle;
using MuscleCuties.Core.ViewModels.Dashboard;
using MuscleCuties.Core.ViewModels.Nutrition;
using MuscleCuties.Core.ViewModels.Profile;
using MuscleCuties.Core.ViewModels.Quiz;
using MuscleCuties.Core.ViewModels.Workout;

namespace MuscleCuties.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                EntryHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                {
                    handler.PlatformView.BackgroundTintList =
                        Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                    handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                    handler.PlatformView.SetPadding(0, 0, 0, 0);
                });
                PickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                {
                    handler.PlatformView.BackgroundTintList =
                        Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                    handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                    handler.PlatformView.SetPadding(0, 0, 0, 0);
                });
                DatePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                {
                    handler.PlatformView.BackgroundTintList =
                        Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                    handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                    handler.PlatformView.SetPadding(0, 0, 0, 0);
                });
                TimePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                {
                    handler.PlatformView.BackgroundTintList =
                        Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                    handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                    handler.PlatformView.SetPadding(0, 0, 0, 0);
                });
#elif IOS || MACCATALYST
                EntryHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                {
                    handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
                    handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
                });
                PickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                {
                    handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
                    handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
                });
                DatePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                {
                    handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
                });
                TimePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
                {
                    handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
                });
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "NunitoRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "FrauncesDisplay");
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
        services.AddSingleton<ITokenStorage, SecureStorageService>();
        services.AddSingleton<ILocalNotificationService, LocalNotificationService>();
        services.AddScoped<ICyclePhaseNotificationService, CyclePhaseNotificationService>();
        services.AddScoped<IFeedbackEmailService, FeedbackEmailService>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICycleRepository, CycleRepository>();
        services.AddScoped<INutritionRepository, NutritionRepository>();
        services.AddScoped<ISymptomRepository, SymptomRepository>();
        services.AddScoped<IWorkoutRepository, WorkoutRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<IMealTemplateRepository, MealTemplateRepository>();
        services.AddScoped<IFoodSyncRepository, FoodSyncRepository>();
        // Services
        services.AddSingleton(_ => new HttpClient
        {
            BaseAddress = FdcApiClient.BaseUri,
            Timeout = TimeSpan.FromSeconds(8)
        });
        services.AddScoped<IFdcApiClient, FdcApiClient>();
        services.AddScoped<IFoodSyncService, FoodSyncService>();
        services.AddScoped<ICalorieCalculator, CalorieCalculator>();
        services.AddScoped<INutritionPlanner, NutritionPlanner>();
        services.AddScoped<ICyclePhaseCalculator, CyclePhaseCalculator>();
        services.AddScoped<ICyclePredictionPlanner, CyclePredictionPlanner>();
        services.AddScoped<IDashboardPlanner, DashboardPlanner>();
        services.AddScoped<IWorkoutPlanner, WorkoutPlanner>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICycleService, CycleService>();
        services.AddScoped<INutritionService, NutritionService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IWorkoutService, WorkoutService>();
        // ViewModels — transient so each page gets a fresh instance
        services.AddTransient<LoginViewModel>(sp => new LoginViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IQuizService>(),
            () => NavigateTo("//DashboardPage"),
            () => NavigateTo(nameof(QuizPage)),
            () => NavigateTo(nameof(RegisterPage))));

        services.AddTransient<RegisterViewModel>(sp => new RegisterViewModel(
            sp.GetRequiredService<IAuthService>(),
            () => NavigateTo(nameof(QuizPage)),
            () => NavigateTo("..")));

        services.AddTransient<QuizViewModel>(sp => new QuizViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IQuizService>(),
            () => NavigateTo(nameof(ProfileSetupPage))));

        services.AddTransient<ProfileSetupViewModel>(sp => new ProfileSetupViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            () => NavigateTo("//DashboardPage")));

        services.AddTransient<DashboardViewModel>(sp => new DashboardViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<INutritionService>(),
            sp.GetRequiredService<IWorkoutService>(),
            sp.GetRequiredService<IDashboardPlanner>(),
            () => NavigateTo("//CyclePage"),
            () => NavigateTo("//WorkoutPage"),
            () => NavigateTo("//NutritionPage")));

        services.AddTransient<CycleViewModel>(sp => new CycleViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<IUserRepository>(),
            phase => NavigateTo($"{nameof(CyclePhaseDetailPage)}?phase={phase}")));

        services.AddTransient<CyclePhaseDetailViewModel>(sp => new CyclePhaseDetailViewModel(
            () => NavigateTo("..")));

        services.AddTransient<NutritionViewModel>(sp => new NutritionViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<INutritionService>()));

        services.AddTransient<WorkoutViewModel>(sp => new WorkoutViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<IWorkoutService>()));

        services.AddTransient<ProfileViewModel>(sp => new ProfileViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            () => NavigateTo("//LoginPage"),
            NavigateTo));

        services.AddTransient<ProfilePersonalInfoViewModel>(sp => new ProfilePersonalInfoViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            () => NavigateTo("..")));

        services.AddTransient<ProfileNutritionSettingsViewModel>(sp => new ProfileNutritionSettingsViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<INutritionPlanner>(),
            () => NavigateTo("..")));

        services.AddTransient<ProfileWorkoutPreferencesViewModel>(sp => new ProfileWorkoutPreferencesViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<IWorkoutService>(),
            () => NavigateTo("..")));

        services.AddTransient<ProfileUnitsDisplayViewModel>(sp => new ProfileUnitsDisplayViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            () => NavigateTo("..")));

        services.AddTransient<ProfileFeedbackViewModel>(sp => new ProfileFeedbackViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<IFeedbackEmailService>(),
            () => NavigateTo("..")));

        // Pages
        services.AddTransient<LoginPage>();
        services.AddTransient<AppStartupPage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<QuizPage>();
        services.AddTransient<ProfileSetupPage>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<CyclePage>();
        services.AddTransient<CyclePhaseDetailPage>();
        services.AddTransient<NutritionPage>();
        services.AddTransient<WorkoutPage>();
        services.AddTransient<ProfilePage>();
        services.AddTransient<ProfilePersonalInfoPage>();
        services.AddTransient<ProfileNutritionSettingsPage>();
        services.AddTransient<ProfileWorkoutPreferencesPage>();
        services.AddTransient<ProfileUnitsDisplayPage>();
        services.AddTransient<ProfileFeedbackPage>();
        services.AddTransient<ProfilePrivacyPage>();

        // Shell
        services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void NavigateTo(string route)
    {
        MainThread.BeginInvokeOnMainThread(() => _ = Shell.Current.GoToAsync(route, true));
    }
}
