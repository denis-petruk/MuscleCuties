using System.Diagnostics;
using CommunityToolkit.Maui;
using MauiIcons.Fluent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using SkiaSharp.Views.Maui.Controls.Hosting;
using MuscleCuties.App.Infrastructure.Network;
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
using MuscleCuties.App.Services.Navigation;
using MuscleCuties.App.Services.Notifications;
using MuscleCuties.App.Services.Profile;
using MuscleCuties.App.Services.Security;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.ExerciseMedia;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Core.Services.Progress;
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
        SQLitePCL.Batteries_V2.Init();

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .UseFluentMauiIcons()
            .ConfigureMauiHandlers(ConfigureInputHandlers)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "Inter");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
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

            var textContentType = GetAuthFieldKind(handler.VirtualView.AutomationId) switch
            {
                AuthFieldKind.Email => UIKit.UITextContentType.Username,
                AuthFieldKind.CurrentPassword => UIKit.UITextContentType.Password,
                AuthFieldKind.NewPassword => UIKit.UITextContentType.NewPassword,
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

    private enum AuthFieldKind
    {
        None,
        Email,
        CurrentPassword,
        NewPassword
    }

    private static AuthFieldKind GetAuthFieldKind(string? automationId) =>
        automationId switch
        {
            "LoginEmail" or "RegisterEmail" => AuthFieldKind.Email,
            "LoginPassword" or "ProfileCurrentPassword" => AuthFieldKind.CurrentPassword,
            "RegisterPassword" or "RegisterConfirmPassword" or "ProfileNewPassword" or "ProfileConfirmPassword" => AuthFieldKind.NewPassword,
            _ => AuthFieldKind.None
        };

#if ANDROID
    private static void ClearAndroidInputChrome(Android.Views.View view)
    {
        view.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        view.SetBackgroundColor(Android.Graphics.Color.Transparent);
        view.SetPadding(0, 0, 0, 0);
    }

    private static void ConfigureAndroidAutofill(Android.Widget.EditText editText, string? automationId)
    {
        var hint = GetAuthFieldKind(automationId) switch
        {
            AuthFieldKind.Email => Android.Views.View.AutofillHintEmailAddress,
            AuthFieldKind.CurrentPassword => Android.Views.View.AutofillHintPassword,
            AuthFieldKind.NewPassword => "newPassword",
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
        services.AddSingleton<INavigationContextService, NavigationContextService>();
        services.AddSingleton<IAppDatabaseKeyProvider, AppDatabaseKeyProvider>();
        // One pooled context per operation scope. Singleton view models must inject
        // IServiceScopeFactory and resolve database services inside that scope.
        services.AddPooledDbContextFactory<AppDatabase>(ConfigureDatabaseOptions);
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDatabase>>().CreateDbContext());

        services.AddSingleton<ITokenStorage, SecureStorageService>();
        services.AddSingleton<ILocalNotificationService, LocalNotificationService>();
#if IOS || MACCATALYST
        services.AddSingleton<AppleSignInService>();
        services.AddSingleton<IAppleSignInService>(sp => sp.GetRequiredService<AppleSignInService>());
        services.AddSingleton<IPlatformSignInService>(sp => sp.GetRequiredService<AppleSignInService>());
#endif

        // Native health import requires a separate product and privacy approval.
        // Keep the platform adapter dormant in this release.
        services.AddSingleton<IHealthSyncService, DisabledHealthSyncService>();
        services.AddScoped<ICyclePhaseNotificationService, CyclePhaseNotificationService>();
        services.AddScoped<IFeedbackEmailService, FeedbackEmailService>();
    }

    // Configures SQLCipher encryption using the hardware-backed key from IAppDatabaseKeyProvider with private cache pooling.
    private static void ConfigureDatabaseOptions(IServiceProvider sp, DbContextOptionsBuilder opts)
    {
        var path = sp.GetRequiredService<IDbPathProvider>().GetDatabasePath();
        var password = sp.GetRequiredService<IAppDatabaseKeyProvider>().GetDatabasePassword();
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = true,
            Password = password
        }.ToString();

        opts.UseSqlite(connectionString);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPhotoVaultRepository, PhotoVaultRepository>();
        services.AddScoped<ICycleRepository, CycleRepository>();
        services.AddScoped<INutritionRepository, NutritionRepository>();
        services.AddScoped<IWorkoutRepository, WorkoutRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<IFoodSyncRepository, FoodSyncRepository>();
    }

    private static void RegisterDomainServices(IServiceCollection services)
    {
        services.AddSingleton<IFdcApiKeyProvider, SecureStorageFdcApiKeyProvider>();
        services.AddTransient<FdcAirGapHandler>();
        services.AddHttpClient<IFdcApiClient, Infrastructure.Network.FdcApiClient>(client =>
        {
            client.BaseAddress = Infrastructure.Network.FdcApiClient.BaseUri;
            client.Timeout = TimeSpan.FromSeconds(8);
        })
        .ConfigurePrimaryHttpMessageHandler(CreateFdcHttpHandler)
        .AddHttpMessageHandler<FdcAirGapHandler>();
        services.AddScoped<IFoodSyncService, FoodSyncService>();
        services.AddHttpClient<IWgerApiClient, Infrastructure.Network.WgerApiClient>(client =>
        {
            client.BaseAddress = Infrastructure.Network.WgerApiClient.BaseUri;
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient<IExerciseDbClient, Infrastructure.Network.ExerciseDbClient>(client =>
        {
            client.BaseAddress = Infrastructure.Network.ExerciseDbClient.BaseUri;
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<IExerciseMediaStore>(_ =>
            new ExerciseMediaStore(Path.Combine(FileSystem.AppDataDirectory, "exercises")));
        services.AddScoped<IExerciseMediaSyncService, ExerciseMediaSyncService>();
        services.AddScoped<ICalorieCalculator, CalorieCalculator>();
        services.AddScoped<ICalorieDistributionService, CalorieDistributionService>();
        services.AddScoped<IFallbackMealProvider, FallbackMealProvider>();
        services.AddScoped<INutritionPlanner, NutritionPlanner>();
        services.AddScoped<ICyclePhaseCalculator, CyclePhaseCalculator>();
        services.AddScoped<ICyclePredictionPlanner, CyclePredictionPlanner>();
        services.AddScoped<IDashboardPlanner, DashboardPlanner>();
        services.AddScoped<IWorkoutPlanner, WorkoutPlanner>();
        services.AddScoped<IWorkoutPlanGenerator, WorkoutPlanGenerator>();
        services.AddScoped<IWorkoutScheduleService, WorkoutScheduleService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
#if DEBUG
        services.AddSingleton<IEmailReceiptVerifier, Services.Auth.DevelopmentEmailReceiptVerifier>();
#else
        services.AddSingleton<IEmailReceiptVerifier>(_ => new SignedEmailReceiptVerifier(
            Services.Auth.EmailChallengeBuildConfiguration.ReceiptPublicKeys,
            Services.Auth.EmailChallengeBuildConfiguration.ReceiptIssuer,
            Services.Auth.EmailChallengeBuildConfiguration.ReceiptAudience));
#endif
        services.AddHttpClient<IEmailChallengeService, Services.Auth.EmailChallengeService>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
        services.AddScoped<IOnboardingCommitService, OnboardingCommitService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IPendingRegistrationService, PendingRegistrationService>();
        services.AddScoped<ICycleService, CycleService>();
        services.AddScoped<ICycleAccountService, CycleAccountService>();
        services.AddScoped<IDashboardCalendarDataService, DashboardCalendarDataService>();
        services.AddScoped<ISuggestedMealService, SuggestedMealService>();
        services.AddScoped<IFoodLookupService, FoodLookupService>();
        services.AddScoped<INutritionService, NutritionService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IWorkoutService, WorkoutService>();
        services.AddScoped<IProfileSettingsService, ProfileSettingsService>();
        services.AddScoped<IProfileAccountService, ProfileAccountService>();
        services.AddScoped<IPhotoVaultService, PhotoVaultService>();
        services.AddSingleton<IAppAccountPurgeService, AppAccountPurgeService>();
        services.AddScoped<IProgressSummaryService, ProgressSummaryService>();
        services.AddSingleton<INutritionChangeNotifier>(_ => new NutritionChangeNotifier(MainThread.BeginInvokeOnMainThread));
        services.AddScoped<NutritionTrackingService>();
        services.AddSingleton<INutritionTrackingService, ScopedNutritionTrackingService>();
        services.AddScoped<IWorkoutArchiveService, WorkoutArchiveService>();
        services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();
    }

    private static void RegisterWorkoutPlanningServices(IServiceCollection services)
    {
        services.AddSingleton(WorkoutPlanningConfig.CreateDefault());

        services.AddSingleton<IReadinessEngine, ReadinessEngine>();
        services.AddSingleton<IGatingEngine, GatingEngine>();

        services.AddScoped<IReadinessRepository, ReadinessRepository>();
        services.AddScoped<IDailyCheckInDataService, DailyCheckInDataService>();
        services.AddScoped<IReadinessService>(sp => (DailyCheckInDataService)sp.GetRequiredService<IDailyCheckInDataService>());
        services.AddScoped<IWorkoutInjuryRepository, WorkoutInjuryRepository>();
        services.AddScoped<IWorkoutInjuryService, WorkoutInjuryService>();
        services.AddScoped<IReadinessSyncOrchestrator, ReadinessSyncOrchestrator>();
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
            () => NavigateThenPreloadDashboardAsync(sp),
            () => NavigateAuthenticatedAsync(sp, $"//{nameof(ProfileSetupPage)}"),
            () => NavigateToAsync(nameof(RegisterPage)),
            sp.GetService<IPlatformSignInService>(),
            () => NavigateToAsync(nameof(PasswordRecoveryPage))));

        services.AddTransient<PasswordRecoveryViewModel>(sp => new PasswordRecoveryViewModel(
            sp.GetRequiredService<IPasswordRecoveryService>(),
            () => NavigateToAsync(".."),
            email => NavigateToExistingLoginAsync(sp, email)));

        services.AddTransient<OtpVerificationViewModel>(sp => new OtpVerificationViewModel(
            sp.GetRequiredService<IPendingRegistrationService>(),
            sp.GetRequiredService<IEmailChallengeService>(),
            () => NavigateAuthenticatedAsync(sp, $"//{nameof(ProfileSetupPage)}"),
            () => NavigateToAsync("..")));

        services.AddTransient<RegisterViewModel>(sp => new RegisterViewModel(
            sp.GetRequiredService<IAuthService>(),
            () => NavigateAuthenticatedAsync(sp, $"//{nameof(ProfileSetupPage)}"),
            () => NavigateToAsync(".."),
            sp.GetService<IPlatformSignInService>(),
            () => NavigateThenPreloadDashboardAsync(sp),
            email => NavigateToExistingLoginAsync(sp, email),
            sp.GetRequiredService<IPendingRegistrationService>(),
            () => NavigateToAsync(nameof(OtpVerificationPage)),
            sp.GetRequiredService<IEmailChallengeService>()));

        services.AddSingleton<QuizQuestionCache>();

        services.AddTransient<QuizViewModel>(sp => new QuizViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IAppPreloadService>(),
            sp.GetRequiredService<QuizQuestionCache>(),
            () => NavigateThenPreloadDashboardAsync(sp),
            sp.GetRequiredService<IPendingRegistrationService>(),
            () => NavigateToAsync("//LoginPage")));

        services.AddTransient<ProfileSetupViewModel>(sp => new ProfileSetupViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IProfileAccountService>(),
            sp.GetRequiredService<IQuizService>(),
            sp.GetRequiredService<QuizQuestionCache>(),
            sp.GetRequiredService<IHealthSyncService>(),
            () => NavigateAuthenticatedAsync(sp, $"//{nameof(QuizPage)}"),
            sp.GetRequiredService<IPendingRegistrationService>()));

        services.AddSingleton<DashboardViewModel>(sp => new DashboardViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            () => NavigateToAsync("//CyclePage"),
            () => NavigateToAsync("//WorkoutPage"),
            workoutDayId => NavigateWithContextAsync(sp, nameof(WorkoutSessionPage), "workoutDayId", workoutDayId),
            () => NavigateToAsync("//NutritionPage"),
            () => NavigateToAsync(nameof(DailyCheckInPage)),
            sp.GetService<INutritionChangeNotifier>(),
            () => NavigateToAsync(nameof(PhotoJourneyVaultPage))));

        services.AddTransient<DailyCheckInViewModel>(sp => new DailyCheckInViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IHealthReadinessBridge>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<IReadinessEngine>(),
            sp.GetRequiredService<IWorkoutService>(),
            sp.GetRequiredService<IAppPreloadService>(),
            () => NavigateToAsync("//DashboardPage")));

        services.AddSingleton<CycleViewModel>(sp => new CycleViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            phase => NavigateWithContextAsync(sp, nameof(CyclePhaseDetailPage), "phase", phase),
            () => DateTime.Today,
            () => NavigateToAsync(nameof(ProfilePersonalInfoPage))));

        services.AddTransient<CyclePhaseDetailViewModel>(sp => new CyclePhaseDetailViewModel(() => NavigateToAsync("..")));

        services.AddSingleton<NutritionViewModel>(sp => new NutritionViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IReferenceDataPreparationService>(),
            route => NavigateToAsync(route),
            sp.GetService<INutritionChangeNotifier>()));

        services.AddSingleton<WorkoutViewModel>(sp => new WorkoutViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            route => NavigateToAsync(route),
            workoutDayId => NavigateWithContextAsync(sp, nameof(WorkoutSessionPage), "workoutDayId", workoutDayId)));

        services.AddTransient<WorkoutSessionViewModel>(sp => new WorkoutSessionViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            route => NavigateToAsync(route)));

        services.AddTransient<FoodLogViewModel>(sp => new FoodLogViewModel(
            sp.GetRequiredService<INutritionTrackingService>(),
            () => NavigateToAsync(".."),
            sp.GetRequiredService<IFoodLookupService>(),
            sp.GetService<INutritionChangeNotifier>()));

        services.AddTransient<WorkoutHistoryViewModel>(sp => new WorkoutHistoryViewModel(
            sp.GetRequiredService<IWorkoutArchiveService>(),
            () => NavigateToAsync("..")));

        services.AddSingleton<ProfileViewModel>(sp => new ProfileViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new Lazy<IAppPreloadService>(sp.GetRequiredService<IAppPreloadService>),
            () => NavigateToAsync("//LoginPage"),
            NavigateToAsync,
            sp.GetRequiredService<IAppAccountPurgeService>()));

        services.AddTransient<PhotoJourneyVaultViewModel>(sp => new PhotoJourneyVaultViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            () => NavigateToAsync("..")));

        services.AddTransient<ProfilePersonalInfoViewModel>(sp => new ProfilePersonalInfoViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IProfileAccountService>(),
            sp.GetRequiredService<IAppPreloadService>(),
            () => NavigateToAsync("//ProfilePage")));

        services.AddTransient<ProfileNutritionSettingsViewModel>(sp => new ProfileNutritionSettingsViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<INutritionPlanner>(),
            sp.GetRequiredService<IAppPreloadService>(),
            () => NavigateToAsync("//ProfilePage"),
            () => NavigateToAsync("//NutritionPage")));

        services.AddTransient<ProfileWorkoutPreferencesViewModel>(sp => new ProfileWorkoutPreferencesViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ICycleService>(),
            sp.GetRequiredService<IWorkoutService>(),
            sp.GetRequiredService<IAppPreloadService>(),
            () => NavigateToAsync("//ProfilePage"),
            () => NavigateToAsync("//WorkoutPage")));

        services.AddTransient<ProfileHealthSyncViewModel>(sp => new ProfileHealthSyncViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IReadinessSyncOrchestrator>(),
            sp.GetRequiredService<IAppPreloadService>(),
            () => NavigateToAsync("..")));

        services.AddTransient<ProfileUnitsDisplayViewModel>(sp => new ProfileUnitsDisplayViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IProfileAccountService>(),
            () => NavigateToAsync("//ProfilePage")));

        services.AddTransient<ProfileFeedbackViewModel>(sp => new ProfileFeedbackViewModel(
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<IProfileAccountService>(),
            sp.GetRequiredService<IFeedbackEmailService>(),
            () => NavigateToAsync("//ProfilePage")));

        services.AddTransient<InjuryLogViewModel>(sp => new InjuryLogViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            () => NavigateToAsync("..")));

        services.AddSingleton<IReferenceDataPreparationService, ReferenceDataPreparationService>();
        services.AddSingleton<IAppPreloadService, AppPreloadService>();
    }

    private static void RegisterPages(IServiceCollection services)
    {
        services.AddTransient<WorkoutSessionPage>();
        services.AddTransient<LoginPage>();
        services.AddTransient<AppStartupPage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<OtpVerificationPage>();
        services.AddTransient<QuizPage>();
        services.AddTransient(sp => PageLoadExtensions.CreateWithTiming<ProfileSetupPage, ProfileSetupViewModel>(sp, vm => new ProfileSetupPage(vm)));
        services.AddSingleton(sp => PageLoadExtensions.CreateWithTiming<DashboardPage, DashboardViewModel>(sp, vm => new DashboardPage(vm)));
        services.AddTransient<DailyCheckInPage>();
        services.AddSingleton(sp => PageLoadExtensions.CreateWithTiming<CyclePage, CycleViewModel>(sp, vm => new CyclePage(vm)));
        services.AddTransient<CyclePhaseDetailPage>();
        services.AddSingleton(sp => PageLoadExtensions.CreateWithTiming<NutritionPage, NutritionViewModel>(
            sp,
            vm => new NutritionPage(vm)));
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
        services.AddTransient<FoodLogPage>();
        services.AddTransient<WorkoutHistoryPage>();
        services.AddTransient<PhotoJourneyVaultPage>();
        services.AddTransient<PasswordRecoveryPage>();

        services.AddSingleton<AppShell>();
    }

    private static void ConfigureLogging(MauiAppBuilder builder)
    {
#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Warning);
#endif
    }

    // Enforces TLS 1.3 and SPKI certificate pinning for USDA FoodData Central requests with cookies disabled.
    private static HttpMessageHandler CreateFdcHttpHandler()
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            SslOptions =
            {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls13,
                RemoteCertificateValidationCallback = FdcCertificatePinningPolicy.Validate
            },
            UseCookies = false
        };
    }

    private static Task NavigateToAsync(string route)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var shell = Shell.Current ?? throw new InvalidOperationException("Shell is not available.");
            await shell.GoToAsync(RemoveQueryString(route), false);
        });
    }

    private static Task NavigateWithContextAsync<T>(
        IServiceProvider services,
        string route,
        string key,
        T value)
    {
        services.GetRequiredService<INavigationContextService>().Set(key, value);
        return NavigateToAsync(route);
    }

    private static async Task NavigateThenPreloadDashboardAsync(IServiceProvider services)
    {
        await NavigateAuthenticatedAsync(services, "//DashboardPage");

        if (Shell.Current?.CurrentPage is DashboardPage dashboardPage)
            _ = PreloadAfterDashboardLoadAsync(
                dashboardPage,
                services.GetRequiredService<IAppPreloadService>());
    }

    private static Task NavigateToExistingLoginAsync(IServiceProvider services, string email)
    {
        services.GetRequiredService<INavigationContextService>()
            .Set(LoginPage.ExistingEmailContextKey, email);
        return NavigateToAsync("..");
    }

    private static async Task PreloadAfterDashboardLoadAsync(
        DashboardPage dashboardPage,
        IAppPreloadService preloadService)
    {
        try
        {
            await dashboardPage.InitialLoadCompleted;
            await preloadService.PreloadRemainingAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Preload] Hidden-tab preparation failed ({ex.GetType().Name}).");
        }
    }

    private static Task NavigateAuthenticatedAsync(IServiceProvider services, string route)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var shell = services.GetRequiredService<AppShell>();
            shell.MarkAuthenticationVerified();
            await shell.GoToAsync(RemoveQueryString(route), false);
        });
    }

    private static string RemoveQueryString(string route)
    {
        var queryIndex = route.IndexOf('?', StringComparison.Ordinal);
        return queryIndex < 0 ? route : route[..queryIndex];
    }
}
