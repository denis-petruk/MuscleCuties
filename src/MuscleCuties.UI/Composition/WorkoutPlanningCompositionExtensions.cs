using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.Infrastructure.Account.SignIn;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UI.Services.Auth;
using MuscleCuties.UI.Services.Notifications;
using MuscleCuties.UI.Services.Workout;
using MuscleCuties.UseCases.Features.Account.Otp;
using MuscleCuties.UseCases.Features.Account.Otp.Contracts;
using MuscleCuties.UseCases.Features.Account.Recovery;
using MuscleCuties.UseCases.Features.Account.Recovery.Contracts;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Account.SignIn;
using MuscleCuties.UseCases.Features.Account.SignIn.Contracts;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Session;

namespace MuscleCuties.UI.Composition;

public static class WorkoutPlanningCompositionExtensions
{
    public static IServiceCollection RegisterWorkoutPlanningServices(this IServiceCollection services)
    {
        services.AddSingleton(WorkoutPlanningConfig.CreateDefault());

        services.AddSingleton<IReadinessEngine, DailyReadinessEngine>();
        services.AddSingleton<IGatingEngine, GatingEngine>();

        services.AddScoped<IDailyCheckInNotificationService>(sp =>
            new DailyCheckInNotificationService(
                sp.GetRequiredService<ILocalNotificationService>(),
                sp.GetRequiredService<IDailyCheckInStore>()));

        services.AddSingleton<IStrengthPlanProposalClientV020, CloudStrengthPlanProposalStoreV020>();
        services.AddSingleton<IStrengthStyleOfferClient, CloudStrengthStyleOfferClient>();
        services.AddSingleton<IWorkoutAccountCalendarClient, WorkoutAccountCalendarClient>();
        services.AddScoped<IWorkoutSessionActionStore, CloudWorkoutSessionActionStore>();
        services.AddScoped<IWorkoutSessionActionUseCase, WorkoutSessionActionUseCase>();

        // Account & Onboarding Lifecycle services
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

        return services;
    }
}
