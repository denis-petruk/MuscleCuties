using MuscleCuties.Core.Diagnostics;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;

namespace MuscleCuties.Core.Services.Quiz;

public class QuizService : IQuizService
{
    private readonly IUserRepository _userRepository;
    private readonly IQuizRepository _quizRepository;

    public QuizService(
        IUserRepository userRepository,
        IQuizRepository quizRepository)
    {
        _userRepository = userRepository;
        _quizRepository = quizRepository;
    }

    public async Task<List<QuizQuestion>> GetOnboardingQuestionsAsync()
    {
        AppDebugLog.Write("QuizService", "GetOnboardingQuestions start.");
        var questions = await _quizRepository.GetQuestionsWithAnswersAsync();
        AppDebugLog.Write(
            "QuizService",
            $"GetOnboardingQuestions returned questions={questions.Count}, usable={questions.Count(question => question.Answers.Count > 0)}.");
        return questions;
    }

    public async Task SaveAnswersAsync(int userId, List<UserQuizResponse> responses)
    {
        AppDebugLog.Write("QuizService", $"SaveAnswers start userId={userId}, responseCount={responses.Count}.");
        if (responses.Count == 0)
        {
            AppDebugLog.Write("QuizService", "SaveAnswers skipped: no responses.");
            return;
        }

        var questions = await _quizRepository.GetQuestionsWithAnswersAsync();
        AppDebugLog.Write("QuizService", $"SaveAnswers loaded question map count={questions.Count}.");
        var questionMap = questions.ToDictionary(q => q.Id);
        var answeredAt = DateTime.UtcNow;
        var selections = BuildValidSelections(userId, responses, questionMap, answeredAt);

        if (selections.Count == 0)
        {
            AppDebugLog.Write("QuizService", "SaveAnswers skipped: no valid selections.");
            return;
        }

        var profile = await _userRepository.GetProfileAsync(userId);
        var isNew = profile == null;
        AppDebugLog.Write("QuizService", $"SaveAnswers profile is new={isNew}, validSelections={selections.Count}.");
        profile ??= new UserProfile
        {
            UserId = userId,
            Name = string.Empty,
            DateOfBirth = answeredAt.AddYears(-25),
            WeightGoalPace = WeightGoalPace.Steady,
            CycleTrackingMode = CycleTrackingMode.ManualPhaseLogging,
            WorkoutDaysPerWeek = 3,
            CycleLength = 28,
            UpdatedAt = answeredAt
        };

        var dietarySelections = selections
            .Where(selection => selection.Question.QuestionType is QuizQuestionType.DietaryPreference)
            .ToList();

        foreach (var selection in selections.Where(selection =>
                     selection.Question.QuestionType is not QuizQuestionType.DietaryPreference))
        {
            switch (selection.Question.QuestionType)
            {
                case QuizQuestionType.Goal:
                    profile.Goal = MapEnum(selection.Answer.MappedValue, UserGoal.MaintainHealth);
                    profile.WeightGoalPace = WeightGoalPace.Steady;
                    break;
                case QuizQuestionType.ExperienceLevel:
                    profile.TrainingExperienceLevel = MapTrainingExperience(selection.Answer.MappedValue);
                    break;
                case QuizQuestionType.WorkoutDaysPerWeek:
                    profile.WorkoutDaysPerWeek = Math.Clamp(selection.Answer.MappedValue, 0, 7);
                    break;
                case QuizQuestionType.CurrentCyclePhase:
                    profile.CycleTrackingMode = CycleTrackingMode.ManualPhaseLogging;
                    profile.CurrentCyclePhase = MapEnum(selection.Answer.MappedValue, CyclePhase.Follicular);
                    break;
            }
        }

        if (dietarySelections.Count > 0)
            profile.DietaryTags = BuildDietaryTags(dietarySelections.Select(selection => selection.Answer.MappedValue));

        if (profile.CycleTrackingMode is not CycleTrackingMode.ManualPhaseLogging)
            profile.CurrentCyclePhase = null;

        profile.UpdatedAt = answeredAt;

        if (isNew)
        {
            await _userRepository.AddProfileAsync(profile);
            AppDebugLog.Write("QuizService", "SaveAnswers inserted profile.");
        }
        else
        {
            await _userRepository.UpdateProfileAsync(profile);
            AppDebugLog.Write("QuizService", "SaveAnswers updated profile.");
        }

        var snapshotReason = isNew ? "Initial" : "QuizRetake";
        var snapshot = new UserProfileSnapshot
        {
            UserId = userId,
            SnapshotReason = snapshotReason,
            ProfileJson = System.Text.Json.JsonSerializer.Serialize(BuildSnapshot(profile, selections, answeredAt)),
            CreatedAt = answeredAt
        };
        await _userRepository.AddSnapshotAsync(snapshot);
        AppDebugLog.Write("QuizService", $"SaveAnswers snapshot created id={snapshot.Id}.");

        var validResponses = selections.Select(selection => selection.Response).ToList();
        foreach (var response in validResponses)
            response.UserProfileSnapshotId = snapshot.Id;

        await _quizRepository.AddResponsesAsync(validResponses);
        AppDebugLog.Write("QuizService", $"SaveAnswers stored responses count={validResponses.Count}.");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.IsOnboardingComplete = true;
            user.UpdatedAt = answeredAt;
            await _userRepository.UpdateAsync(user);
            AppDebugLog.Write("QuizService", "SaveAnswers marked onboarding complete.");
        }

        AppDebugLog.Write("QuizService", "SaveAnswers finished.");
    }

    public async Task<bool> IsOnboardingCompleteAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        return user?.IsOnboardingComplete ?? false;
    }

    private static List<QuizSelection> BuildValidSelections(
        int userId,
        IEnumerable<UserQuizResponse> responses,
        IReadOnlyDictionary<int, QuizQuestion> questionMap,
        DateTime answeredAt)
    {
        var selections = responses
            .Select(response => BuildSelection(userId, response, questionMap, answeredAt))
            .Where(selection => selection is not null)
            .Select(selection => selection!)
            .ToList();

        return selections
            .GroupBy(selection => selection.Question.Id)
            .SelectMany(NormalizeQuestionSelections)
            .OrderBy(selection => selection.Question.OrderIndex)
            .ThenBy(selection => selection.Answer.OrderIndex)
            .ToList();
    }

    private static QuizSelection? BuildSelection(
        int userId,
        UserQuizResponse response,
        IReadOnlyDictionary<int, QuizQuestion> questionMap,
        DateTime answeredAt)
    {
        if (!questionMap.TryGetValue(response.QuizQuestionId, out var question))
            return null;

        var answer = question.Answers.FirstOrDefault(a => a.Id == response.QuizAnswerId);
        if (answer is null)
            return null;

        response.UserId = userId;
        response.AnsweredAt = answeredAt;
        response.QuizQuestionId = question.Id;
        response.QuizAnswerId = answer.Id;

        return new QuizSelection(response, question, answer);
    }

    private static TEnum MapEnum<TEnum>(int value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.IsDefined(typeof(TEnum), value) ? (TEnum)(object)value : fallback;
    }

    private static TrainingExperienceLevel MapTrainingExperience(int mappedValue) =>
        MapEnum(mappedValue, TrainingExperienceLevel.Unknown);

    private static IEnumerable<QuizSelection> NormalizeQuestionSelections(IGrouping<int, QuizSelection> group)
    {
        var selections = group
            .GroupBy(selection => selection.Answer.Id)
            .Select(answerGroup => answerGroup.Last())
            .ToList();

        if (selections.First().Question.QuestionType is not QuizQuestionType.DietaryPreference)
            return [selections.Last()];

        var selectedTags = selections
            .Where(selection => selection.Answer.MappedValue != (int)DietaryTag.None)
            .ToList();

        return selectedTags.Count > 0 ? selectedTags : [selections.Last()];
    }

    private static string BuildDietaryTags(IEnumerable<int> mappedValues)
    {
        var tags = mappedValues
            .Select(value => MapEnum(value, DietaryTag.None))
            .Where(tag => tag is not DietaryTag.None)
            .Distinct()
            .Select(tag => tag.ToString())
            .ToList();

        return string.Join(",", tags);
    }

    private static QuizProfileSnapshot BuildSnapshot(
        UserProfile profile,
        IReadOnlyCollection<QuizSelection> selections,
        DateTime answeredAt)
    {
        return new QuizProfileSnapshot(
            profile.Name,
            profile.DateOfBirth,
            profile.Height,
            profile.Weight,
            profile.Goal.ToString(),
            profile.WeightGoalPace.ToString(),
            profile.TrainingExperienceLevel.ToString(),
            profile.CycleTrackingMode.ToString(),
            profile.CurrentCyclePhase?.ToString() ?? string.Empty,
            profile.WorkoutDaysPerWeek,
            profile.CycleLength,
            profile.DietaryTags,
            BuildCyclePhaseBaselines(selections),
            selections
                .Select(selection => new QuizAnswerSnapshot(
                    selection.Question.QuestionType.ToString(),
                    selection.Question.Question,
                    selection.Answer.Text,
                    selection.Answer.MappedValue))
                .ToList(),
            answeredAt);
    }

    private static CyclePhaseBaselineSnapshot BuildCyclePhaseBaselines(
        IReadOnlyCollection<QuizSelection> selections)
    {
        var values = selections
            .GroupBy(selection => selection.Question.QuestionType)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Answer.MappedValue);

        return new CyclePhaseBaselineSnapshot(
            new PhaseBaselineSnapshot(
                GetNullableValue(values, QuizQuestionType.MenstrualPain),
                GetNullableValue(values, QuizQuestionType.MenstrualEnergy)),
            new PhaseBaselineSnapshot(
                GetNullableValue(values, QuizQuestionType.FollicularPain),
                GetNullableValue(values, QuizQuestionType.FollicularEnergy)),
            new PhaseBaselineSnapshot(
                GetNullableValue(values, QuizQuestionType.OvulatoryPain),
                GetNullableValue(values, QuizQuestionType.OvulatoryEnergy)),
            new PhaseBaselineSnapshot(
                GetNullableValue(values, QuizQuestionType.LutealPain),
                GetNullableValue(values, QuizQuestionType.LutealEnergy)));
    }

    private static int? GetNullableValue(
        IReadOnlyDictionary<QuizQuestionType, int> values,
        QuizQuestionType questionType) =>
        values.TryGetValue(questionType, out var value) ? value : null;

    private sealed record QuizSelection(
        UserQuizResponse Response,
        QuizQuestion Question,
        QuizAnswer Answer);

    private sealed record QuizProfileSnapshot(
        string Name,
        DateTime DateOfBirth,
        float Height,
        float Weight,
        string Goal,
        string WeightGoalPace,
        string TrainingExperienceLevel,
        string CycleTrackingMode,
        string CurrentCyclePhase,
        int WorkoutDaysPerWeek,
        int CycleLength,
        string DietaryTags,
        CyclePhaseBaselineSnapshot CyclePhaseBaselines,
        IReadOnlyCollection<QuizAnswerSnapshot> QuizResponses,
        DateTime AnsweredAt);

    private sealed record QuizAnswerSnapshot(
        string QuestionType,
        string Question,
        string Answer,
        int MappedValue);

    private sealed record CyclePhaseBaselineSnapshot(
        PhaseBaselineSnapshot Menstrual,
        PhaseBaselineSnapshot Follicular,
        PhaseBaselineSnapshot Ovulatory,
        PhaseBaselineSnapshot Luteal);

    private sealed record PhaseBaselineSnapshot(int? Pain, int? Energy);
}
