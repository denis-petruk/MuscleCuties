using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Onboarding;

public sealed record StagedProfileObservation(
    string Name,
    DateTime BirthDate,
    float Height,
    float Weight,
    int CycleLength);

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
    public const float MinimumHeightCentimeters = 50f;
    public const float MaximumHeightCentimeters = 280f;
    public const float MinimumWeightKilograms = 20f;
    public const float MaximumWeightKilograms = 400f;

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
                $"Please provide a valid date of birth (must be at least {BirthDateEligibilityRule.MinimumEligibleAgeYears} years old).");

        if (!float.IsFinite(staged.Height) || staged.Height < MinimumHeightCentimeters ||
            staged.Height > MaximumHeightCentimeters)
            return OnboardingValidationResult.Failure(
                $"Please provide a valid height between {MinimumHeightCentimeters:0} cm and {MaximumHeightCentimeters:0} cm.");

        if (!float.IsFinite(staged.Weight) || staged.Weight < MinimumWeightKilograms ||
            staged.Weight > MaximumWeightKilograms)
            return OnboardingValidationResult.Failure(
                $"Please provide a valid weight between {MinimumWeightKilograms:0} kg and {MaximumWeightKilograms:0} kg.");

        if (staged.CycleLength < CycleLengthLimits.Minimum || staged.CycleLength > CycleLengthLimits.Maximum)
            return OnboardingValidationResult.Failure(
                $"Please provide a valid cycle length between {CycleLengthLimits.Minimum} and {CycleLengthLimits.Maximum} days.");

        return OnboardingValidationResult.Success();
    }

    public static OnboardingValidationResult ValidateProfileSetup(
        StagedProfileObservation? staged,
        IReadOnlyList<int>? selectedActivityTypes,
        DateTime now)
    {
        var profileResult = ValidateStagedProfile(staged, username: null, now);
        if (!profileResult.IsValid)
            return profileResult;

        if (selectedActivityTypes is not { Count: > 0 } ||
            selectedActivityTypes.Any(value => !Enum.IsDefined((WorkoutActivityType)value)))
            return OnboardingValidationResult.Failure("Select at least one valid workout activity.");

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

            var allowsMultipleAnswers = question.QuestionType is QuizQuestionType.DietaryPreference or
                QuizQuestionType.AvailableWeekdays;
            if (!allowsMultipleAnswers && selected.Count > 1)
                return OnboardingValidationResult.Failure("Conflicting answers selected for a single-choice question.");

            if (question.QuestionType == QuizQuestionType.DietaryPreference && selected.Count > 1)
            {
                var answers = question.Answers.Where(a => selected.Any(s => s.QuizAnswerId == a.Id)).ToList();
                if (answers.Any(a => a.MappedValue == (int)DietaryTag.None))
                    return OnboardingValidationResult.Failure("Conflicting answers selected for dietary preferences.");
            }

            if (question.QuestionType == QuizQuestionType.AvailableWeekdays)
            {
                var selectedDays = question.Answers
                    .Where(answer => selected.Any(response => response.QuizAnswerId == answer.Id))
                    .Select(answer => answer.MappedValue)
                    .ToArray();
                if (selectedDays.Length is < WorkoutPreferencesRule.MinimumDaysPerWeek or > WorkoutPreferencesRule.MaximumDaysPerWeek ||
                    selectedDays.Any(day => !Enum.IsDefined(typeof(DayOfWeek), day)) ||
                    selectedDays.Distinct().Count() != selectedDays.Length)
                    return OnboardingValidationResult.Failure("Choose between 2 and 7 available weekdays.");
            }

            if (question.QuestionType == QuizQuestionType.WorkoutDaysPerWeek)
            {
                var selectedAnswer = question.Answers.FirstOrDefault(
                    answer => answer.Id == selected[0].QuizAnswerId);
                if (selectedAnswer is null ||
                    selectedAnswer.MappedValue is < WorkoutPreferencesRule.MinimumDaysPerWeek or > WorkoutPreferencesRule.MaximumDaysPerWeek)
                    return OnboardingValidationResult.Failure("Choose between 2 and 7 available weekdays.");
            }

            if (question.QuestionType == QuizQuestionType.SessionDuration)
            {
                var selectedAnswer = question.Answers.FirstOrDefault(
                    answer => answer.Id == selected[0].QuizAnswerId);
                if (selectedAnswer is null ||
                    !ActivitySessionDurationRule.TryResolveStrengthQuizSessionDurationMinutes(
                        selectedAnswer.MappedValue, out _))
                    return OnboardingValidationResult.Failure(
                        "Choose a valid strength session length (30, 45, 60, 75, or 90 minutes).");
            }
        }

        return OnboardingValidationResult.Success();
    }

    private static OnboardingValidationResult ValidateRequiredQuestions(
        IReadOnlyList<QuizQuestionObservation> questions,
        Dictionary<int, List<QuizResponseObservation>> responsesByQuestion)
    {
        var scheduleQuestionType = questions.Any(x => x.QuestionType == QuizQuestionType.AvailableWeekdays)
            ? QuizQuestionType.AvailableWeekdays
            : QuizQuestionType.WorkoutDaysPerWeek;

        ReadOnlySpan<QuizQuestionType> requiredTypes =
        [
            QuizQuestionType.CurrentCyclePhase,
            QuizQuestionType.Goal,
            QuizQuestionType.ExperienceLevel,
            scheduleQuestionType,
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
