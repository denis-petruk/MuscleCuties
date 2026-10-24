using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class OnboardingQuizValidationTests
{
    private const int FirstTestId = 1;
    private const int ExamplePhaseRating = 3;

    [Xunit.Fact]
    public void UnknownCyclePhaseIsValidAlongsideHistoricalPhaseRatings()
    {
        var (questions, responses) = BuildQuiz(CyclePhase.Unknown, includePhaseRatings: true);

        var result = OnboardingValidationRule.ValidateQuizResponses(responses, questions);

        Xunit.Assert.True(result.IsValid, result.ErrorMessage);
    }

    [Xunit.Fact]
    public void KnownCyclePhaseRequiresEveryPhaseRating()
    {
        var (questions, responses) = BuildQuiz(CyclePhase.Menstrual, includePhaseRatings: false);

        var result = OnboardingValidationRule.ValidateQuizResponses(responses, questions);

        Xunit.Assert.False(result.IsValid);
        Xunit.Assert.Contains("MenstrualPain", result.ErrorMessage ?? string.Empty);
    }

    [Xunit.Fact]
    public void MissingOrInvalidStrengthSessionDurationFailsQuizValidation()
    {
        var (questions, responses) = BuildQuiz(CyclePhase.Follicular, includePhaseRatings: true,
            includeSessionDuration: false);

        var missingResult = OnboardingValidationRule.ValidateQuizResponses(responses, questions);

        Xunit.Assert.False(missingResult.IsValid);
        Xunit.Assert.Contains("SessionDuration", missingResult.ErrorMessage ?? string.Empty);
    }

    private static (IReadOnlyList<QuizQuestionObservation> Questions,
        IReadOnlyList<QuizResponseObservation> Responses) BuildQuiz(
        CyclePhase phase, bool includePhaseRatings, bool includeSessionDuration = true)
    {
        var questions = new List<QuizQuestionObservation>();
        var responses = new List<QuizResponseObservation>();
        var questionId = FirstTestId;
        var answerId = FirstTestId;

        AddSingle(QuizQuestionType.CurrentCyclePhase, (int)phase,
            [(int)CyclePhase.Menstrual, (int)CyclePhase.Follicular, (int)CyclePhase.Ovulatory, (int)CyclePhase.Luteal, (int)CyclePhase.Unknown]);
        AddSingle(QuizQuestionType.Goal, (int)UserGoal.FatLoss, [(int)UserGoal.FatLoss]);
        AddSingle(QuizQuestionType.GoalPace, (int)WeightGoalPace.Steady,
            [(int)WeightGoalPace.Steady]);
        AddSingle(QuizQuestionType.ExperienceLevel, (int)TrainingExperienceLevel.Beginner,
            [(int)TrainingExperienceLevel.Beginner]);
        AddMultiple(QuizQuestionType.AvailableWeekdays, [(int)DayOfWeek.Monday, (int)DayOfWeek.Tuesday]);
        AddSingle(QuizQuestionType.DietaryPreference, (int)DietaryTag.None, [(int)DietaryTag.None]);
        AddSingle(QuizQuestionType.SessionDuration, ActivitySessionDurationRule.SixtyMinutes,
            [
                ActivitySessionDurationRule.ThirtyMinutes,
                ActivitySessionDurationRule.FortyFiveMinutes,
                ActivitySessionDurationRule.SixtyMinutes,
                ActivitySessionDurationRule.SeventyFiveMinutes,
                ActivitySessionDurationRule.NinetyMinutes
            ],
            includeSessionDuration);
        AddSingle(QuizQuestionType.Equipment, (int)Equipment.FullGym, [(int)Equipment.FullGym]);

        AddSingle(QuizQuestionType.MenstrualPain, ExamplePhaseRating, [ExamplePhaseRating], includePhaseRatings);
        AddSingle(QuizQuestionType.MenstrualEnergy, ExamplePhaseRating, [ExamplePhaseRating], includePhaseRatings);
        AddSingle(QuizQuestionType.FollicularPain, ExamplePhaseRating, [ExamplePhaseRating], includePhaseRatings);
        AddSingle(QuizQuestionType.FollicularEnergy, ExamplePhaseRating, [ExamplePhaseRating], includePhaseRatings);
        AddSingle(QuizQuestionType.OvulatoryPain, ExamplePhaseRating, [ExamplePhaseRating], includePhaseRatings);
        AddSingle(QuizQuestionType.OvulatoryEnergy, ExamplePhaseRating, [ExamplePhaseRating], includePhaseRatings);
        AddSingle(QuizQuestionType.LutealPain, ExamplePhaseRating, [ExamplePhaseRating], includePhaseRatings);
        AddSingle(QuizQuestionType.LutealEnergy, ExamplePhaseRating, [ExamplePhaseRating], includePhaseRatings);

        return (questions, responses);

        void AddSingle(QuizQuestionType type, int selectedValue, IReadOnlyList<int> values,
            bool addResponse = true)
        {
            var answers = values.Select(value => new QuizAnswerObservation(answerId++, value)).ToArray();
            var id = questionId++;
            questions.Add(new QuizQuestionObservation(id, type.ToString(), type, answers));
            if (addResponse)
            {
                var selected = answers.First(answer => answer.MappedValue == selectedValue);
                responses.Add(new QuizResponseObservation(id, selected.Id));
            }
        }

        void AddMultiple(QuizQuestionType type, IReadOnlyList<int> selectedValues)
        {
            var answers = selectedValues.Select(value => new QuizAnswerObservation(answerId++, value)).ToArray();
            var id = questionId++;
            questions.Add(new QuizQuestionObservation(id, type.ToString(), type, answers));
            responses.AddRange(answers.Select(answer => new QuizResponseObservation(id, answer.Id)));
        }
    }
}
