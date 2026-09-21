using CommunityToolkit.Maui;
using MauiIcons.Fluent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using MuscleCuties.App.Pages;
using MuscleCuties.App.Pages.Auth;
using MuscleCuties.App.Pages.Cycle;
using MuscleCuties.App.Pages.Dashboard;
using MuscleCuties.App.Pages.Nutrition;
using MuscleCuties.App.Pages.Onboarding;
using MuscleCuties.App.Pages.Profile;
using MuscleCuties.App.Pages.Startup;
using MuscleCuties.App.Pages.Workout;
using MuscleCuties.App.Services;
using MuscleCuties.App.Services.Auth;
using MuscleCuties.App.Services.Notifications;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Core.Services.Progress;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Repositories.Workout.Planning;
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
            .UseFluentMauiIcons()
            .ConfigureMauiHandlers(ConfigureInputHandlers)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Variable.ttf", "Inter");
                fonts.AddFont("Fluent_Icons_Regular.ttf", "FluentIcons");
            });

        var services = builder.Services;
        RegisterPlatformServices(services);
        RegisterRepositories(services);
        RegisterDomainServices(services);
        RegisterWorkoutPlanningServices(services);
        RegisterViewModels(services);
        RegisterPages(services);
        ConfigureLogging(builder);

        return builder.Build();
    }

    private static void ConfigureInputHandlers(IMauiHandlersCollection handlers)
    {
#if ANDROID
        EntryHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
        {
            ClearAndroidInputChrome(handler.PlatformView);
            ConfigureAndroidAutofill(handler.PlatformView, handler.VirtualView.AutomationId);
        });
        PickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
            ClearAndroidInputChrome(handler.PlatformView));
        DatePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
            ClearAndroidInputChrome(handler.PlatformView));
        TimePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
            ClearAndroidInputChrome(handler.PlatformView));
#elif IOS || MACCATALYST
        EntryHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
        {
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;

            var textContentType = handler.VirtualView.AutomationId switch
            {
                "LoginEmail" or "RegisterEmail" => UIKit.UITextContentType.Username,
                "LoginPassword" => UIKit.UITextContentType.Password,
                "RegisterPassword" or "RegisterConfirmPassword" => UIKit.UITextContentType.NewPassword,
                _ => null
            };

            if (textContentType is not null)
                handler.PlatformView.TextContentType = textContentType;
        });
        PickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
        {
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
        });
        DatePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
            handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear);
        TimePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
            handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear);
#endif
    }

#if ANDROID
    private static void ClearAndroidInputChrome(Android.Views.View view)
    {
        view.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        view.SetBackgroundColor(Android.Graphics.Color.Transparent);
        view.SetPadding(0, 0, 0, 0);
    }

    private static void ConfigureAndroidAutofill(Android.Widget.EditText editText, string? automationId)
    {
        var hint = automationId switch
        {
            "LoginEmail" or "RegisterEmail" => Android.Views.View.AutofillHintEmailAddress,
            "LoginPassword" => Android.Views.View.AutofillHintPassword,
            "RegisterPassword" or "RegisterConfirmPassword" => "newPassword",
            _ => null
        };

        if (hint is null) return;

        editText.ImportantForAutofill = Android.Views.ImportantForAutofill.Yes;
        editText.SetAutofillHints(hint);
    }
#endif

    private static void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IDbPathProvider, MauiDbPathProvider>();
        // One context per operation scope. Singleton view models must inject
        // IServiceScopeFactory and resolve database services inside that scope.
        services.AddDbContext<AppDatabase>((sp, opts) =>
        {
            var path = sp.GetRequiredService<IDbPathProvider>().GetDatabasePath();
            opts.UseSqlite($"Filename={path}");
        }, contextLifetime: ServiceLifetime.Scoped);

        services.AddSingleton<ITokenStorage, SecureStorageService>();
        services.AddSingleton<ILocalNotificationService, LocalNotificationService>();
#if ANDROID
        services.AddSingleton<IPlatformSignInService, GoogleSignInService>();
#else
        services.AddSingleton<AppleSignInService>();
        services.AddSingleton<IAppleSignInService>(sp => sp.GetRequiredService<AppleSignInService>());
        services.AddSingleton<IPlatformSignInService>(sp => sp.GetRequiredService<AppleSignInService>());
#endif

        services.AddSingleton<IHealthSyncService, DisabledHealthSyncService>();
        services.AddScoped<ICyclePhaseNotificationService, CyclePhaseNotificationService>();
        services.AddScoped<IFeedbackEmailService, FeedbackEmailService>();
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICycleRepository, CycleRepository>();
        services.AddScoped<INutritionRepository, NutritionRepository>();
        services.AddScoped<IWorkoutRepository, WorkoutRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<IFoodSyncRepository, FoodSyncRepository>();
    }

    private static void RegisterDomainServices(IServiceCollection services)
    {
        services.AddHttpClient<IFdcApiClient, FdcApiClient>(client =>
        {
            client.BaseAddress = FdcApiClient.BaseUri;
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddScoped<IFoodSyncService, FoodSyncService>();
        services.AddScoped<ICalorieCalculator, CalorieCalculator>();
        services.AddScoped<INutritionPlanner, NutritionPlanner>();
        services.AddScoped<ICyclePhaseCalculator, CyclePhaseCalculator>();
        services.AddScoped<ICyclePredictionPlanner, CyclePredictionPlanner>();
        services.AddScoped<IDashboardPlanner, DashboardPlanner>();
        services.AddScoped<IWorkoutPlanner, WorkoutPlanner>();
        services.AddScoped<IWorkoutPlanGenerator, WorkoutPlanGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICycleService, CycleService>();
        services.AddScoped<ISuggestedMealService, SuggestedMealService>();
        services.AddScoped<INutritionService, NutritionService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IWorkoutService, WorkoutService>();
        services.AddScoped<IProgressSummaryService, ProgressSummaryService>();
    }

    private static void RegisterWorkoutPlanningServices(IServiceCollection services)
    {
        services.AddSingleton(WorkoutPlanningConfig.CreateDefault());

        services.AddSingleton<IReadinessEngine, ReadinessEngine>();
        services.AddSingleton<IGatingEngine, GatingEngine>();

        services.AddScoped<IReadinessRepository, ReadinessRepository>();
        services.AddScoped<IWorkoutInjuryRepository, WorkoutInjuryRepository>();
        services.AddScoped<IHealthReadinessBridge, HealthReadinessBridge>();
        services.AddScoped<IDailyCheckInNotificationService, DailyCheckInNotificationService>();

        services.AddScoped<IContributionLookup, ContributionLookup>();
        services.AddScoped<IVolumeBudgetResolver, VolumeBudgetResolver>();
        services.AddScoped<IWeekPlanGenerator, WeekPlanGenerator>();
        services.AddScoped<IExercisePickerService, ExercisePickerService>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<LoginViewModel>(sp => new LoginViewModel(
            sp.GetRequiredService<IAuthService>(),
            () => PreloadAndNavigateToDashboardAsync(sp),
            () => NavigateAuthenticatedAsync(sp, $"//{nameof(ProfileSetupPage)}"),
            () => NavigateToAsync(nameof(RegisterPage)),
            sp.GetRequiredService<IPlatformSignInService>()));

        services.AddTransient<RegisterViewModel>(sp => new RegisterViewModel(
            sp.GetRequiredService<IAuthService>(),
            () => NavigateAuthenticatedAsync(sp, $"//{nameof(ProfileSetupPage)}"),
            () => NavigateToAsync(".."),
            sp.GetRequiredService<IPlatformSignInService>(),
            () => PreloadAndNavigateToDashboardAsync(sp)));

        services.AddSingleton<QuizQuestionCache>();

        services.AddTransient<QuizViewModel>(sp => new QuizViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IAppPreloadService>(),
            sp.GetRequiredService<QuizQuestionCache>(),
            () => PrepareAndNavigateToDashboardAsync(sp)));

        services.AddTransient<ProfileSetupViewModel>(sp => new ProfileSetupViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<IQuizService>(),
            sp.GetRequiredService<QuizQuestionCache>(),
            () => NavigateAuthenticatedAsync(sp, $"//{nameof(QuizPage)}")));

        services.AddSingleton<DashboardViewModel>(sp => new DashboardViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            () => NavigateToAsync("//CyclePage"),
            () => NavigateToAsync("//WorkoutPage"),
            () => NavigateToAsync("//NutritionPage"),
            () => NavigateToAsync(nameof(DailyCheckInPage))));

        services.AddTransient<DailyCheckInViewModel>(sp => new DailyCheckInViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<IReadinessRepository>(),
            sp.GetRequiredService<IHealthReadinessBridge>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<IReadinessEngine>(),
            sp.GetRequiredService<IDailyCheckInNotificationService>(),
            sp.GetRequiredService<IWorkoutService>(),
            sp.GetRequiredService<IAppPreloadService>(),
            () => NavigateToAsync("//DashboardPage")));

        services.AddSingleton<CycleViewModel>(sp => new CycleViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            phase => NavigateToAsync($"{nameof(CyclePhaseDetailPage)}?phase={phase}")));

        services.AddTransient<CyclePhaseDetailViewModel>(sp => new CyclePhaseDetailViewModel(() => NavigateToAsync("..")));

        services.AddSingleton<NutritionViewModel>(sp => new NutritionViewModel(
            sp.GetRequiredService<IServiceScopeFactory>()));

        services.AddSingleton<WorkoutViewModel>(sp => new WorkoutViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            route => NavigateToAsync(route)));

        services.AddTransient<WorkoutSessionViewModel>(sp => new WorkoutSessionViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            route => NavigateToAsync(route)));

        services.AddSingleton<ProfileViewModel>(sp => new ProfileViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new Lazy<IAppPreloadService>(sp.GetRequiredService<IAppPreloadService>),
            () => NavigateToAsync("//LoginPage"),
            NavigateToAsync));

        services.AddTransient<ProfilePersonalInfoViewModel>(sp => new ProfilePersonalInfoViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<IAppPreloadService>(),
            () => NavigateToAsync("//ProfilePage")));

        services.AddTransient<ProfileNutritionSettingsViewModel>(sp => new ProfileNutritionSettingsViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<INutritionPlanner>(),
            sp.GetRequiredService<IAppPreloadService>(),
            () => NavigateToAsync("//NutritionPage")));

        services.AddTransient<ProfileWorkoutPreferencesViewModel>(sp => new ProfileWorkoutPreferencesViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<IWorkoutService>(),
            sp.GetRequiredService<IAppPreloadService>(),
            () => NavigateToAsync("//WorkoutPage")));

        services.AddTransient<ProfileHealthSyncViewModel>(sp => new ProfileHealthSyncViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<INutritionService>(),
            sp.GetRequiredService<IWorkoutService>(),
            sp.GetRequiredService<IDashboardPlanner>(),
            sp.GetRequiredService<IHealthSyncService>(),
            () => NavigateToAsync("..")));

        services.AddTransient<ProfileUnitsDisplayViewModel>(sp => new ProfileUnitsDisplayViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            () => NavigateToAsync("//ProfilePage")));

        services.AddTransient<ProfileFeedbackViewModel>(sp => new ProfileFeedbackViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<IFeedbackEmailService>(),
            () => NavigateToAsync("//ProfilePage")));

        services.AddTransient<InjuryLogViewModel>(sp => new InjuryLogViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            () => NavigateToAsync("..")));

        services.AddSingleton<IAppPreloadService, AppPreloadService>();
    }

    private static void RegisterPages(IServiceCollection services)
    {
        services.AddTransient<WorkoutSessionPage>();
        services.AddTransient<LoginPage>();
        services.AddTransient<AppStartupPage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<QuizPage>();
        services.AddTransient(sp => PageLoadExtensions.CreateWithTiming<ProfileSetupPage, ProfileSetupViewModel>(sp, vm => new ProfileSetupPage(vm)));
        services.AddSingleton(sp => PageLoadExtensions.CreateWithTiming<DashboardPage, DashboardViewModel>(sp, vm => new DashboardPage(vm)));
        services.AddTransient<DailyCheckInPage>();
        services.AddSingleton(sp => PageLoadExtensions.CreateWithTiming<CyclePage, CycleViewModel>(sp, vm => new CyclePage(vm)));
        services.AddTransient<CyclePhaseDetailPage>();
        services.AddSingleton(sp => PageLoadExtensions.CreateWithTiming<NutritionPage, NutritionViewModel>(sp, vm => new NutritionPage(vm)));
        services.AddSingleton(sp => PageLoadExtensions.CreateWithTiming<WorkoutPage, WorkoutViewModel>(sp, vm => new WorkoutPage(vm)));
        services.AddSingleton(sp => PageLoadExtensions.CreateWithTiming<ProfilePage, ProfileViewModel>(sp, vm => new ProfilePage(vm)));
        services.AddTransient<ProfilePersonalInfoPage>();
        services.AddTransient<ProfileNutritionSettingsPage>();
        services.AddTransient<ProfileWorkoutPreferencesPage>();
        services.AddTransient<ProfileHealthSyncPage>();
        services.AddTransient<ProfileUnitsDisplayPage>();
        services.AddTransient<ProfileFeedbackPage>();
        services.AddTransient<ProfilePrivacyPage>();
        services.AddTransient<InjuryLogPage>();

        services.AddSingleton<AppShell>();
    }

    private static void ConfigureLogging(MauiAppBuilder builder)
    {
#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Warning);
#endif
    }

    private static Task NavigateToAsync(string route)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var shell = Shell.Current ?? throw new InvalidOperationException("Shell is not available.");
            await shell.GoToAsync(route, false);
        });
    }

    private static async Task PreloadAndNavigateToDashboardAsync(IServiceProvider services)
    {
        var preloadService = services.GetRequiredService<IAppPreloadService>();
        await preloadService.PreloadDashboardAsync();
        await NavigateAuthenticatedAsync(services, "//DashboardPage");
        _ = preloadService.PreloadRemainingAsync();
    }

    private static async Task PrepareAndNavigateToDashboardAsync(IServiceProvider services)
    {
        await NavigateAuthenticatedAsync(services, "//DashboardPage");
    }

    private static Task NavigateAuthenticatedAsync(IServiceProvider services, string route)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var shell = services.GetRequiredService<AppShell>();
            shell.MarkAuthenticationVerified();
            await shell.GoToAsync(route, false);
        });
    }
}
