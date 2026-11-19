using MuscleCuties.Contracts.Account;
using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.UseCases.Features.Account.Registration;

public interface ICompleteAccountOnboardingUseCase
{
    Task<CompleteAccountOnboardingResult> ExecuteAsync(
        Guid userId,
        CompleteAccountOnboardingRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICompleteAccountOnboardingStore
{
    Task<IReadOnlyList<AccountRegistrationQuizQuestion>> LoadQuizQuestionsAsync(
        CancellationToken cancellationToken);

    Task<CompleteAccountOnboardingResult> CompleteOnboardingAsync(
        Guid userId,
        CompleteAccountOnboardingRequest request,
        CancellationToken cancellationToken);
}

public sealed class CompleteAccountOnboardingUseCase(
    ICompleteAccountOnboardingStore store,
    TimeProvider timeProvider) : ICompleteAccountOnboardingUseCase
{
    public async Task<CompleteAccountOnboardingResult> ExecuteAsync(
        Guid userId,
        CompleteAccountOnboardingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty)
            return new CompleteAccountOnboardingResult(CompleteAccountOnboardingStatus.AccountNotFound);

        var profile = request.Profile;
        if (profile is null || profile.SelectedActivityTypes is null ||
            profile.SelectedActivityTypes.Count == 0 ||
            profile.SelectedActivityTypes.Any(activity => !Enum.IsDefined((WorkoutActivityType)activity)) ||
            !WorkoutPreferencesRule.HasAtLeastOneStrengthStyle(profile.SelectedActivityTypes))
            return new CompleteAccountOnboardingResult(CompleteAccountOnboardingStatus.InvalidProfile);

        if (request.QuizResponses is null || request.QuizResponses.Any(selection => selection is null))
            return new CompleteAccountOnboardingResult(CompleteAccountOnboardingStatus.InvalidQuiz);

        var profileValidation = OnboardingValidationRule.ValidateProfileSetup(
            new StagedProfileObservation(
                profile.Name,
                profile.BirthDate,
                profile.HeightCm,
                profile.WeightKg,
                profile.CycleLength),
            profile.SelectedActivityTypes,
            timeProvider.GetUtcNow().UtcDateTime);
        if (!profileValidation.IsValid)
            return new CompleteAccountOnboardingResult(
                CompleteAccountOnboardingStatus.InvalidProfile,
                ErrorMessage: profileValidation.ErrorMessage);

        var questions = await store.LoadQuizQuestionsAsync(cancellationToken).ConfigureAwait(false);
        var quizValidation = OnboardingValidationRule.ValidateQuizResponses(
            request.QuizResponses.Select(selection =>
                new QuizResponseObservation(selection.QuestionId, selection.AnswerId)).ToArray(),
            questions.Select(question => new QuizQuestionObservation(
                question.Id,
                question.Question,
                question.QuestionType,
                question.Answers.Select(answer => new QuizAnswerObservation(answer.Id, answer.MappedValue)).ToArray()))
                .ToArray());
        if (!quizValidation.IsValid)
            return new CompleteAccountOnboardingResult(
                CompleteAccountOnboardingStatus.InvalidQuiz,
                ErrorMessage: quizValidation.ErrorMessage);

        cancellationToken.ThrowIfCancellationRequested();
        return await store.CompleteOnboardingAsync(userId, request, cancellationToken).ConfigureAwait(false);
    }
}
