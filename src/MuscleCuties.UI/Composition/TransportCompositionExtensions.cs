using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.ExerciseMedia;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Profile;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Caching;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.Infrastructure.Nutrition.DailyLog;
using MuscleCuties.Infrastructure.Nutrition.Recipes;
using MuscleCuties.Infrastructure.Workout.Media;
using MuscleCuties.UI.Infrastructure.Network;
using MuscleCuties.UI.Services;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UI.Services.Configuration;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.Services.Security;
using MuscleCuties.UseCases.Features.Caching;
using MuscleCuties.UseCases.Features.Dashboard.Overview.Contracts;

namespace MuscleCuties.UI.Composition;

public static class TransportCompositionExtensions
{
#if DEBUG
    private const string DebugBackendBaseUrlPreferenceKey = "backend_api_base_url";
#endif
    private const int BackendRequestTimeoutSeconds = 15;

    public static IServiceCollection RegisterTransportServices(this IServiceCollection services)
    {


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
        services.AddSingleton<IBackendSessionTokenStore>(_ => new SecureStorageBackendSessionTokenStore(
            AppEndpointConfiguration.GetSessionTokenKey(AppEndpointConfiguration.ActiveEnvironment),
            allowLegacyMigration: AppEndpointConfiguration.ActiveEnvironment == AppTargetEnvironment.Beta));
        services.AddSingleton<ILocalCacheService>(sp => new EncryptedLocalSnapshotCacheService(
            FileSystem.AppDataDirectory,
            sp.GetRequiredService<ISnapshotCacheKeyStore>(),
            ProfileMediaProtection.Apply,
            environmentName: AppEndpointConfiguration.ActiveEnvironment.ToString().ToLowerInvariant()));

        services.AddSingleton<ICloudAuthorityGate>(_ => new VersionedCloudAuthorityGate());

        services.AddHttpClient<IMuscleCutiesBackendApiClient, MuscleCutiesBackendApiClient>(client =>
        {
            string? overrideUrl = null;
#if DEBUG
            overrideUrl = Preferences.Get(DebugBackendBaseUrlPreferenceKey, (string?)null);
#endif
            var configuredUrl = AppEndpointConfiguration.ResolveBackendBaseUrl(overrideUrl);
            client.BaseAddress = new Uri(configuredUrl);
            client.Timeout = TimeSpan.FromSeconds(BackendRequestTimeoutSeconds);
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<MobileJourneyCloudGateway>();

        services.AddSingleton<IPhotoCacheAccountLifecycle>(sp => new PhotoCacheAccountLifecycle(
            async () =>
            {
                await using var scope = sp.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<IAuthService>()
                    .GetCurrentUserIdAsync().ConfigureAwait(false);
            },
            sp.GetRequiredService<ILocalPhotoCacheService>()));

        return services;
    }
}
