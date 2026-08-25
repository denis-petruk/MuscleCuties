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
        return await _quizRepository.GetQuestionsWithAnswersAsync();
    }

    public async Task SaveAnswersAsync(int userId, List<UserQuizResponse> responses)
    {
        if (responses.Count == 0)
            return;

        var questions = await _quizRepository.GetQuestionsWithAnswersAsync();
        var questionMap = questions.ToDictionary(q => q.Id);
        var answeredAt = DateTime.UtcNow;
        var selections = BuildValidSelections(userId, responses, questionMap, answeredAt);

        if (selections.Count == 0)
            return;

        var profile = await _userRepository.GetProfileAsync(userId);
        var isNew = profile == null;
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

        foreach (var selection in selections)
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
                case QuizQuestionType.DietaryPreference:
                    profile.DietaryTags = BuildDietaryTags(selection.Answer.MappedValue);
                    break;
                case QuizQuestionType.CurrentCyclePhase:
                    profile.CycleTrackingMode = CycleTrackingMode.ManualPhaseLogging;
                    profile.CurrentCyclePhase = MapEnum(selection.Answer.MappedValue, CyclePhase.Follicular);
                    break;
            }
        }

        if (profile.CycleTrackingMode is not CycleTrackingMode.ManualPhaseLogging)
            profile.CurrentCyclePhase = null;

        profile.UpdatedAt = answeredAt;

        if (isNew)
            await _userRepository.AddProfileAsync(profile);
        else
            await _userRepository.UpdateProfileAsync(profile);

        var snapshotReason = isNew ? "Initial" : "QuizRetake";
        var snapshot = new UserProfileSnapshot
        {
            UserId = userId,
            SnapshotReason = snapshotReason,
            ProfileJson = System.Text.Json.JsonSerializer.Serialize(BuildSnapshot(profile, selections, answeredAt)),
            CreatedAt = answeredAt
        };
        await _userRepository.AddSnapshotAsync(snapshot);

        var validResponses = selections.Select(selection => selection.Response).ToList();
        foreach (var response in validResponses)
            response.UserProfileSnapshotId = snapshot.Id;

        await _quizRepository.AddResponsesAsync(validResponses);

        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.UpdatedAt = answeredAt;
            await _userRepository.UpdateAsync(user);
        }
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
        return responses
            .Select(response => BuildSelection(userId, response, questionMap, answeredAt))
            .Where(selection => selection is not null)
            .Select(selection => selection!)
            .GroupBy(selection => selection.Question.Id)
            .Select(group => group.Last())
            .OrderBy(selection => selection.Question.OrderIndex)
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

    private static string BuildDietaryTags(int mappedValue)
    {
        var tag = MapEnum(mappedValue, DietaryTag.None);
        return tag is DietaryTag.None ? string.Empty : tag.ToString();
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
