using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MuscleCuties.Infrastructure.PostgreSql.Account.Registration;
using MuscleCuties.Infrastructure.PostgreSql.Account.Authentication;
using MuscleCuties.Infrastructure.PostgreSql.Account.EmailChallenge;
using MuscleCuties.Infrastructure.PostgreSql.Account.MigrationClaims;
using MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;
using MuscleCuties.UseCases.Features.Account.Deletion;
using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;
using MuscleCuties.Infrastructure.PostgreSql.Cycle.History;
using MuscleCuties.Infrastructure.PostgreSql.Cycle.Overview;
using MuscleCuties.Infrastructure.PostgreSql.CheckInAndInjury;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.CatalogSearch;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Diary;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Targets;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.BatchAndCopy;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Recipes;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Overview;
using MuscleCuties.Infrastructure.PostgreSql.Profile.UnitsDisplay;
using MuscleCuties.Infrastructure.PostgreSql.Profile.ServerProfile;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Archive;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Injury;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Account.Authentication;
using MuscleCuties.UseCases.Features.Account.EmailChallenge;
using MuscleCuties.UseCases.Features.Account.MigrationClaims;
using MuscleCuties.UseCases.Features.Cycle.History;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.CustomFoods;
using MuscleCuties.UseCases.Features.Nutrition.Diary;
using MuscleCuties.UseCases.Features.Nutrition.Targets;
using MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Archive;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.Infrastructure.PostgreSql.Dashboard;
using MuscleCuties.UseCases.Features.Dashboard.Overview;
using MuscleCuties.Server.Dashboard;
using MuscleCuties.Server.Nutrition;
using MuscleCuties.Server.Cycle;
using MuscleCuties.Infrastructure.PostgreSql.Cycle.PhaseLog;
using MuscleCuties.UseCases.Features.Cycle.PhaseLog;
using MuscleCuties.Server.CheckInAndInjury;
using MuscleCuties.Server.Workout.Planning;
using MuscleCuties.Server.Workout.ActiveSession;
using MuscleCuties.Server.Workout.Archive;
using MuscleCuties.Server.Workout.Injury;
using MuscleCuties.Server.Workout.Session;
using MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using MuscleCuties.Server.Profile.PhotoVault;
using MuscleCuties.Server.Account.Registration;
using MuscleCuties.Infrastructure.PostgreSql.Hydration;
using MuscleCuties.Infrastructure.PostgreSql.Progress;
using MuscleCuties.Infrastructure.PostgreSql.Fasting;
using MuscleCuties.UseCases.Features.Hydration;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;
using MuscleCuties.UseCases.Features.Fasting;
using MuscleCuties.Server.Hydration;

namespace MuscleCuties.Server;

public static class ApiApplication
{
    private const string AuthenticationSectionName = "Authentication";
    private const string SigningKeySettingName = "SigningKey";
    private const string IssuerSettingName = "Issuer";
    private const string AudienceSettingName = "Audience";
    private const string TrustedProxiesSettingName = "Authentication:TrustedProxies";
    private const int TrustedProxyForwardLimit = 1;
    private const int MinimumSigningKeyBytes = 32;
    private const int PasswordRecoveryEmailReceiptPurpose = 2;
    private const string ApiVersion = "v1";
    private const string RequestIdHeader = "X-Request-Id";
    private const string SessionRoute = "/api/v1/session";
    private const string LiveRoute = "/health/live";
    private const string ReadyRoute = "/health/ready";
    private const string AuthenticationRoute = "/api/v1/auth";
    private const string EmailChallengeRoute = "/api/v1/email-challenges";
    private const string ProfileUnitsRoute = "/api/v1/profile/units";
    private const string LegacyWorkoutPreferencesRoute = "/api/v1/profile/workout-preferences";
    private const string WorkoutPreferencesRequireProposalCode = "WorkoutPreferencesRequireProposal";
    private const string WorkoutPreferencesRequireProposalMessage =
        "Change workout preferences through a workout plan proposal.";

    public static WebApplication Build(
        WebApplicationBuilder builder,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var connectionString = builder.Configuration.GetConnectionString("PostgreSql");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:PostgreSql must be configured.");

        if (LambdaHostingConfiguration.IsRunningInLambda())
        {
            var poolOptions = LambdaHostingConfiguration.ResolveOptions(builder.Configuration);
            connectionString = LambdaHostingConfiguration.BuildBoundedRdsProxyConnectionString(
                connectionString, poolOptions);
        }

        var auth = builder.Configuration.GetSection(AuthenticationSectionName);
        var signingKey = auth[SigningKeySettingName];
        var issuer = auth[IssuerSettingName];
        var audience = auth[AudienceSettingName];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < MinimumSigningKeyBytes)
            throw new InvalidOperationException("Authentication:SigningKey must be configured with at least 32 UTF-8 bytes.");
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Authentication:Issuer and Authentication:Audience must be configured.");

        var trustedProxyAddresses = builder.Configuration.GetSection(TrustedProxiesSettingName).GetChildren()
            .Where(section => !string.IsNullOrWhiteSpace(section.Value))
            .Select(section => IPAddress.TryParse(section.Value, out var address)
                ? address
                : throw new InvalidOperationException("Authentication:TrustedProxies contains an invalid IP address."))
            .ToArray();
        if (trustedProxyAddresses.Length > 0)
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                options.ForwardLimit = TrustedProxyForwardLimit;
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                foreach (var address in trustedProxyAddresses)
                    options.KnownProxies.Add(address);
            });

        builder.Services.AddPostgreSqlPersistence(connectionString);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IRegistrationPasswordHasher, Pbkdf2RegistrationPasswordHasher>();
        var emailReceiptPublicKeys = builder.Configuration.GetSection("EmailReceipts:PublicKeys").GetChildren()
            .Where(section => !string.IsNullOrWhiteSpace(section.Value))
            .ToDictionary(section => section.Key, section => section.Value!, StringComparer.Ordinal);
        builder.Services.AddSingleton<IRegistrationEmailReceiptVerifier>(serviceProvider =>
        {
            return new SignedRegistrationEmailReceiptVerifier(
                emailReceiptPublicKeys,
                builder.Configuration["EmailReceipts:Issuer"] ?? string.Empty,
                builder.Configuration["EmailReceipts:Audience"] ?? string.Empty,
                serviceProvider.GetRequiredService<TimeProvider>());
        });
        builder.Services.AddScoped<ICompleteAccountRegistrationStore, PostgresCompleteAccountRegistrationStore>();
        builder.Services.AddScoped<ICompleteAccountRegistrationUseCase, CompleteAccountRegistrationUseCase>();
        builder.Services.AddScoped<GetRegistrationQuizUseCase>();
        builder.Services.AddScoped<IAccountPasswordResetStore, PostgresAccountPasswordResetStore>();
        builder.Services.AddScoped<IAccountPasswordResetUseCase>(serviceProvider => new CompletePasswordResetUseCase(
            new SignedRegistrationEmailReceiptVerifier(
                emailReceiptPublicKeys,
                builder.Configuration["EmailReceipts:Issuer"] ?? string.Empty,
                builder.Configuration["EmailReceipts:Audience"] ?? string.Empty,
                serviceProvider.GetRequiredService<TimeProvider>(),
                expectedPurpose: PasswordRecoveryEmailReceiptPurpose),
            serviceProvider.GetRequiredService<IRegistrationPasswordHasher>(),
            serviceProvider.GetRequiredService<IAccountPasswordResetStore>(),
            serviceProvider.GetRequiredService<TimeProvider>()));
        builder.Services.AddScoped<IAccountMigrationClaimStore, PostgresAccountMigrationClaimStore>();
        builder.Services.AddScoped<IAccountMigrationClaimUseCase, AccountMigrationClaimUseCase>();
        builder.Services.AddScoped<IServerProfileUnitsStore, PostgresProfileUnitsStore>();
        builder.Services.AddScoped<ServerProfileUnitsUseCase>();
        builder.Services.AddScoped<IServerProfileDataStore, PostgresProfileDataStore>();
        builder.Services.AddScoped<ServerProfileUseCase>();
        builder.Services.AddScoped<ICycleHistoryStore, PostgresCycleHistoryStore>();
        builder.Services.AddScoped<CycleHistoryUseCase>();
        builder.Services.AddScoped<ICyclePhaseLogStore, PostgresCyclePhaseLogStore>();
        builder.Services.AddScoped<ISaveCyclePhaseLogUseCase, SaveCyclePhaseLogUseCase>();
        builder.Services.AddScoped<IServerNutritionTargetProjectionStore, PostgresNutritionTargetProjectionStore>();
        builder.Services.AddScoped<ServerNutritionTargetProjectionUseCase>();
        builder.Services.AddScoped<IServerCyclePhaseLogProjectionStore, PostgresCyclePhaseLogProjectionStore>();
        builder.Services.AddScoped<ServerCycleOverviewUseCase>();
        builder.Services.AddScoped<ICheckInAndInjuryStore, PostgresCheckInAndInjuryStore>();
        builder.Services.AddScoped<CheckInAndInjuryUseCase>();
        builder.Services.AddScoped<PostgresFoodCatalogStore>();
        builder.Services.AddScoped<IFoodCatalogSearchStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresFoodCatalogStore>());
        builder.Services.AddScoped<IBarcodeFoodLookupStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresFoodCatalogStore>());
        builder.Services.AddScoped<IFoodCatalogSearchUseCase, FoodCatalogSearchUseCase>();
        builder.Services.AddScoped<ILookupBarcodeUseCase, LookupBarcodeUseCase>();
        builder.Services.AddScoped<ICreateCustomFoodStore, PostgresCreateCustomFoodStore>();
        builder.Services.AddScoped<ICreateCustomFoodUseCase, CreateCustomFoodUseCase>();
        builder.Services.AddSingleton<PostgresIdempotencyExecutor>();
        builder.Services.AddScoped<PostgresNutritionDiaryStore>();
        builder.Services.AddScoped<INutritionDiaryStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresNutritionDiaryStore>());
        builder.Services.AddScoped<INutritionDiaryCommandEventStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresNutritionDiaryStore>());
        builder.Services.AddScoped<NutritionDiaryUseCase>();
        builder.Services.AddScoped<PostgresNutritionBatchAndCopyStore>();
        builder.Services.AddScoped<INutritionBatchAndCopyStore>(sp => sp.GetRequiredService<PostgresNutritionBatchAndCopyStore>());
        builder.Services.AddScoped<INutritionCopyPreviewStore>(sp => sp.GetRequiredService<PostgresNutritionBatchAndCopyStore>());
        builder.Services.AddScoped<INutritionBatchAndCopyChangeNotifier, ServerNutritionBatchAndCopyChangeNotifier>();
        builder.Services.AddScoped<NutritionBatchAndCopyUseCase>();
        builder.Services.AddScoped<NutritionCopyPreviewUseCase>();
        builder.Services.AddScoped<PostgresRecipeStore>();
        builder.Services.AddScoped<IRecipeStore>(sp => sp.GetRequiredService<PostgresRecipeStore>());
        builder.Services.AddScoped<ICreateRecipeUseCase, CreateRecipeUseCase>();
        builder.Services.AddScoped<CreateRecipeUseCase>();
        builder.Services.AddScoped<IPreviewRecipePortionUseCase, PreviewRecipePortionUseCase>();
        builder.Services.AddScoped<PreviewRecipePortionUseCase>();
        builder.Services.AddScoped<IGetRecipesAndTemplatesUseCase, GetRecipesAndTemplatesUseCase>();
        builder.Services.AddScoped<GetRecipesAndTemplatesUseCase>();
        builder.Services.AddScoped<ISaveMealTemplateUseCase, SaveMealTemplateUseCase>();
        builder.Services.AddScoped<SaveMealTemplateUseCase>();
        builder.Services.AddScoped<PostgresNutritionOverviewStore>();
        builder.Services.AddScoped<IServerNutritionOverviewStore>(sp => sp.GetRequiredService<PostgresNutritionOverviewStore>());
        builder.Services.AddScoped<INutritionOverviewStore>(sp => sp.GetRequiredService<PostgresNutritionOverviewStore>());
        builder.Services.AddScoped<ServerNutritionOverviewUseCase>();
        builder.Services.AddScoped<IWorkoutPlanningCatalogStore, PostgresWorkoutPlanningCatalogStore>();
        builder.Services.AddScoped<IWorkoutProgressStore, PostgresWorkoutProgressStore>();
        builder.Services.AddScoped<GetWorkoutPlanningCatalogUseCase>();
        builder.Services.AddScoped<IStrengthPlanPreviewSourceV020, PostgresStrengthPlanPreviewSourceV020>();
        builder.Services.AddScoped<IReviewedExerciseExclusionCatalogV020Store,
            PostgresReviewedExerciseExclusionCatalogV020Store>();
        builder.Services.AddScoped<GetReviewedExerciseExclusionCatalogV020UseCase>();
        builder.Services.AddScoped<PreviewStrengthPlanV020UseCase>();
        builder.Services.AddScoped<IStrengthPlanProposalStoreV020, PostgresStrengthPlanProposalStoreV020>();
        builder.Services.AddScoped<IAcceptStrengthPlanProposalStoreV020,
            PostgresAcceptStrengthPlanProposalStoreV020>();
        builder.Services.AddScoped<CreateStrengthPlanProposalV020UseCase>();
        builder.Services.AddScoped<AcceptStrengthPlanProposalV020UseCase>();
        builder.Services.AddScoped<IWorkoutActiveSessionStore, PostgresWorkoutActiveSessionStore>();
        builder.Services.AddScoped<WorkoutActiveSessionUseCase>();
        builder.Services.AddScoped<IServerWorkoutArchiveStore, PostgresWorkoutArchiveStore>();
        builder.Services.AddScoped<ServerWorkoutArchiveUseCase>();
        builder.Services.AddScoped<IServerWorkoutInjuryStore, PostgresWorkoutInjuryStore>();
        builder.Services.AddScoped<ServerWorkoutInjuryUseCase>();
        builder.Services.AddScoped<PostgresWorkoutPlanAndSessionStore>();
        builder.Services.AddScoped<PostgresReadinessAdjustmentStore>();
        builder.Services.AddScoped<IServerDashboardOverviewStore, PostgresDashboardOverviewStore>();
        builder.Services.AddScoped<ServerDashboardOverviewUseCase>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<PostgresHydrationStore>();
        builder.Services.AddScoped<IWaterPresetInitializationStore>(sp => sp.GetRequiredService<PostgresHydrationStore>());
        builder.Services.AddScoped<IWaterPresetStore>(sp => sp.GetRequiredService<PostgresHydrationStore>());
        builder.Services.AddScoped<IWaterIntakeStore>(sp => sp.GetRequiredService<PostgresHydrationStore>());
        builder.Services.AddScoped<IWaterReminderConfigurationStore>(sp => sp.GetRequiredService<PostgresHydrationStore>());
        builder.Services.AddScoped<InitializeWaterPresetsUseCase>();
        builder.Services.AddScoped<GetWaterPresetsUseCase>();
        builder.Services.AddScoped<CreateWaterPresetUseCase>();
        builder.Services.AddScoped<UpdateWaterPresetUseCase>();
        builder.Services.AddScoped<DeleteWaterPresetUseCase>();
        builder.Services.AddScoped<ReorderWaterPresetsUseCase>();
        builder.Services.AddScoped<GetWaterIntakeUseCase>();
        builder.Services.AddScoped<ChangeWaterIntakeUseCase>();
        builder.Services.AddScoped<GetWaterReminderConfigurationUseCase>();
        builder.Services.AddScoped<SaveWaterReminderConfigurationUseCase>();
        builder.Services.AddScoped<PreviewWaterReminderUseCase>();
        builder.Services.AddScoped<IBodyMeasurementSession, HttpContextBodyMeasurementSession>();
        builder.Services.AddScoped<IBodyMeasurementStore, PostgresBodyMeasurementStore>();
        builder.Services.AddScoped<CreateBodyMeasurementUseCase>();
        builder.Services.AddScoped<CorrectBodyMeasurementUseCase>();
        builder.Services.AddScoped<DeleteBodyMeasurementUseCase>();
        builder.Services.AddScoped<ReadBodyMeasurementHistoryUseCase>();
        builder.Services.AddScoped<IFastingSessionStore, PostgresFastingStore>();
        builder.Services.AddScoped<FastingSessionUseCase>();
        builder.Services.AddSingleton<IS3PresignedUrlService, DefaultS3PresignedUrlService>();
        builder.Services.AddScoped<IPhotoUploadIntentStore, PostgresPhotoUploadIntentStore>();
        builder.Services.AddHttpClient<IS3PhotoObjectInspector, DefaultS3PhotoObjectInspector>();
        builder.Services.AddSingleton<IPhotoByteSanitizer, SkiaPhotoByteSanitizer>();
        builder.Services.AddScoped<IServerPhotoVaultStore, PostgresPhotoVaultStore>();
        builder.Services.AddScoped<ServerPhotoVaultUseCase>();
        builder.Services.AddSingleton<IAccountDeletionAuditStore, DefaultAccountDeletionAuditStore>();
        builder.Services.AddScoped<IServerAccountPurgeStore, PostgresAccountPurgeStore>();
        builder.Services.AddHttpClient("AppleAccountTokenRevoker", client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.AddScoped<IAppleAccountTokenRevoker>(serviceProvider =>
            new AppleAccountTokenRevoker(
                serviceProvider.GetRequiredService<PostgresDbContext>(),
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("AppleAccountTokenRevoker"),
                serviceProvider.GetRequiredService<TimeProvider>(),
                builder.Configuration["AppleSignIn:Audience"] ?? string.Empty,
                builder.Configuration["AppleSignIn:TeamId"],
                builder.Configuration["AppleSignIn:KeyId"],
                builder.Configuration["AppleSignIn:PrivateKeyPem"]));
        builder.Services.AddScoped<ServerAccountDeletionUseCase>();
        builder.Services.AddScoped<IDeletionReplayService, PostgresDeletionReplayService>();
        builder.Services.AddSingleton<MuscleCuties.Core.Domain.Workout.IReadinessEngine>(
            new MuscleCuties.Core.Domain.Workout.ReadinessEngine(
                MuscleCuties.Core.Models.Workout.Planning.WorkoutPlanningConfig.CreateDefault(),
                "v1.0-clinical"));
        builder.Services.AddScoped<MuscleCuties.UseCases.Features.Workout.Readiness.Contracts.IEvaluateReadinessUseCase, MuscleCuties.UseCases.Features.Workout.Readiness.EvaluateReadinessUseCase>();
        builder.Services.AddScoped<MuscleCuties.UseCases.Features.Workout.Readiness.EvaluateReadinessUseCase>();
        var appleKeys = builder.Configuration.GetSection("AppleSignIn:PublicKeys").GetChildren()
            .Where(section => !string.IsNullOrWhiteSpace(section.Value))
            .ToDictionary(section => section.Key, section => section.Value!, StringComparer.Ordinal);
        builder.Services.AddScoped<IAccountAuthenticationUseCase>(serviceProvider =>
            new PostgresAccountAuthenticationUseCase(
                serviceProvider.GetRequiredService<PostgresDbContext>(),
                serviceProvider.GetRequiredService<IRegistrationPasswordHasher>(),
                serviceProvider.GetRequiredService<TimeProvider>(),
                signingKey!, issuer!, audience!,
                builder.Configuration["AppleSignIn:Audience"] ?? string.Empty,
                appleKeys,
                serviceProvider.GetService<IAppleAccountTokenRevoker>()));
        builder.Services.AddHttpClient("ResendEmailDelivery", client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.AddHttpClient("FirstPartyEmailChallengeRelay", client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.AddScoped<IEmailChallengeDelivery>(serviceProvider =>
        {
            var resendApiKey = new[]
                {
                    builder.Configuration["Resend:ApiKey"],
                    builder.Configuration["RESEND_API_KEY"],
                    builder.Configuration["EmailChallenges:ResendApiKey"]
                }
                .FirstOrDefault(key => !string.IsNullOrWhiteSpace(key));

            if (!string.IsNullOrWhiteSpace(resendApiKey))
            {
                var fromEmail = builder.Configuration["Resend:FromEmail"]
                    ?? builder.Configuration["EmailChallenges:FromEmail"];
                var resendClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("ResendEmailDelivery");
                return new ResendEmailChallengeDelivery(resendClient, resendApiKey, fromEmail);
            }

            return new FirstPartyEmailChallengeDelivery(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("FirstPartyEmailChallengeRelay"),
                builder.Configuration["EmailChallenges:RelayEndpoint"],
                builder.Configuration["EmailChallenges:RelayBearerToken"]);
        });
        builder.Services.AddSingleton<IEmailReceiptTokenIssuer>(_ => new SignedEmailReceiptTokenIssuer(
            builder.Configuration["EmailReceipts:ActiveKeyId"] ?? string.Empty,
            builder.Configuration["EmailReceipts:PrivateKeyPem"] ?? string.Empty,
            builder.Configuration["EmailReceipts:Issuer"] ?? string.Empty,
            builder.Configuration["EmailReceipts:Audience"] ?? string.Empty,
            TimeProvider.System));
        var emailChallengeHmacKeys = builder.Configuration.GetSection("EmailChallenges:HmacKeys").GetChildren()
            .Where(section => !string.IsNullOrWhiteSpace(section.Value))
            .ToDictionary(section => section.Key, section => section.Value!, StringComparer.Ordinal);
        builder.Services.AddScoped<IEmailChallengeUseCase>(serviceProvider => new PostgresEmailChallengeUseCase(
            serviceProvider.GetRequiredService<PostgresDbContext>(),
            serviceProvider.GetRequiredService<IEmailChallengeDelivery>(),
            serviceProvider.GetRequiredService<IEmailReceiptTokenIssuer>(),
            serviceProvider.GetRequiredService<TimeProvider>(),
            emailChallengeHmacKeys,
            builder.Configuration["EmailChallenges:ActiveHmacKeyId"] ?? string.Empty));
        builder.Services.AddHealthChecks().AddCheck<PostgreSqlReadinessCheck>("postgresql");
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        var tokenId = context.Principal?.FindFirst("jti")?.Value;
                        var sessionId = context.Principal?.FindFirst("sid")?.Value;
                        if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty ||
                            !Guid.TryParse(tokenId, out var parsedTokenId) || parsedTokenId == Guid.Empty ||
                            !Guid.TryParse(sessionId, out var parsedSessionId) || parsedSessionId == Guid.Empty)
                        {
                            context.Fail("The access token is missing required identity claims.");
                            return;
                        }

                        var authentication = context.HttpContext.RequestServices
                            .GetRequiredService<IAccountAuthenticationUseCase>();
                        if (await authentication.IsAccessTokenRevokedAsync(tokenId!, sessionId!, context.HttpContext.RequestAborted)
                                .ConfigureAwait(false))
                            context.Fail("The access token has been revoked.");
                    }
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.Configure<ExceptionHandlerOptions>(options =>
            options.SuppressDiagnosticsCallback = _ => true);

        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
            if (exceptionFeature is null)
                return;

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Headers[RequestIdHeader] = context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected server error occurred.",
                Extensions = { ["requestId"] = context.TraceIdentifier }
            }, options: (JsonSerializerOptions?)null, contentType: "application/problem+json");
        }));
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[RequestIdHeader] = context.TraceIdentifier;
                return Task.CompletedTask;
            });
            await next(context);
        });
        if (trustedProxyAddresses.Length > 0)
            app.UseForwardedHeaders();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet(LiveRoute, () => Results.Ok(new HealthResponse("live", ApiVersion)))
            .AllowAnonymous();
        app.MapHealthChecks(ReadyRoute).AllowAnonymous();
        app.MapGet(SessionRoute, (HttpContext context) => Results.Ok(new SessionResponse(
            IsAuthenticated: context.User.Identity?.IsAuthenticated == true,
            UserId: context.User.FindFirst("sub")?.Value,
            ApiVersion: ApiVersion)))
            .RequireAuthorization();
        app.MapGet(ProfileUnitsRoute, async (
                HttpContext context,
                ServerProfileUnitsUseCase profileUnits,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetAuthenticatedUserId(context, out var userId))
                    return Results.Unauthorized();

                var settings = await profileUnits.LoadAsync(userId, cancellationToken).ConfigureAwait(false);
                return settings is null ? Results.NotFound() : Results.Ok(settings);
            })
            .RequireAuthorization();
        app.MapPut(ProfileUnitsRoute, async (
                UpdateProfileUnitsRequest request,
                HttpContext context,
                ServerProfileUnitsUseCase profileUnits,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetAuthenticatedUserId(context, out var userId))
                    return Results.Unauthorized();

                var result = await profileUnits.SaveAsync(new SaveServerProfileUnitsRequest(
                    userId,
                    request.ExpectedVersion,
                    request.UseMetricSystem,
                    request.BodyWeightUnit,
                    request.FoodMassUnit,
                    request.HeightUnit,
                    request.DistanceUnit,
                    request.EnergyUnit), cancellationToken).ConfigureAwait(false);

                return result.Status switch
                {
                    SaveServerProfileUnitsStatus.Saved => Results.Ok(new { result.Version }),
                    SaveServerProfileUnitsStatus.ProfileMissing => Results.NotFound(),
                    SaveServerProfileUnitsStatus.VersionConflict => Results.Conflict(new { result.Version }),
                    _ => Results.BadRequest(new AccountRegistrationError("invalidProfileUnitsRequest"))
                };
            })
            .RequireAuthorization();
        app.MapGet("/api/v1/profile", async (
            HttpContext context, ServerProfileUseCase profile, CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedUserId(context, out var userId)) return Results.Unauthorized();
            var snapshot = await profile.LoadAsync(userId, cancellationToken).ConfigureAwait(false);
            return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
        }).RequireAuthorization();
        app.MapPut("/api/v1/profile", async (
            UpdateServerProfileBody request, HttpContext context, ServerProfileUseCase profile, CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedUserId(context, out var userId)) return Results.Unauthorized();
            var result = await profile.UpdateProfileAsync(new UpdateServerProfileRequest(userId, request.ExpectedVersion,
                request.Name, request.DateOfBirth, request.SetDateOfBirth, request.Height, request.Weight, request.Goal,
                request.WeightGoalPace, request.TrainingExperienceLevel,
                request.CycleLength, request.DietaryTags, request.EquipmentLevel,
                request.ReproductiveContextJson, request.ProfileImagePath), cancellationToken).ConfigureAwait(false);
            return MapProfileWrite(result);
        }).RequireAuthorization();
        app.MapGet("/api/v1/profile/history", async (
            HttpContext context, ServerProfileUseCase profile, CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedUserId(context, out var userId)) return Results.Unauthorized();
            return Results.Ok(await profile.LoadHistoryAsync(userId, cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization();
        app.MapGet("/api/v1/profile/quiz-responses", async (
            HttpContext context, ServerProfileUseCase profile, CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedUserId(context, out var userId)) return Results.Unauthorized();
            return Results.Ok(await profile.LoadQuizResponsesAsync(userId, cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization();
        app.MapPut("/api/v1/profile/quiz-responses/{questionId:int}", async (
            int questionId, CorrectServerQuizResponseBody request, HttpContext context, ServerProfileUseCase profile, CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedUserId(context, out var userId)) return Results.Unauthorized();
            return MapProfileWrite(await profile.CorrectQuizResponseAsync(new CorrectServerQuizResponseRequest(
                userId, request.ExpectedVersion, questionId, request.AnswerId), cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization();
        app.MapPut("/api/v1/profile/nutrition-preferences", async (
            UpdateServerNutritionPreferencesBody request, HttpContext context, ServerProfileUseCase profile, CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedUserId(context, out var userId)) return Results.Unauthorized();
            return MapProfileWrite(await profile.UpdateNutritionPreferencesAsync(new UpdateServerNutritionPreferencesRequest(
                userId, request.ExpectedVersion, request.Goal, request.WeightGoalPace, request.DietaryTags), cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization();
        app.MapPut("/api/v1/profile/nutrition-settings", async (
            UpdateServerNutritionPreferencesBody request, HttpContext context, ServerProfileUseCase profile, CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedUserId(context, out var userId)) return Results.Unauthorized();
            return MapProfileWrite(await profile.UpdateNutritionPreferencesAsync(new UpdateServerNutritionPreferencesRequest(
                userId, request.ExpectedVersion, request.Goal, request.WeightGoalPace, request.DietaryTags), cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization();
        // Older clients must upgrade: a standalone write would change applied preferences
        // without atomically replacing the plan through the proposal lifecycle.
        app.MapPut(LegacyWorkoutPreferencesRoute, () => Results.Conflict(new
        {
            code = WorkoutPreferencesRequireProposalCode,
            message = WorkoutPreferencesRequireProposalMessage
        })).RequireAuthorization();
        app.MapPut("/api/v1/profile/nutrition-targets", async (
            UpdateServerNutritionTargetsBody request, HttpContext context, ServerProfileUseCase profile, CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedUserId(context, out var userId)) return Results.Unauthorized();
            var stringTargets = request.Targets?.ToDictionary(pair => pair.Key, pair => pair.Value.GetRawText()) ?? new Dictionary<string, string>();
            return MapProfileWrite(await profile.UpdateNutritionTargetsAsync(new UpdateServerNutritionTargetsRequest(
                userId, request.ExpectedVersion, stringTargets), cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization();
        app.MapGet("/api/v1/accounts/registration/quiz", async (
                GetRegistrationQuizUseCase quiz,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await quiz.ExecuteAsync(cancellationToken).ConfigureAwait(false));
                }
                catch (InvalidOperationException)
                {
                    return Results.Problem("The registration quiz catalog is unavailable.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .AllowAnonymous();

        app.MapPost("/api/v1/accounts/registration", async (
                AccountRegistrationBody request,
                ICompleteAccountRegistrationUseCase registration,
                CancellationToken cancellationToken) =>
            {
                if (request.Profile is { HasConflictingCycleLengths: true })
                    return Results.BadRequest(new AccountRegistrationError("conflictingCycleLengths"));

                var result = await registration.ExecuteAsync(request.ToInput(), cancellationToken).ConfigureAwait(false);
                return result.Status switch
                {
                    CompleteAccountRegistrationStatus.Created => Results.Created(
                        $"/api/v1/accounts/{result.UserId:D}",
                        new AccountRegistrationResponse(result.UserId!.Value, result.Succeeded)),
                    CompleteAccountRegistrationStatus.EmailAlreadyRegistered or
                        CompleteAccountRegistrationStatus.UsernameTaken or
                        CompleteAccountRegistrationStatus.EmailReceiptAlreadyUsed => Results.Conflict(
                            new AccountRegistrationError("registrationConflict")),
                    CompleteAccountRegistrationStatus.InvalidEmailVerification => Results.Unauthorized(),
                    CompleteAccountRegistrationStatus.PrivacyConsentRequired => Results.BadRequest(
                        new AccountRegistrationError("privacyConsentRequired")),
                    CompleteAccountRegistrationStatus.Failed => Results.Conflict(
                        new AccountRegistrationError(result.Status.ToString())),
                    _ => Results.BadRequest(new AccountRegistrationError(result.Status.ToString()))
                };
            })
            .AllowAnonymous();

        app.MapPost($"{AuthenticationRoute}/apple/nonce", async (
                IAccountAuthenticationUseCase authentication,
                CancellationToken cancellationToken) =>
            {
                var nonce = await authentication.CreateAppleNonceAsync(cancellationToken).ConfigureAwait(false);
                return Results.Ok(new AppleNonceResponse(nonce.ChallengeId, nonce.Nonce));
            })
            .AllowAnonymous();
        app.MapPost($"{AuthenticationRoute}/password", async (
                PasswordLoginRequest request,
                HttpContext httpContext,
                IAccountAuthenticationUseCase authentication,
                CancellationToken cancellationToken) =>
            SessionOperationHttpResults.OkOrUnauthorized(await authentication.LoginWithPasswordAsync(
                request, httpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken).ConfigureAwait(false)))
            .AllowAnonymous();
        app.MapPost($"{AuthenticationRoute}/apple", async (
                AppleLoginRequest request,
                IAccountAuthenticationUseCase authentication,
                CancellationToken cancellationToken) =>
            SessionOperationHttpResults.OkOrUnauthorized(await authentication.LoginWithAppleAsync(request, cancellationToken).ConfigureAwait(false)))
            .AllowAnonymous();
        app.MapPost($"{AuthenticationRoute}/refresh", async (
                RefreshSessionRequest request,
                IAccountAuthenticationUseCase authentication,
                CancellationToken cancellationToken) =>
            SessionOperationHttpResults.OkOrUnauthorized(await authentication.RefreshAsync(request, cancellationToken).ConfigureAwait(false)))
            .AllowAnonymous();
        app.MapPost($"{AuthenticationRoute}/password/reset", async (
                ResetPasswordRequest request,
                IAccountPasswordResetUseCase passwordReset,
                CancellationToken cancellationToken) =>
            {
                var result = await passwordReset.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
                return result switch
                {
                    PasswordResetStatus.Succeeded => Results.NoContent(),
                    PasswordResetStatus.InvalidRequest => Results.BadRequest(new AccountRegistrationError("invalidResetRequest")),
                    _ => Results.Unauthorized()
                };
            })
            .AllowAnonymous();
        app.MapPost($"{AuthenticationRoute}/password/change", async (
                ChangePasswordBody request,
                HttpContext context,
                IAccountAuthenticationUseCase authentication,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetAuthenticatedUserId(context, out var userId))
                    return Results.Unauthorized();
                var result = await authentication.ChangePasswordAsync(
                        userId, request.CurrentPassword, request.NewPassword, cancellationToken)
                    .ConfigureAwait(false);
                return result switch
                {
                    PasswordChangeStatus.Succeeded => Results.NoContent(),
                    PasswordChangeStatus.InvalidCurrentPassword => Results.BadRequest(new { error = "Current password is incorrect." }),
                    PasswordChangeStatus.AccountNotFound => Results.NotFound(new { error = "Account not found." }),
                    _ => Results.BadRequest(new { error = "Please provide a valid new password." })
                };
            })
            .RequireAuthorization();
        app.MapGet($"{AuthenticationRoute}/email-exists", async (
                string? email,
                IAccountAuthenticationUseCase authentication,
                CancellationToken cancellationToken) =>
            Results.Ok(await authentication.EmailExistsAsync(email ?? string.Empty, cancellationToken).ConfigureAwait(false)))
            .AllowAnonymous();
        app.MapGet($"{AuthenticationRoute}/username-exists", async (
                string? username,
                IAccountAuthenticationUseCase authentication,
                CancellationToken cancellationToken) =>
            Results.Ok(await authentication.UsernameExistsAsync(username ?? string.Empty, cancellationToken).ConfigureAwait(false)))
            .AllowAnonymous();
        app.MapPost("/api/v1/accounts/migration-claims", async (
                CreateMigrationClaimRequest request,
                HttpContext context,
                IAccountMigrationClaimUseCase migrationClaims,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetAuthenticatedUserId(context, out var destinationUserId))
                    return Results.Forbid();
                var claim = await migrationClaims.CreateAsync(destinationUserId, request, cancellationToken)
                    .ConfigureAwait(false);
                return claim is null
                    ? Results.BadRequest(new AccountRegistrationError("invalidMigrationClaim"))
                    : Results.Ok(claim);
            })
            .RequireAuthorization();
        app.MapPost("/api/v1/accounts/migration-claims/redeem", async (
                RedeemMigrationClaimRequest request,
                HttpContext context,
                IAccountMigrationClaimUseCase migrationClaims,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetAuthenticatedUserId(context, out var destinationUserId))
                    return Results.Forbid();
                var result = await migrationClaims.RedeemAsync(destinationUserId, request, cancellationToken)
                    .ConfigureAwait(false);
                return result.Succeeded
                    ? Results.Ok(result)
                    : Results.Conflict(new AccountRegistrationError("migrationClaimConflict"));
            })
            .RequireAuthorization();
        app.MapDelete("/api/v1/accounts/me", async (
                [FromBody] AccountDeletionRequest request,
                HttpContext context,
                ServerAccountDeletionUseCase accountDeletion,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetAuthenticatedUserId(context, out var userId))
                    return Results.Forbid();
                var result = await accountDeletion.DeleteAccountAsync(userId, cancellationToken, request.AuthorizationCode)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .RequireAuthorization();
        app.MapPost($"{AuthenticationRoute}/revoke", async (
                RevokeSessionBody request,
                HttpContext context,
                IAccountAuthenticationUseCase authentication,
                CancellationToken cancellationToken) =>
            {
                var sub = context.User.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                    return Results.Forbid();
                var tokenId = context.User.FindFirst("jti")?.Value;
                var expirationClaim = context.User.FindFirst("exp")?.Value;
                DateTimeOffset? accessExpiration = long.TryParse(expirationClaim, out var expirationSeconds)
                    ? DateTimeOffset.FromUnixTimeSeconds(expirationSeconds)
                    : null;
                return await authentication.RevokeAsync(
                    new RevokeSessionRequest(userId, request.RefreshToken, tokenId, accessExpiration), cancellationToken).ConfigureAwait(false)
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .RequireAuthorization();

        app.MapPost($"{EmailChallengeRoute}/start", async (
                StartEmailChallengeRequest request,
                IEmailChallengeUseCase emailChallenges,
                CancellationToken cancellationToken) =>
            MapChallengeStart(await emailChallenges.StartAsync(request, cancellationToken).ConfigureAwait(false)))
            .AllowAnonymous();
        app.MapPost($"{EmailChallengeRoute}/resend", async (
                ResendEmailChallengeRequest request,
                IEmailChallengeUseCase emailChallenges,
                CancellationToken cancellationToken) =>
            MapChallengeStart(await emailChallenges.ResendAsync(request, cancellationToken).ConfigureAwait(false)))
            .AllowAnonymous();
        app.MapPost($"{EmailChallengeRoute}/verify", async (
                VerifyEmailChallengeRequest request,
                IEmailChallengeUseCase emailChallenges,
                CancellationToken cancellationToken) =>
            MapChallengeVerification(await emailChallenges.VerifyAsync(request, cancellationToken).ConfigureAwait(false)))
            .AllowAnonymous();

        app.MapFoodCatalogEndpoints();
        app.MapNutritionDiaryEndpoints();
        app.MapNutritionBatchAndCopyEndpoints();
        app.MapRecipeEndpoints();
        app.MapWorkoutPlanningEndpoints();
        app.MapWorkoutProgressEndpoints();
        app.MapWorkoutActiveSessionEndpoints();
        app.MapWorkoutArchiveEndpoints();
        app.MapWorkoutInjuryEndpoints();
        app.MapWorkoutPlanAndSessionEndpoints();
        app.MapCycleHistoryEndpoints();
        app.MapCyclePhaseLogEndpoints();
        app.MapServerOverviewEndpoints();
        app.MapCheckInAndInjuryEndpoints();
        app.MapDashboardEndpoints();
        app.MapPhotoVaultEndpoints();
        app.MapHydrationProgressAndFastingEndpoints();

        return app;
    }

    private sealed record HealthResponse(string Status, string ApiVersion);
    /// <summary>Server requests refresh their diary through API reads, so there are no process-local observers to notify.</summary>
    private sealed class ServerNutritionBatchAndCopyChangeNotifier : INutritionBatchAndCopyChangeNotifier
    {
        public void NotifyNutritionChanged(Guid userId, DateOnly date)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("A nutrition change requires an owner.", nameof(userId));
            if (date == default)
                throw new ArgumentException("A nutrition change requires a diary date.", nameof(date));
        }
    }

    private sealed record SessionResponse(bool IsAuthenticated, string? UserId, string ApiVersion);
    private sealed record ChangePasswordBody(string CurrentPassword, string NewPassword);
    private sealed record AccountRegistrationResponse(Guid UserId, bool OnboardingComplete);
    private sealed record AccountRegistrationError(string Code);
    private sealed record AppleNonceResponse(string ChallengeId, string Nonce);
    private sealed record RevokeSessionBody(string RefreshToken);
    private sealed record UpdateProfileUnitsRequest(
        int ExpectedVersion,
        bool UseMetricSystem,
        string? BodyWeightUnit,
        string? FoodMassUnit,
        string? HeightUnit,
        string? DistanceUnit,
        string? EnergyUnit);
    private sealed record UpdateServerProfileBody(
        int ExpectedVersion, string? Name, DateOnly? DateOfBirth, bool SetDateOfBirth, decimal? Height, decimal? Weight,
        string? Goal, string? WeightGoalPace, string? TrainingExperienceLevel,
        int? CycleLength, string? DietaryTags, string? EquipmentLevel,
        string? ReproductiveContextJson = null, string? ProfileImagePath = null);
    private sealed record CorrectServerQuizResponseBody(int ExpectedVersion, int AnswerId);
    private sealed record UpdateServerNutritionPreferencesBody(int ExpectedVersion, string Goal, string WeightGoalPace, string DietaryTags);
    private sealed record UpdateServerWorkoutPreferencesBody(int ExpectedVersion, IReadOnlyList<int> ActivityTypes,
        IReadOnlyList<int> AvailableWeekdays, IReadOnlyDictionary<int, int> ActivitySessionDurationsMinutes,
        string? EquipmentLevel = null,
        IReadOnlyDictionary<int, int>? RequestedSessionsPerWeek = null,
        HashSet<int>? ExcludedExerciseIds = null);
    private sealed record UpdateServerNutritionTargetsBody(int ExpectedVersion, IReadOnlyDictionary<string, JsonElement> Targets);

    private static readonly JsonSerializerOptions ChallengeJsonOptions = new() { PropertyNamingPolicy = null };

    private static IResult MapChallengeStart(EmailChallengeOperationResult result) => result.Status switch
    {
        EmailChallengeStatus.Succeeded => Results.Json(new
        {
            ChallengeId = result.ChallengeId,
            ResendCooldownSeconds = result.ResendCooldownSeconds,
            ExpiresInSeconds = result.ExpiresInSeconds
        }, ChallengeJsonOptions),
        EmailChallengeStatus.RateLimited or EmailChallengeStatus.Cooldown => Results.StatusCode(StatusCodes.Status429TooManyRequests),
        EmailChallengeStatus.DeliveryUnavailable => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
        _ => Results.BadRequest(new AccountRegistrationError("invalidChallengeRequest"))
    };

    private static IResult MapChallengeVerification(EmailChallengeOperationResult result) => result.Status switch
    {
        EmailChallengeStatus.Succeeded => Results.Json(new
        {
            Verified = true,
            VerificationToken = result.VerificationToken,
            ReceiptExpiresInSeconds = result.ReceiptExpiresInSeconds
        }, ChallengeJsonOptions),
        EmailChallengeStatus.InvalidRequest => Results.BadRequest(new AccountRegistrationError("invalidChallengeRequest")),
        _ => Results.Unauthorized()
    };

    private static bool TryGetAuthenticatedUserId(HttpContext context, out Guid userId) =>
        Guid.TryParse(context.User.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;

    private static IResult MapProfileWrite(ServerProfileWriteResult result) => result.Status switch
    {
        ServerProfileWriteStatus.Saved => Results.Ok(new { result.Version }),
        ServerProfileWriteStatus.ProfileMissing => Results.NotFound(),
        ServerProfileWriteStatus.VersionConflict => Results.Conflict(new { result.Version }),
        ServerProfileWriteStatus.InvalidCatalogReference => Results.UnprocessableEntity(new AccountRegistrationError("invalidCatalogReference")),
        _ => Results.BadRequest(new AccountRegistrationError("invalidProfileRequest"))
    };
}

internal static class SessionOperationHttpResults
{
    public static IResult OkOrUnauthorized(SessionOperationResult result) => result.Status switch
    {
        SessionOperationStatus.Succeeded when result.Tokens is not null => Results.Ok(result.Tokens),
        _ => Results.Unauthorized()
    };
}

internal sealed class PostgreSqlReadinessCheck(PostgresDbContext dbContext) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy()
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy();
        }
        catch
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy();
        }
    }
}
