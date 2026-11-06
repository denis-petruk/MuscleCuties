using System.Diagnostics;
using CommunityToolkit.Maui;
using MauiIcons.Fluent;
using MuscleCuties.Contracts.Account;
using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.UI.Composition;
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
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Workout;
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
using MuscleCuties.UseCases.Features.Caching;
using MuscleCuties.UseCases.Features.Cycle.DailyLog;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Cycle.PhaseEdit;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Dashboard.Overview;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;
using MuscleCuties.UseCases.Features.Hydration;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;
using MuscleCuties.UseCases.Features.Profile.Feedback;
using MuscleCuties.UseCases.Features.Profile.NutritionSettings;
using MuscleCuties.UseCases.Features.Profile.Overview;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Archive;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Session;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace MuscleCuties.UI;

public static class MauiProgram
{
    private const string WorkoutPreferencesAfterOnboardingContextKey = "workout-preferences-after-onboarding";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .UseFluentMauiIcons()
            .ConfigurePlatformInputHandlers()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "Inter");
                fonts.AddFont("OpenSans-Semibold.ttf", "InterSemibold");
            });

        var services = builder.Services;
        services.RegisterPlatformServices();
        services.RegisterTransportServices();
        services.RegisterDomainAndApplicationServices();
        services.RegisterWorkoutPlanningServices();
        RegisterViewModels(services);
        RegisterPages(services);

        builder.ConfigureAppLogging();

        return builder.Build();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<LoginViewModel>(sp => new LoginViewModel(
            sp.GetRequiredService<IAccountSignInUseCase>(),
            sp.GetRequiredService<IAccountLoginScheduler>(),
            () => NavigateAfterLoginAsync(sp),
            () => NavigateToProfileSetupAfterLoginAsync(sp),
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
            () => NavigateAfterOnboardingAsync(sp),
            () => NavigateToAsync("//LoginPage"),
            () => NavigateToAsync($"//{nameof(ProfileSetupPage)}"),
            () => sp.GetRequiredService<INavigationContextService>()
                .Set(WorkoutPreferencesAfterOnboardingContextKey, true),
            weekdays => sp.GetRequiredService<INavigationContextService>()
                .Set(ProfileWorkoutPreferencesViewModel.OnboardingWeekdaysContextKey, weekdays)));

        services.AddTransient<ProfileSetupViewModel>(sp => new ProfileSetupViewModel(
            sp.GetRequiredService<IProfileSetupUseCase>(),
            () => NavigateAuthenticatedAsync($"//{nameof(QuizPage)}")));

        services.AddSingleton<DashboardViewModel>(sp => new DashboardViewModel(
            sp.GetRequiredService<IDashboardOverviewUseCase>(),
            sp.GetRequiredService<IWorkoutInjuryUseCase>(),
            sp.GetRequiredService<IDailyCheckInUseCase>(),
            sp.GetRequiredService<ICyclePhaseProjectionUseCase>(),
            sp.GetRequiredService<IProfileUnitsUseCase>(),
            () => NavigateToAsync("//CyclePage"),
            () => NavigateToAsync("//WorkoutPage"),
            workoutDayId => NavigateWithContextAsync(sp, nameof(WorkoutSessionPage), "workoutDayId", workoutDayId),
            () => NavigateToAsync("//NutritionPage"),
            () => sp.GetRequiredService<IAppPreloadService>().InvalidateDashboard(),
            sp.GetService<UseCases.Features.Nutrition.DailyLog.INutritionChangeNotifier>(),
            () => NavigateToAsync(nameof(PhotoJourneyVaultPage))));

        services.AddSingleton<CycleViewModel>(sp => new CycleViewModel(
            sp.GetRequiredService<ICycleOverviewUseCase>(),
            sp.GetRequiredService<ICyclePhaseProjectionUseCase>(),
            sp.GetRequiredService<ICycleDailyLogUseCase>(),
            sp.GetRequiredService<ICyclePhaseEditUseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            sp.GetRequiredService<IDashboardOverviewUseCase>(),
            phase => NavigateWithContextAsync(sp, nameof(CyclePhaseDetailPage), "phase", phase),
            () => DateTime.Today,
            () => NavigateToAsync(nameof(ProfilePersonalInfoPage))));

        services.AddTransient<CyclePhaseDetailViewModel>(sp =>
            new CyclePhaseDetailViewModel(() => NavigateToAsync("..")));

        services.AddSingleton<NutritionViewModel>(sp => new NutritionViewModel(
            sp.GetRequiredService<INutritionOverviewUseCase>(),
            sp.GetRequiredService<IFoodCatalogSearchUseCase>(),
            sp.GetRequiredService<IRecipePortionUseCase>(),
            NavigateToAsync,
            sp.GetService<UseCases.Features.Nutrition.DailyLog.INutritionChangeNotifier>(),
            sp.GetRequiredService<ILookupBarcodeUseCase>(),
            sp.GetRequiredService<IBarcodeScannerHardwareService>(),
            sp.GetRequiredService<ISaveMealTemplateUseCase>(),
            sp.GetRequiredService<IGetRecipesAndTemplatesUseCase>()));

        services.AddSingleton<WorkoutViewModel>(sp => new WorkoutViewModel(
            sp.GetRequiredService<IWorkoutOverviewUseCase>(),
            sp.GetRequiredService<IWorkoutInjuryUseCase>(),
            NavigateToAsync,
            workoutDayId => NavigateWithContextAsync(sp, nameof(WorkoutSessionPage), "workoutDayId", workoutDayId),
            sp.GetRequiredService<IStrengthStyleOfferClient>(),
            async offer =>
            {
                var alternatives = offer.AlternativeStyleIds
                    .Select(id => (WorkoutActivityDto)id)
                    .Where(WorkoutPlanningConstants.StrengthActivities.Contains)
                    .Distinct()
                    .ToArray();
                if (alternatives.Length == 0) return null;
                if (alternatives.Length == 1) return alternatives[0];
                var labels = alternatives.ToDictionary(
                    style => StrengthPlanGenerationDisplayMapper.ActivityLabel(style), style => style);
                var selected = await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayActionSheetAsync(
                    UiTextCatalog.Get("Workout_StyleOffer_Title"),
                    UiTextCatalog.Get("Workout_StyleOffer_Keep"), null, labels.Keys.ToArray()));
                return selected is not null && labels.TryGetValue(selected, out var style) ? style : null;
            },
            style => NavigateWithContextAsync(sp, nameof(StrengthPlanGenerationPage),
                StrengthPlanGenerationViewModel.OfferedStrengthStyleContextKey, style)));

        services.AddTransient<StrengthPlanGenerationViewModel>(sp => new StrengthPlanGenerationViewModel(
            sp.GetRequiredService<IStrengthPlanProposalClientV020>(),
            sp.GetRequiredService<IProfileWorkoutPreferencesUseCase>(),
            sp.GetRequiredService<IWorkoutArchiveUseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            () => NavigateToAsync(".."),
            () => NavigateToAsync("//WorkoutPage"),
            sp.GetRequiredService<INavigationContextService>(),
            summary => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync(
                UiTextCatalog.Get("Workout_Generation_ReplacementTitle"), summary,
                UiTextCatalog.Get("Workout_Generation_ApplyPlan"),
                UiTextCatalog.Get("Workout_Generation_ReplacementCancel"))),
            () => NavigateToAsync("//DashboardPage"),
            sp.GetRequiredService<IWorkoutAccountCalendarClient>(),
            overview: sp.GetRequiredService<IWorkoutOverviewUseCase>(),
            confirmInjuryAdaptationAsync: summary => MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.DisplayAlertAsync(
                    UiTextCatalog.Get("Workout_Generation_InjuryTitle"), summary,
                    UiTextCatalog.Get("Workout_Generation_InjuryContinue"),
                    UiTextCatalog.Get("Common_Cancel")))));

        services.AddTransient<WorkoutSessionViewModel>(sp => new WorkoutSessionViewModel(
            sp.GetRequiredService<IWorkoutSessionActionUseCase>(),
            sp.GetRequiredService<ICycleOverviewUseCase>(),
            route => NavigateToAsync(route),
            sp.GetRequiredService<IStrengthPlanProposalClientV020>(),
            ConfirmHiitHistoryAsync,
            () =>
            {
                sp.GetRequiredService<INavigationContextService>().Set(StrengthPlanGenerationViewModel.EditHiitAnswersContextKey, true);
                return NavigateToAsync(nameof(StrengthPlanGenerationPage));
            }));

        services.AddTransient<HydrationClient>();
        services.AddTransient<BodyMeasurementClient>();
        services.AddTransient<FoodLogViewModel>(sp => new FoodLogViewModel(
            sp.GetRequiredService<INutritionDailyLogUseCase>(),
            sp.GetRequiredService<IRecipePortionUseCase>(),
            MuscleCuties.UI.Services.Nutrition.NutritionPresentationMapper.FromWaterQuickAddDefaults(
                sp.GetRequiredService<IGetWaterQuickAddDefaultsUseCase>().Execute()),
            () => NavigateToAsync(".."),
            sp.GetService<UseCases.Features.Nutrition.DailyLog.INutritionChangeNotifier>(),
            sp.GetRequiredService<ILookupBarcodeUseCase>(),
            sp.GetRequiredService<IBarcodeScannerHardwareService>(),
            sp.GetRequiredService<ISaveMealTemplateUseCase>(),
            sp.GetRequiredService<IGetRecipesAndTemplatesUseCase>(),
            sp.GetRequiredService<ICreateRecipeUseCase>(),
            sp.GetRequiredService<INutritionOverviewUseCase>(),
            sp.GetRequiredService<HydrationClient>()));

        services.AddTransient<WorkoutHistoryViewModel>(sp => new WorkoutHistoryViewModel(
            sp.GetRequiredService<IWorkoutArchiveUseCase>(),
            () => NavigateToAsync("..")));

        services.AddSingleton<ProfileViewModel>(sp => new ProfileViewModel(
            sp.GetRequiredService<IProfileOverviewUseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            () => NavigateToAsync("//LoginPage"),
            NavigateToAsync,
            sp.GetRequiredService<ILocalPhotoCacheService>()));

        services.AddTransient<PhotoJourneyVaultViewModel>(sp => new PhotoJourneyVaultViewModel(
            sp.GetRequiredService<IPhotoVaultUseCase>(),
            () => NavigateToAsync("..")));

        services.AddTransient<ProfilePersonalInfoViewModel>(sp => new ProfilePersonalInfoViewModel(
            sp.GetRequiredService<IProfilePersonalInfoUseCase>(),
            sp.GetRequiredService<IAccountPasswordValidator>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            sp.GetRequiredService<IProfileUnitsUseCase>(),
            () => NavigateToAsync(".."),
            sp.GetRequiredService<BodyMeasurementClient>()));

        services.AddTransient<ProfileNutritionSettingsViewModel>(sp => new ProfileNutritionSettingsViewModel(
            sp.GetRequiredService<IProfileNutritionSettingsUseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            () => NavigateToAsync("//ProfilePage"),
            () => NavigateToAsync("//NutritionPage")));

        services.AddTransient<ProfileWorkoutPreferencesViewModel>(sp => new ProfileWorkoutPreferencesViewModel(
            sp.GetRequiredService<IProfileWorkoutPreferencesUseCase>(),
            sp.GetRequiredService<GetReviewedExerciseExclusionCatalogV020UseCase>(),
            sp.GetRequiredService<IProfileRefreshNotifier>(),
            () => NavigateToAsync(".."),
            () => NavigateToAsync(nameof(StrengthPlanGenerationPage)),
            sp.GetRequiredService<INavigationContextService>()));

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
        services.AddTransient<StrengthPlanGenerationPage>();
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
        services.AddTransient<ProfileUnitsDisplayPage>();
        services.AddTransient<ProfileFeedbackPage>();
        services.AddTransient<ProfilePrivacyPage>();
        services.AddTransient<FoodLogPage>();
        services.AddTransient<WorkoutHistoryPage>();
        services.AddTransient<PhotoJourneyVaultPage>();
        services.AddTransient<PasswordRecoveryPage>();

        services.AddSingleton<AppShell>();
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

    private static async Task<HiitConfirmationChoice> ConfirmHiitHistoryAsync(SavedActivityAnswersDto saved)
    {
        var evidence = saved.HiitEligibilityEvidence;
        var history = evidence?.UnderlyingCardioHistory;
        var weeks = evidence?.PreviousFourWeeksAerobicEvidence;
        if (history is null || weeks is null || saved.HiitUnderlyingCardioActivity is null ||
            evidence?.ExistingClearanceScreeningSatisfied is null)
            return HiitConfirmationChoice.Update;

        var summary = UiTextCatalog.Format("Workout_Session_HiitConfirmationSummary",
            saved.HiitUnderlyingCardioActivity, history.ConsistentParticipationWeeks,
            history.AverageSessionsPerWeekInRecentFourWeeks, history.LastCompletedSessionDate,
            string.Join(", ", weeks.OrderBy(week => week.WeekOffsetFromCurrent)
                .Select(week => week.QualifyingManageableAerobicSessionCount)),
            UiTextCatalog.Get(evidence.ExistingClearanceScreeningSatisfied == true
                ? "Workout_Session_HiitAnswerYes" : "Workout_Session_HiitAnswerNo"));
        var confirm = UiTextCatalog.Get("Workout_Session_HiitConfirm");
        var update = UiTextCatalog.Get("Workout_Session_HiitUpdate");
        var selected = await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayActionSheetAsync(
            summary, UiTextCatalog.Get("Workout_Session_HiitCancel"), null, confirm, update));
        return selected == confirm ? HiitConfirmationChoice.Confirm :
            selected == update ? HiitConfirmationChoice.Update : HiitConfirmationChoice.Cancel;
    }

    private static async Task NavigateAfterOnboardingAsync(IServiceProvider services)
    {
        await NavigateThenPreloadDashboardAsync(services, preloadRemaining: false);
        services.GetRequiredService<INavigationContextService>()
            .TryTake<int[]>(ProfileWorkoutPreferencesViewModel.OnboardingWeekdaysContextKey, out _);
        await NavigateToAsync(nameof(StrengthPlanGenerationPage));
    }

    private static Task NavigateAfterLoginAsync(IServiceProvider services)
    {
        var navigationContext = services.GetRequiredService<INavigationContextService>();
        if (navigationContext.TryTake<bool>(WorkoutPreferencesAfterOnboardingContextKey, out var pending) && pending)
            return NavigateAfterOnboardingAsync(services);

        return NavigateThenPreloadDashboardAsync(services, preloadRemaining: false);
    }

    private static Task NavigateToProfileSetupAfterLoginAsync(IServiceProvider services)
    {
        services.GetRequiredService<INavigationContextService>()
            .TryTake<bool>(WorkoutPreferencesAfterOnboardingContextKey, out _);
        return NavigateAuthenticatedAsync($"//{nameof(ProfileSetupPage)}");
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
