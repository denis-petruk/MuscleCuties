using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.Core.Services.Dashboard;
using MuscleCuties.Core.Services.Dashboard.Planning;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Core.Services.Quiz;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.Infrastructure.Cycle.Overview;
using MuscleCuties.Infrastructure.Dashboard.Overview;
using MuscleCuties.Infrastructure.Nutrition.DailyLog;
using MuscleCuties.Infrastructure.Nutrition.Recipes;
using MuscleCuties.UI.Services;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.Services.Nutrition;
using MuscleCuties.UI.Services.Profile;
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
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Overview;
using MuscleCuties.UseCases.Features.Workout.Planning;
using INutritionChangeNotifier = MuscleCuties.Core.Services.Nutrition.INutritionChangeNotifier;

namespace MuscleCuties.UI.Composition;

public static class ApplicationCompositionExtensions
{
    public static IServiceCollection RegisterDomainAndApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICalorieCalculator, CalorieCalculator>();
        services.AddScoped<INutritionPlanner, NutritionPlanner>();
        services.AddScoped<ICyclePhaseCalculator, CyclePhaseCalculator>();
        services.AddScoped<ICyclePredictionPlanner, CyclePredictionPlanner>();
        services.AddScoped<IDashboardPlanner, DashboardPlanner>();

        services.AddScoped<CloudDailyCheckInStore>();
        services.AddSingleton<IDailyCheckInStore, ScopedCloudDailyCheckInStore>();
        services.AddSingleton<IDailyCheckInUseCase, DailyCheckInUseCase>();

        services.AddSingleton<IDashboardOverviewStore, CloudDashboardOverviewStore>();
        services.AddSingleton<IDashboardOverviewUseCase, DashboardOverviewUseCase>();

        services.AddSingleton<INutritionChangeNotifier>(_ =>
            new NutritionChangeNotifier(MainThread.BeginInvokeOnMainThread));
        services.AddSingleton<IBarcodeScannerHardwareService, BarcodeScannerHardwareService>();

        services.AddSingleton<IProfileRefreshNotifier>(sp =>
            new ProfileRefreshNotifier(new Lazy<IAppPreloadService>(sp.GetRequiredService<IAppPreloadService>)));
        services.AddScoped<ICurrentProfileUserIdProvider, CurrentProfileUserIdProvider>();

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

        services.AddSingleton<IReviewedExerciseExclusionCatalogV020Store,
            CloudReviewedExerciseExclusionCatalogV020Store>();
        services.AddSingleton<GetReviewedExerciseExclusionCatalogV020UseCase>();

        services.AddSingleton<IProfileUnitsStore, CloudProfileUnitsStore>();
        services.AddSingleton<IProfileUnitsUseCase, ProfileUnitsUseCase>();

        services.AddHttpClient<IPhotoVaultStore, MobilePhotoVaultStore>();
        services.AddSingleton<IPhotoVaultUseCase, PhotoVaultUseCase>();

        services.AddSingleton<IProfileFeedbackComposer, LocalProfileFeedbackComposer>();
        services.AddSingleton<IProfileFeedbackUseCase, ProfileFeedbackUseCase>();

        services.AddSingleton<ILocalizationService, LocalizationService>();

        services.AddSingleton<ICycleOverviewStore, CloudCycleOverviewStore>();
        services.AddSingleton<ICycleOverviewUseCase, CycleOverviewUseCase>();
        services.AddSingleton<ICyclePhaseProjectionUseCase, CyclePhaseProjectionUseCase>();

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
        services.AddSingleton<IRecipePortionUseCase, RecipePortionUseCase>();
        services.AddSingleton<IGetWaterQuickAddDefaultsUseCase, GetWaterQuickAddDefaultsUseCase>();
        services.AddSingleton<ISaveMealTemplateUseCase, SaveMealTemplateUseCase>();
        services.AddSingleton<IGetRecipesAndTemplatesUseCase, GetRecipesAndTemplatesUseCase>();

        services.AddScoped<IWorkoutArchiveStore, CloudWorkoutArchiveStore>();
        services.AddScoped<IWorkoutArchiveUseCase, WorkoutArchiveUseCase>();

        services.AddSingleton<IWorkoutInjuryStore, CloudWorkoutInjuryStore>();
        services.AddSingleton<IWorkoutInjuryUseCase, WorkoutInjuryUseCase>();

        services.AddSingleton<IWorkoutOverviewStore, CloudWorkoutOverviewStore>();
        services.AddSingleton<IWorkoutOverviewUseCase, WorkoutOverviewUseCase>();

        return services;
    }
}
