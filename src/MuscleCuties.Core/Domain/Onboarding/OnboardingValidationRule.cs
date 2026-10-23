using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Onboarding;

public sealed record StagedProfileObservation(
    string Name,
    DateTime BirthDate,
    float Height,
    float Weight,
    int CycleLength,
    int WorkoutDaysPerWeek);

public sealed record QuizResponseObservation(int QuizQuestionId, int QuizAnswerId);

public sealed record QuizAnswerObservation(int Id, int MappedValue);

public sealed record QuizQuestionObservation(
    int Id,
    string Question,
    QuizQuestionType QuestionType,
    IReadOnlyList<QuizAnswerObservation> Answers);

public sealed record OnboardingValidationResult(bool IsValid, string? ErrorMessage)
{
    public static OnboardingValidationResult Success()
    {
        return new OnboardingValidationResult(true, null);
    }

    public static OnboardingValidationResult Failure(string message)
    {
        return new OnboardingValidationResult(false, message);
    }
}

public static class OnboardingValidationRule
{
    public static OnboardingValidationResult ValidateStagedProfile(
        StagedProfileObservation? staged,
        string? username,
        DateTime now)
    {
        if (staged is null)
            return OnboardingValidationResult.Failure(
                "Please enter your profile measurements before completing onboarding.");

        var effectiveName = !string.IsNullOrWhiteSpace(staged.Name) ? staged.Name.Trim() : username?.Trim();
        if (string.IsNullOrWhiteSpace(effectiveName))
            return OnboardingValidationResult.Failure("Profile name is required.");

        var birthDateEligibility = BirthDateEligibilityRule.Assess(staged.BirthDate, now);
        if (birthDateEligibility == BirthDateEligibility.MissingOrInvalid)
            return OnboardingValidationResult.Failure("Please provide your date of birth.");
        if (birthDateEligibility == BirthDateEligibility.Underage)
            return OnboardingValidationResult.Failure(
                "Please provide a valid date of birth (must be at least 18 years old).");

        if (!float.IsFinite(staged.Height) || staged.Height < 50f || staged.Height > 280f)
            return OnboardingValidationResult.Failure("Please provide a valid height between 50 cm and 280 cm.");

        if (!float.IsFinite(staged.Weight) || staged.Weight < 20f || staged.Weight > 400f)
            return OnboardingValidationResult.Failure("Please provide a valid weight between 20 kg and 400 kg.");

        if (staged.CycleLength < CycleLengthLimits.Minimum || staged.CycleLength > CycleLengthLimits.Maximum)
            return OnboardingValidationResult.Failure("Please provide a valid cycle length between 18 and 60 days.");

        if (staged.WorkoutDaysPerWeek < 1 || staged.WorkoutDaysPerWeek > 7)
            return OnboardingValidationResult.Failure("Please provide valid workout days per week between 1 and 7.");

        return OnboardingValidationResult.Success();
    }

    public static OnboardingValidationResult ValidateQuizResponses(
        IReadOnlyList<QuizResponseObservation>? responses,
        IReadOnlyList<QuizQuestionObservation>? questions)
    {
        var structureResult = ValidateQuestionStructure(responses, questions, out var questionsById);
        if (!structureResult.IsValid)
            return structureResult;

        var responsesByQuestion = responses!
            .GroupBy(r => r.QuizQuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var answersResult = ValidateSelectedAnswers(responsesByQuestion, questionsById);
        if (!answersResult.IsValid)
            return answersResult;

        var requiredResult = ValidateRequiredQuestions(questions!, responsesByQuestion);
        if (!requiredResult.IsValid)
            return requiredResult;

        return ValidateGoalPaceAlignment(questions!, responsesByQuestion);
    }

    private static OnboardingValidationResult ValidateQuestionStructure(
        IReadOnlyList<QuizResponseObservation>? responses,
        IReadOnlyList<QuizQuestionObservation>? questions,
        out Dictionary<int, List<QuizQuestionObservation>> questionsById)
    {
        questionsById = [];

        if (responses is null || responses.Count == 0)
            return OnboardingValidationResult.Failure("Please answer all required onboarding questions to continue.");

        if (questions is null || questions.Count == 0)
            return OnboardingValidationResult.Failure("Onboarding questions are unavailable. Please try again.");

        questionsById = questions.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.ToList());
        if (questionsById.Values.Any(group => group.Count != 1))
            return OnboardingValidationResult.Failure("Onboarding questions are unavailable. Please try again.");

        if (questions.Any(q => q.Answers is null || q.Answers.Count == 0))
            return OnboardingValidationResult.Failure("Onboarding questions are unavailable. Please try again.");

        if (responses.GroupBy(r => (r.QuizQuestionId, r.QuizAnswerId)).Any(g => g.Count() > 1))
            return OnboardingValidationResult.Failure("Duplicate answers submitted.");

        return OnboardingValidationResult.Success();
    }

    private static OnboardingValidationResult ValidateSelectedAnswers(
        Dictionary<int, List<QuizResponseObservation>> responsesByQuestion,
        Dictionary<int, List<QuizQuestionObservation>> questionsById)
    {
        foreach (var (questionId, selected) in responsesByQuestion)
        {
            if (questionId <= 0 || !questionsById.TryGetValue(questionId, out var matches))
                return OnboardingValidationResult.Failure("One or more selected answers are invalid.");

            var question = matches[0];
            var validAnswerIds = question.Answers.Select(answer => answer.Id).ToHashSet();
            if (selected.Any(response =>
                    response.QuizAnswerId <= 0 || !validAnswerIds.Contains(response.QuizAnswerId)) ||
                selected.Select(response => response.QuizAnswerId).Distinct().Count() != selected.Count)
                return OnboardingValidationResult.Failure("One or more selected answers are invalid.");

            if (question.QuestionType != QuizQuestionType.DietaryPreference && selected.Count > 1)
                return OnboardingValidationResult.Failure("Conflicting answers selected for a single-choice question.");

            if (question.QuestionType == QuizQuestionType.DietaryPreference && selected.Count > 1)
            {
                var answers = question.Answers.Where(a => selected.Any(s => s.QuizAnswerId == a.Id)).ToList();
                if (answers.Any(a => a.MappedValue == (int)DietaryTag.None))
                    return OnboardingValidationResult.Failure("Conflicting answers selected for dietary preferences.");
            }
        }

        return OnboardingValidationResult.Success();
    }

    private static OnboardingValidationResult ValidateRequiredQuestions(
        IReadOnlyList<QuizQuestionObservation> questions,
        Dictionary<int, List<QuizResponseObservation>> responsesByQuestion)
    {
        ReadOnlySpan<QuizQuestionType> requiredTypes =
        [
            QuizQuestionType.CurrentCyclePhase,
            QuizQuestionType.Goal,
            QuizQuestionType.ExperienceLevel,
            QuizQuestionType.WorkoutDaysPerWeek,
            QuizQuestionType.DietaryPreference,
            QuizQuestionType.SessionDuration,
            QuizQuestionType.Equipment,
            QuizQuestionType.MenstrualPain,
            QuizQuestionType.MenstrualEnergy,
            QuizQuestionType.FollicularPain,
            QuizQuestionType.FollicularEnergy,
            QuizQuestionType.OvulatoryPain,
            QuizQuestionType.OvulatoryEnergy,
            QuizQuestionType.LutealPain,
            QuizQuestionType.LutealEnergy
        ];

        foreach (var reqType in requiredTypes)
        {
            var q = questions.FirstOrDefault(x => x.QuestionType == reqType);
            if (q is null)
                return OnboardingValidationResult.Failure("Onboarding questions are unavailable. Please try again.");

            if (!responsesByQuestion.TryGetValue(q.Id, out var qResponses) || qResponses.Count == 0)
                return OnboardingValidationResult.Failure($"Please answer: {q.Question}");
        }

        return OnboardingValidationResult.Success();
    }

    private static OnboardingValidationResult ValidateGoalPaceAlignment(
        IReadOnlyList<QuizQuestionObservation> questions,
        Dictionary<int, List<QuizResponseObservation>> responsesByQuestion)
    {
        var goalQ = questions.FirstOrDefault(x => x.QuestionType == QuizQuestionType.Goal);
        var paceQ = questions.FirstOrDefault(x => x.QuestionType == QuizQuestionType.GoalPace);
        if (goalQ == null || !responsesByQuestion.TryGetValue(goalQ.Id, out var goalResponses) ||
            goalResponses.Count == 0)
            return OnboardingValidationResult.Success();

        var selectedGoalAnswer = goalQ.Answers.FirstOrDefault(a => a.Id == goalResponses[0].QuizAnswerId);
        if (selectedGoalAnswer == null)
            return OnboardingValidationResult.Success();

        var isFatLossOrStrength = selectedGoalAnswer.MappedValue == (int)UserGoal.FatLoss ||
                                  selectedGoalAnswer.MappedValue == (int)UserGoal.Strength;
        if (isFatLossOrStrength)
        {
            if (paceQ is null || !responsesByQuestion.TryGetValue(paceQ.Id, out var paceResponses) ||
                paceResponses.Count == 0)
                return OnboardingValidationResult.Failure("Please select your goal pace.");

            if (paceResponses.Count != 1)
                return OnboardingValidationResult.Failure("Conflicting answers selected for a single-choice question.");
        }
        else
        {
            if (paceQ != null && responsesByQuestion.TryGetValue(paceQ.Id, out var paceResponses) &&
                paceResponses.Count > 0)
                return OnboardingValidationResult.Failure("Conflicting answers selected for goal pace.");
        }

        return OnboardingValidationResult.Success();
    }
}
