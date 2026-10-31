using System.Diagnostics;
using System.Security.Authentication;
using CommunityToolkit.Maui;
using MauiIcons.Fluent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
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
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Infrastructure.Account.Otp;
using MuscleCuties.Infrastructure.Account.Recovery;
using MuscleCuties.Infrastructure.Account.Registration;
using MuscleCuties.Infrastructure.Account.SignIn;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Caching;
using MuscleCuties.UseCases.Features.Caching;
using MuscleCuties.Infrastructure.Cycle.Overview;
using MuscleCuties.Infrastructure.Dashboard.CheckIn;
using MuscleCuties.Infrastructure.Dashboard.Overview;
using MuscleCuties.Infrastructure.Nutrition.DailyLog;
using MuscleCuties.Infrastructure.Nutrition.Overview;
using MuscleCuties.Infrastructure.Profile.HealthSync;
using MuscleCuties.Infrastructure.Progress;
using MuscleCuties.Infrastructure.Workout.Media;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UI.Infrastructure.Network;
using MuscleCuties.UI.Pages;
using MuscleCuties.UI.Pages.Auth;
using MuscleCuties.UI.Pages.Cycle;
using MuscleCuties.UI.Pages.Dashboard;
using MuscleCuties.UI.Pages.Nutrition;
using MuscleCuties.UI.Pages.Onboarding;
using MuscleCuties.UI.Pages.Profile;
using MuscleCuties.UI.Pages.Startup;
using MuscleCuties.UI.Pages.Workout;
using MuscleCuties.UI.Services;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Navigation;
using MuscleCuties.UI.Services.Notifications;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Security;
using MuscleCuties.UI.ViewModels.Auth;
using MuscleCuties.UI.ViewModels.Cycle;
using MuscleCuties.UI.ViewModels.Dashboard;
using MuscleCuties.UI.ViewModels.Nutrition;
using MuscleCuties.UI.ViewModels.Profile;
using MuscleCuties.UI.ViewModels.Quiz;
using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UseCases.Features.Account.Otp;
using MuscleCuties.UseCases.Features.Account.Otp.Contracts;
using MuscleCuties.UseCases.Features.Account.Recovery;
using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Account.SignIn;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;
using MuscleCuties.UseCases.Features.Cycle.DailyLog;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Cycle.PhaseEdit;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Dashboard.Overview;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;
using MuscleCuties.Infrastructure.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;
using MuscleCuties.UseCases.Features.Profile.Feedback;
using MuscleCuties.UseCases.Features.Profile.HealthSync;
using MuscleCuties.UseCases.Features.Profile.NutritionSettings;
using MuscleCuties.UseCases.Features.Profile.Overview;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Progress;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Session;
using SkiaSharp.Views.Maui.Controls.Hosting;
#if IOS || MACCATALYST
using UIKit;
#endif
using INutritionChangeNotifier = MuscleCuties.Core.Services.Nutrition.INutritionChangeNotifier;

namespace MuscleCuties.UI;

public static class MauiProgram
{
    private const string ProductionBackendBaseUrl = "https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com";
#if DEBUG
    private const string DebugBackendBaseUrlPreferenceKey = "backend_api_base_url";
#endif
    private const int BackendRequestTimeoutSeconds = 15;

    public static MauiApp CreateMauiApp()
    {
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
        RegisterDomainServices(services);
        RegisterWorkoutPlanningServices(services);
        RegisterMigrationServices(services);
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
            handler.PlatformView.BorderStyle = UITextBorderStyle.None;
            handler.PlatformView.BackgroundColor = UIColor.Clear;

            var textContentType = GetAuthFieldKind(handler.VirtualView.AutomationId) switch
            {
                AuthFieldKind.LoginIdentifier or AuthFieldKind.AccountUsername => UITextContentType.Username,
                AuthFieldKind.EmailAddress => UITextContentType.EmailAddress,
                AuthFieldKind.CurrentPassword => UITextContentType.Password,
                AuthFieldKind.NewPassword => UITextContentType.NewPassword,
                AuthFieldKind.OneTimeCode => UITextContentType.OneTimeCode,
                _ => null
            };

            if (textContentType is not null)
                handler.PlatformView.TextContentType = textContentType;

            if (handler.VirtualView.AutomationId is "RegisterPassword")
                handler.PlatformView.PasswordRules = UITextInputPasswordRules.Create(NewPasswordRules);
        });
        PickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
        {
            handler.PlatformView.BorderStyle = UITextBorderStyle.None;
            handler.PlatformView.BackgroundColor = UIColor.Clear;
        });
        DatePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
            handler.PlatformView.BackgroundColor = UIColor.Clear);
        TimePickerHandler.Mapper.AppendToMapping("MuscleCutiesInputChrome", (handler, _) =>
            handler.PlatformView.BackgroundColor = UIColor.Clear);
#endif
    }

    private enum AuthFieldKind
    {
        None,
        LoginIdentifier,
        EmailAddress,
        AccountUsername,
        CurrentPassword,
        NewPassword,
        OneTimeCode
    }

    // Matches AuthInputValidator's minimum length and character-class requirements.
    private const string NewPasswordRules =
        "required: upper; required: lower; required: digit; required: [-().&@?'#,/+_*]; minlength: 8;";

    private static AuthFieldKind GetAuthFieldKind(string? automationId)
    {
        return automationId switch
        {
            "LoginEmail" => AuthFieldKind.LoginIdentifier,
            "RegisterEmail" => AuthFieldKind.EmailAddress,
            "RegisterUsername" => AuthFieldKind.AccountUsername,
            "OtpEntry" => AuthFieldKind.OneTimeCode,
            "LoginPassword" or "ProfileCurrentPassword" => AuthFieldKind.CurrentPassword,
            "RegisterPassword" or "RegisterConfirmPassword" or "ProfileNewPassword" or "ProfileConfirmPassword" =>
                AuthFieldKind.NewPassword,
            _ => AuthFieldKind.None
        };
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
        var hint = GetAuthFieldKind(automationId) switch
        {
            AuthFieldKind.LoginIdentifier or AuthFieldKind.EmailAddress => Android.Views.View.AutofillHintEmailAddress,
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
        services.AddSingleton<INavigationContextService, NavigationContextService>();
        services.AddSingleton<ITokenStorage, SecureStorageService>();
        services.AddSingleton(AccountSessionGate.Shared);
        services.AddSingleton<ILocalNotificationService, LocalNotificationService>();
#if IOS || MACCATALYST
        services.AddSingleton<AppleSignInService>();
        services.AddSingleton<IAppleSignInService>(sp => sp.GetRequiredService<AppleSignInService>());
        services.AddSingleton<IPlatformSignInService>(sp => sp.GetRequiredService<AppleSignInService>());
#endif

#if IOS
        services.AddSingleton<IHealthDataProvider, Platforms.iOS.AppleHealthDataProvider>();
        services.AddSingleton<IHealthSyncService, HealthSyncService>();
#else
        services.AddSingleton<IHealthSyncService, DisabledHealthSyncService>();
#endif
        services.AddScoped<ICyclePhaseNotificationService>(sp =>
            new CyclePhaseNotificationService(
                sp.GetRequiredService<ICycleOverviewUseCase>(),
                sp.GetRequiredService<ILocalNotificationService>()));
        services.AddScoped<IFeedbackEmailService, FeedbackEmailService>();
    }

    private static void RegisterDomainServices(IServiceCollection services)
    {
        services.AddSingleton<IFdcApiKeyProvider, SecureStorageFdcApiKeyProvider>();
        services.AddTransient<FdcAirGapHandler>();
        services.AddHttpClient<IFdcApiClient, FdcApiClient>(client =>
            {
                client.BaseAddress = FdcApiClient.BaseUri;
                client.Timeout = TimeSpan.FromSeconds(8);
            })
            .ConfigurePrimaryHttpMessageHandler(CreateFdcHttpHandler)
            .AddHttpMessageHandler<FdcAirGapHandler>();
        services.AddScoped<IFoodSyncService, FoodSyncService>();
        services.AddHttpClient<IWgerApiClient, WgerApiClient>(client =>
        {
            client.BaseAddress = WgerApiClient.BaseUri;
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient<IExerciseDbClient, ExerciseDbClient>(client =>
        {
            client.BaseAddress = ExerciseDbClient.BaseUri;
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<IExerciseMediaStore>(_ =>
            new LocalExerciseMediaStore(Path.Combine(FileSystem.AppDataDirectory, "exercises")));
        services.AddScoped<ICalorieCalculator, CalorieCalculator>();
        services.AddScoped<ICalorieDistributionService, CalorieDistributionService>();
        services.AddScoped<IFallbackMealProvider, FallbackMealProvider>();
        services.AddScoped<INutritionPlanner, NutritionPlanner>();
        services.AddScoped<ICyclePhaseCalculator, CyclePhaseCalculator>();
        services.AddScoped<ICyclePredictionPlanner, CyclePredictionPlanner>();
        services.AddScoped<IDashboardPlanner, DashboardPlanner>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, CloudAuthService>();
        services.AddSingleton<IPendingRegistrationService, PendingRegistrationService>();
        services.AddSingleton<IAppAccountPurgeService>(sp => new AppAccountPurgeService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new Lazy<IAppPreloadService>(sp.GetRequiredService<IAppPreloadService>),
            sp.GetRequiredService<AccountSessionGate>(),
            sp.GetRequiredService<ILocalPhotoCacheService>()));
        services.AddSingleton<IProtectedPhotoCacheKeyStore, SecureStoragePhotoCacheKeyStore>();
        services.AddSingleton<ILocalPhotoCacheService>(sp => new EncryptedLocalPhotoCacheService(
            FileSystem.AppDataDirectory,
            sp.GetRequiredService<IProtectedPhotoCacheKeyStore>(),
            ProfileMediaProtection.Apply));
        services.AddSingleton<ISnapshotCacheKeyStore, SecureStorageSnapshotCacheKeyStore>();
        services.AddSingleton<IBackendSessionTokenStore, SecureStorageBackendSessionTokenStore>();
        services.AddSingleton<ILocalCacheService>(sp => new EncryptedLocalSnapshotCacheService(
            FileSystem.AppDataDirectory,
            sp.GetRequiredService<ISnapshotCacheKeyStore>(),
            ProfileMediaProtection.Apply));
        services.AddSingleton<ICloudAuthorityGate>(_ => new VersionedCloudAuthorityGate());
        services.AddHttpClient<IMuscleCutiesBackendApiClient, MuscleCutiesBackendApiClient>(client =>
        {
            var configuredUrl = ProductionBackendBaseUrl;
#if DEBUG
            configuredUrl = Preferences.Get(DebugBackendBaseUrlPreferenceKey, ProductionBackendBaseUrl);
#endif
            client.BaseAddress = new Uri(configuredUrl);
            client.Timeout = TimeSpan.FromSeconds(BackendRequestTimeoutSeconds);
        });
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<MobileJourneyCloudGateway>();
        services.AddSingleton<IPhotoCacheAccountLifecycle>(sp => new PhotoCacheAccountLifecycle(
            async () =>
            {
                await using var scope = sp.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<IAuthService>()
                    .GetCurrentUserIdAsync().ConfigureAwait(false);
            },
            sp.GetRequiredService<ILocalPhotoCacheService>()));
        services.AddScoped<LocalProgressSummaryStore>(sp => new LocalProgressSummaryStore(
            sp.GetRequiredService<IAuthService>(),
            db: null,
            sp.GetRequiredService<IMuscleCutiesBackendApiClient>(),
            sp.GetRequiredService<MobileJourneyCloudGateway>()));
        services.AddSingleton<IProgressSummaryStore,
            ScopedProgressSummaryStore>();
        services.AddSingleton<IProgressSummaryUseCase,
            ProgressSummaryUseCase>();
        services.AddScoped<IDailyCheckInStore, CloudDailyCheckInStore>();
        services.AddSingleton<IDailyCheckInUseCase,
            DailyCheckInUseCase>();
        services.AddSingleton<IDashboardOverviewStore,
            CloudDashboardOverviewStore>();
        services.AddSingleton<IDashboardOverviewUseCase,
            DashboardOverviewUseCase>();
        services.AddSingleton<INutritionChangeNotifier>(_ =>
            new NutritionChangeNotifier(MainThread.BeginInvokeOnMainThread));
        services.AddSingleton<IBarcodeScannerHardwareService, BarcodeScannerHardwareService>();
    }

    private static void RegisterWorkoutPlanningServices(IServiceCollection services)
    {
        services.AddSingleton(WorkoutPlanningConfig.CreateDefault());

        services.AddSingleton<IReadinessEngine, DailyReadinessEngine>();
        services.AddSingleton<IGatingEngine, GatingEngine>();

        services.AddScoped<IDailyCheckInNotificationService>(sp =>
            new DailyCheckInNotificationService(
                sp.GetRequiredService<ILocalNotificationService>(),
                sp.GetRequiredService<IDailyCheckInStore>()));
    }

    private static void RegisterMigrationServices(IServiceCollection services)
    {
        services.AddSingleton<IAccountLoginScheduler, AccountLoginScheduler>();
        services.AddSingleton<IAccountLookupScheduler, AccountLookupScheduler>();
        services.AddSingleton<IPlatformAccountSignInAdapter>(sp =>
            new PlatformAccountSignInAdapter(sp.GetService<IPlatformSignInService>()));
        services.AddSingleton<IAccountPasswordValidator, AccountPasswordValidator>();
        services.AddSingleton<IAccountSignInStore, CloudAccountSignInStore>();
        services.AddSingleton<IAccountSignInUseCase, AccountSignInUseCase>();
        services.AddSingleton<IAccountRecoveryStore, CloudAccountRecoveryStore>();
        services.AddSingleton<IAccountRecoveryUseCase, AccountRecoveryUseCase>();
        services.AddSingleton<IAccountRegistrationGateway, CloudAccountRegistrationGateway>();
        services.AddSingleton<IAccountRegistrationUseCase, AccountRegistrationUseCase>();
        services.AddSingleton<IRegistrationOtpGateway, CloudRegistrationOtpGateway>();
        services.AddSingleton<IRegistrationOtpUseCase, RegistrationOtpUseCase>();

        services.AddSingleton<IProfileRefreshNotifier>(sp =>
            new ProfileRefreshNotifier(new Lazy<IAppPreloadService>(sp.GetRequiredService<IAppPreloadService>)));
        services.AddScoped<ICurrentProfileUserIdProvider, CurrentProfileUserIdProvider>();
        services.AddSingleton<IProfileHealthConnection, ProfileHealthConnection>();
        services.AddSingleton<CloudProfileSetupStore>();
        services.AddSingleton<IProfileSetupPersistence>(sp => sp.GetRequiredService<CloudProfileSetupStore>());
        services.AddSingleton<IProfileSetupStore>(sp => sp.GetRequiredService<CloudProfileSetupStore>());
        services.AddSingleton<IProfileSetupUseCase, ProfileSetupUseCase>();
        services.AddSingleton<IOnboardingQuizStore, CloudOnboardingQuizStore>();
        services.AddSingleton<IOnboardingQuizUseCase, OnboardingQuizUseCase>();
        services.AddSingleton<IProfileAccountSession, CloudProfileAccountSession>();
        services.AddSingleton<IProfileAccountDeletion, LocalProfileAccountDeletion>();
        services.AddSingleton<IProfilePurgePort, ProfilePurgeCoordinator>();
        services.AddSingleton<IProfileOverviewStore, CloudProfileOverviewStore>();
        services.AddSingleton<IProfileOverviewUseCase, ProfileOverviewUseCase>();
        services.AddSingleton<IProfilePersonalInfoStore, CloudProfilePersonalInfoStore>();
        services.AddSingleton<IProfilePersonalInfoUseCase, ProfilePersonalInfoUseCase>();
        services.AddSingleton<IProfileNutritionSettingsStore, CloudProfileNutritionSettingsStore>();
        services.AddSingleton<IProfileNutritionSettingsUseCase, ProfileNutritionSettingsUseCase>();
        services.AddSingleton<IProfileWorkoutPreferencesStore, CloudProfileWorkoutPreferencesStore>();
        services.AddSingleton<IProfileWorkoutPreferencesUseCase, ProfileWorkoutPreferencesUseCase>();
        services.AddSingleton<IProfileUnitsStore, CloudProfileUnitsStore>();
        services.AddSingleton<IProfileUnitsUseCase, ProfileUnitsUseCase>();
        services.AddSingleton<IProfileHealthSyncStore, LocalProfileHealthSyncStore>();
        services.AddSingleton<IProfileHealthSyncUseCase, ProfileHealthSyncUseCase>();
        services.AddHttpClient<IPhotoVaultStore, MobilePhotoVaultStore>();
        services.AddSingleton<IPhotoVaultUseCase, PhotoVaultUseCase>();
        services.AddSingleton<IProfileFeedbackComposer, LocalProfileFeedbackComposer>();
        services.AddSingleton<IProfileFeedbackUseCase, ProfileFeedbackUseCase>();

        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<ICycleOverviewStore, CloudCycleOverviewStore>();
        services.AddSingleton<ICycleOverviewUseCase, CycleOverviewUseCase>();
        services.AddSingleton<ICycleDailyLogStore, CloudCycleDailyLogStore>();
        services.AddSingleton<ICycleDailyLogUseCase, CycleDailyLogUseCase>();
        services.AddSingleton<ICyclePhaseEditStore, CloudCyclePhaseEditStore>();
        services.AddSingleton<ICyclePhaseEditUseCase, CyclePhaseEditUseCase>();
        services.AddSingleton<INutritionDailyLogStore, CloudNutritionDailyLogStore>();
        services.AddSingleton<INutritionDailyLogUseCase, NutritionDailyLogUseCase>();
        services.AddSingleton<UseCases.Features.Nutrition.DailyLog.INutritionChangeNotifier,
            NutritionChangeNotifierAdapter>();
        services.AddSingleton<INutritionOverviewStore, CloudNutritionOverviewStore>();
        services.AddSingleton<INutritionOverviewUseCase, NutritionOverviewUseCase>();
        services.AddSingleton<CloudFoodCatalogSearchStore>();
        services.AddSingleton<IFoodCatalogSearchStore>(sp => sp.GetRequiredService<CloudFoodCatalogSearchStore>());
        services.AddSingleton<IBarcodeFoodLookupStore>(sp => sp.GetRequiredService<CloudFoodCatalogSearchStore>());
        services.AddSingleton<IFoodCatalogSearchUseCase, FoodCatalogSearchUseCase>();
        services.AddSingleton<ILookupBarcodeUseCase, LookupBarcodeUseCase>();
        services.AddSingleton<IRecipeStore, CloudRecipeStore>();
        services.AddSingleton<ICreateRecipeUseCase, CreateRecipeUseCase>();
        services.AddSingleton<ISaveMealTemplateUseCase, SaveMealTemplateUseCase>();
        services.AddSingleton<IGetRecipesAndTemplatesUseCase, GetRecipesAndTemplatesUseCase>();
        services.AddSingleton<IWorkoutInjuryStore, CloudWorkoutInjuryStore>();
        services.AddSingleton<IWorkoutInjuryUseCase, WorkoutInjuryUseCase>();
        services.AddSingleton<IWorkoutOverviewStore, CloudWorkoutOverviewStore>();
        services.AddSingleton<IWorkoutOverviewUseCase, WorkoutOverviewUseCase>();
        services.AddScoped<IWorkoutSessionActionStore, CloudWorkoutSessionActionStore>();
        services.AddScoped<IWorkoutSessionActionUseCase, WorkoutSessionActionUseCase>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<LoginViewModel>(sp => new LoginViewModel(
            sp.GetRequiredService<IAccountSignInUseCase>(),
            sp.GetRequiredService<IAccountLoginScheduler>(),
            () => NavigateThenPreloadDashboardAsync(sp, preloadRemaining: false),
            () => NavigateAuthenticatedAsync($"//{nameof(ProfileSetupPage)}"),
            () => NavigateToAsync(nameof(RegisterPage)),
            sp.GetRequiredService<IPlatformAccountSignInAdapter>(),
            () => NavigateToAsync(nameof(PasswordRecoveryPage)),
            sp.GetRequiredService<IPhotoCacheAccountLifecycle>()));

        services.AddTransient<PasswordRecoveryViewModel>(sp => new PasswordRecoveryViewModel(
            sp.GetRequiredService<IAccountRecoveryUseCase>(),
            sp.GetRequiredService<IAccountPasswordValidator>(),
            () => NavigateToAsync(".."),
            email => _ = NavigateToExistingLoginAsync(sp, email)));

        services.AddTransient<OtpVerificationViewModel>(sp => new OtpVerificationViewModel(
            sp.GetRequiredService<IRegistrationOtpUseCase>(),
            () => NavigateAuthenticatedAsync($"//{nameof(ProfileSetupPage)}"),
            () => NavigateToAsync("..")));

        services.AddTransient<RegisterViewModel>(sp => new RegisterViewModel(
            sp.GetRequiredService<IAccountRegistrationUseCase>(),
            sp.GetRequiredService<IAccountSignInUseCase>(),
            sp.GetRequiredService<IPlatformAccountSignInAdapter>(),
            () => NavigateAuthenticatedAsync($"//{nameof(ProfileSetupPage)}"),
            () => NavigateToAsync(".."),
            () => NavigateThenPreloadDashboardAsync(sp, preloadRemaining: false),
            email => NavigateToExistingLoginAsync(sp, email),
            () => NavigateToAsync(nameof(OtpVerificationPage)),
            () => NavigateToAsync(nameof(ProfilePrivacyPage)),
            photoCacheAccountLifecycle: sp.GetRequiredService<IPhotoCacheAccountLifecycle>()));

        services.AddSingleton<QuizQuestionCache>();

        services.AddTransient<QuizViewModel>(sp => new QuizViewModel(
            sp.GetRequiredService<IOnboardingQuizUseCase>(),
            () => NavigateThenPreloadDashboardAsync(sp, preloadRemaining: false),
            () => NavigateToAsync("//LoginPage"),
            () => NavigateToAsync($"//{nameof(ProfileSetupPage)}")));

        services.AddTransient<ProfileSetupViewModel>(sp => new ProfileSetupViewModel(
            sp.GetRequiredService<IProfileSetupUseCase>(),
            sp.GetRequiredService<IProfileHealthConnection>(),
            () => NavigateAuthenticatedAsync($"//{nameof(QuizPage)}")));

        services.AddSingleton<DashboardViewModel>(sp => new DashboardViewModel(
            sp.GetRequiredService<IDashboardOverviewUseCase>(),
            sp.GetRequiredService<IWorkoutInjuryUseCase>(),
            () => NavigateToAsync("//CyclePage"),
            () => NavigateToAsync("//WorkoutPage"),
            workoutDayId => NavigateWithContextAsync(sp, nameof(WorkoutSessionPage), "workoutDayId", workoutDayId),
            () => NavigateToAsync("//NutritionPage"),
            () => NavigateToAsync(nameof(DailyCheckInPage)),
            sp.GetService<UseCases.Features.Nutrition.DailyLog.INutritionChangeNotifier>(),
            () => NavigateToAsync(nameof(PhotoJourneyVaultPage))));

        services.AddTransient<DailyCheckInViewModel>(sp => new DailyCheckInViewModel(
            sp.GetRequiredService<IDailyCheckInUseCase>(),
            () => sp.GetRequiredService<IAppPreloadService>().InvalidateDashboard(),
            () => NavigateToAsync("//DashboardPage")));

        services.AddSingleton<CycleViewModel>(sp => new CycleViewModel(
            sp.GetRequiredService<ICycleOverviewUseCase>(),
            sp.GetRequiredService<ICycleDailyLogUseCase>(),
            sp.GetRequiredService<ICyclePhaseEditUseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            phase => NavigateWithContextAsync(sp, nameof(CyclePhaseDetailPage), "phase", phase),
            () => DateTime.Today,
            () => NavigateToAsync(nameof(ProfilePersonalInfoPage))));

        services.AddTransient<CyclePhaseDetailViewModel>(sp =>
            new CyclePhaseDetailViewModel(() => NavigateToAsync("..")));

        services.AddSingleton<NutritionViewModel>(sp => new NutritionViewModel(
            sp.GetRequiredService<INutritionOverviewUseCase>(),
            sp.GetRequiredService<IFoodCatalogSearchUseCase>(),
            NavigateToAsync,
            sp.GetService<UseCases.Features.Nutrition.DailyLog.INutritionChangeNotifier>()));

        services.AddSingleton<WorkoutViewModel>(sp => new WorkoutViewModel(
            sp.GetRequiredService<IWorkoutOverviewUseCase>(),
            sp.GetRequiredService<IWorkoutInjuryUseCase>(),
            sp.GetRequiredService<IProfileWorkoutPreferencesUseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            NavigateToAsync,
            workoutDayId => NavigateWithContextAsync(sp, nameof(WorkoutSessionPage), "workoutDayId", workoutDayId)));

        services.AddTransient<WorkoutSessionViewModel>(sp => new WorkoutSessionViewModel(
            sp.GetRequiredService<IWorkoutSessionActionUseCase>(),
            sp.GetRequiredService<ICycleOverviewUseCase>(),
            route => NavigateToAsync(route)));

        services.AddTransient<FoodLogViewModel>(sp => new FoodLogViewModel(
            sp.GetRequiredService<INutritionDailyLogUseCase>(),
            () => NavigateToAsync(".."),
            sp.GetService<UseCases.Features.Nutrition.DailyLog.INutritionChangeNotifier>(),
            sp.GetRequiredService<ILookupBarcodeUseCase>(),
            sp.GetRequiredService<IBarcodeScannerHardwareService>(),
            sp.GetRequiredService<ISaveMealTemplateUseCase>(),
            sp.GetRequiredService<IGetRecipesAndTemplatesUseCase>(),
            sp.GetRequiredService<ICreateRecipeUseCase>()));

        services.AddSingleton<ProfileViewModel>(sp => new ProfileViewModel(
            sp.GetRequiredService<IProfileOverviewUseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            () => NavigateToAsync("//LoginPage"),
            NavigateToAsync,
            sp.GetRequiredService<ILocalPhotoCacheService>(),
            sp.GetService<IProfileHealthConnection>()));

        services.AddTransient<PhotoJourneyVaultViewModel>(sp => new PhotoJourneyVaultViewModel(
            sp.GetRequiredService<IPhotoVaultUseCase>(),
            () => NavigateToAsync("..")));

        services.AddTransient<ProfilePersonalInfoViewModel>(sp => new ProfilePersonalInfoViewModel(
            sp.GetRequiredService<IProfilePersonalInfoUseCase>(),
            sp.GetRequiredService<IAccountPasswordValidator>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            () => NavigateToAsync("..")));

        services.AddTransient<ProfileNutritionSettingsViewModel>(sp => new ProfileNutritionSettingsViewModel(
            sp.GetRequiredService<IProfileNutritionSettingsUseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            () => NavigateToAsync("//ProfilePage"),
            () => NavigateToAsync("//NutritionPage")));

        services.AddTransient<ProfileWorkoutPreferencesViewModel>(sp => new ProfileWorkoutPreferencesViewModel(
            sp.GetRequiredService<IProfileWorkoutPreferencesUseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            () => NavigateToAsync("//ProfilePage"),
            () => NavigateToAsync("//WorkoutPage")));

        services.AddTransient<ProfileHealthSyncViewModel>(sp => new ProfileHealthSyncViewModel(
            sp.GetRequiredService<IProfileHealthSyncUseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            () => NavigateToAsync("..")));

        services.AddTransient<ProfileUnitsDisplayViewModel>(sp => new ProfileUnitsDisplayViewModel(
            sp.GetRequiredService<IProfileUnitsUseCase>(),
            () => NavigateToAsync("//ProfilePage")));

        services.AddTransient<ProfileFeedbackViewModel>(sp => new ProfileFeedbackViewModel(
            sp.GetRequiredService<IProfileFeedbackUseCase>(),
            () => NavigateToAsync("//ProfilePage")));

        services.AddTransient<InjuryLogViewModel>(sp => new InjuryLogViewModel(
            sp.GetRequiredService<IWorkoutInjuryUseCase>(),
            () => NavigateToAsync("..")));

        services.AddSingleton<IAppPreloadService, AppPreloadCoordinator>();
    }

    private static void RegisterPages(IServiceCollection services)
    {
        services.AddTransient<WorkoutSessionPage>();
        services.AddTransient<LoginPage>();
        services.AddTransient<AppStartupPage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<OtpVerificationPage>();
        services.AddTransient<QuizPage>();
        services.AddTransient(sp =>
            PageLoadExtensions.CreateWithTiming<ProfileSetupPage, ProfileSetupViewModel>(sp,
                vm => new ProfileSetupPage(vm)));
        services.AddSingleton(sp => PageLoadExtensions.CreateWithTiming<DashboardPage, DashboardViewModel>(sp,
            vm => new DashboardPage(vm)));
        services.AddTransient<DailyCheckInPage>();
        services.AddSingleton(sp =>
            PageLoadExtensions.CreateWithTiming<CyclePage, CycleViewModel>(sp, vm => new CyclePage(vm)));
        services.AddTransient<CyclePhaseDetailPage>();
        services.AddSingleton(sp => PageLoadExtensions.CreateWithTiming<NutritionPage, NutritionViewModel>(
            sp,
            vm => new NutritionPage(vm)));
        services.AddSingleton(sp =>
            PageLoadExtensions.CreateWithTiming<WorkoutPage, WorkoutViewModel>(sp,
                vm => new WorkoutPage(vm, sp.GetRequiredService<WorkoutSessionViewModel>(),
                    sp.GetRequiredService<INavigationContextService>())));
        services.AddSingleton(sp =>
            PageLoadExtensions.CreateWithTiming<ProfilePage, ProfileViewModel>(sp, vm => new ProfilePage(vm)));
        services.AddTransient<ProfilePersonalInfoPage>();
        services.AddTransient<ProfileNutritionSettingsPage>();
        services.AddTransient<ProfileWorkoutPreferencesPage>();
        services.AddTransient<ProfileHealthSyncPage>();
        services.AddTransient<ProfileUnitsDisplayPage>();
        services.AddTransient<ProfileFeedbackPage>();
        services.AddTransient<ProfilePrivacyPage>();
        services.AddTransient<InjuryLogPage>();
        services.AddTransient<FoodLogPage>();
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
                EnabledSslProtocols = SslProtocols.Tls13,
                RemoteCertificateValidationCallback = FdcCertificatePinningPolicy.Validate
            },
            UseCookies = false
        };
    }

    private static Task NavigateToAsync(string route)
    {
        if (RemoveQueryString(route) == "//LoginPage")
        {
            var app = Application.Current as App
                ?? throw new InvalidOperationException("The active application is unavailable.");
            return app.ActivateRootAsync(nameof(LoginPage));
        }

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

    private static async Task NavigateThenPreloadDashboardAsync(
        IServiceProvider services, bool preloadRemaining = true)
    {
        var app = Application.Current as App
            ?? throw new InvalidOperationException("The active application is unavailable.");
        await app.ActivateRootAsync(nameof(DashboardPage));

        if (!preloadRemaining)
            return;

        var dashboardPage = await App.WaitForDashboardPageAsync();
        if (dashboardPage is null)
        {
            Trace.WriteLine("[Navigation] Dashboard page is pending its first appearance.");
            return;
        }

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

    private static Task NavigateAuthenticatedAsync(string route)
    {
        var app = Application.Current as App
            ?? throw new InvalidOperationException("The active application is unavailable.");
        return app.ActivateRootAsync(RemoveQueryString(route).TrimStart('/'));
    }

    private static string RemoveQueryString(string route)
    {
        var queryIndex = route.IndexOf('?', StringComparison.Ordinal);
        return queryIndex < 0 ? route : route[..queryIndex];
    }
}
