global using WorkoutType = MuscleCuties.Contracts.Account.WorkoutType;
global using InitialWorkoutPlanExerciseInput = MuscleCuties.Contracts.Account.InitialWorkoutPlanExerciseInput;
global using InitialWorkoutPlanDayInput = MuscleCuties.Contracts.Account.InitialWorkoutPlanDayInput;
global using CompleteAccountRegistrationRequest = MuscleCuties.Contracts.Account.CompleteAccountRegistrationRequest;
global using CompleteAccountRegistrationStatus = MuscleCuties.Contracts.Account.CompleteAccountRegistrationStatus;
global using CompleteAccountRegistrationResult = MuscleCuties.Contracts.Account.CompleteAccountRegistrationResult;

using MuscleCuties.Contracts.Onboarding;

namespace MuscleCuties.UseCases.Features.Account.Registration.Contracts;

public interface ICompleteAccountRegistrationUseCase
{
    Task<CompleteAccountRegistrationResult> ExecuteAsync(
        CompleteAccountRegistrationRequest request,
        CancellationToken cancellationToken = default);
}
