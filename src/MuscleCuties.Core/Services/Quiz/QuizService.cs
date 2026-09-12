using System.Text.Json;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Workout.Planning;

namespace MuscleCuties.Core.Services.Quiz;

public class QuizService : IQuizService
{
    private readonly IQuizRepository _quizRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICycleService _cycleService;

    public QuizService(
        IUserRepository userRepository,
        IQuizRepository quizRepository,
        ICycleService cycleService)
    {
        _userRepository = userRepository;
        _quizRepository = quizRepository;
        _cycleService = cycleService;
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

        var dietarySelections = selections
            .Where(selection => selection.Question.QuestionType is QuizQuestionType.DietaryPreference)
            .ToList();

        foreach (var selection in selections.Where(selection =>
                     selection.Question.QuestionType is not QuizQuestionType.DietaryPreference))
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
                case QuizQuestionType.SessionDuration:
                    profile.SessionDurationMinutes = MapSessionDuration(selection.Answer.MappedValue);
                    break;
                case QuizQuestionType.Equipment:
                    profile.EquipmentLevel = MapEquipment(selection.Answer.MappedValue);
                    break;
            }

        if (dietarySelections.Count > 0)
            profile.DietaryTags = BuildDietaryTags(dietarySelections.Select(selection => selection.Answer.MappedValue));

        profile.PhaseBaselinesJson = BuildPhaseBaselinesJson(selections);

        if (profile.CycleTrackingMode is not CycleTrackingMode.ManualPhaseLogging)
            profile.CurrentCyclePhase = null;

        profile.UpdatedAt = answeredAt;

        if (isNew)
            await _userRepository.AddProfileAsync(profile);
        else
            await _userRepository.UpdateProfileAsync(profile);

        if (profile.CurrentCyclePhase is not null)
            await _cycleService.SetPhaseForDateAsync(
                userId, profile.CurrentCyclePhase.Value, DateTime.UtcNow, "Set from onboarding quiz");

        var snapshotReason = isNew ? "Initial" : "QuizRetake";
        var snapshot = new UserProfileSnapshot
        {
            UserId = userId,
            SnapshotReason = snapshotReason,
            ProfileJson = JsonSerializer.Serialize(BuildSnapshot(profile, selections, answeredAt)),
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
            user.IsOnboardingComplete = true;
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

    private static int MapSessionDuration(int mappedValue)
    {
        return mappedValue switch
        {
            1 => 30,
            2 => 45,
            3 => 60,
            4 => 75,
            5 => 90,
            _ => 60
        };
    }

    private static string MapEquipment(int mappedValue)
    {
        return mappedValue switch
        {
            1 => Equipment.FullGym.ToString(),
            2 => Equipment.HomeDumbbellsBands.ToString(),
            3 => Equipment.Bodyweight.ToString(),
            _ => Equipment.FullGym.ToString()
        };
    }

    private static string BuildPhaseBaselinesJson(IReadOnlyCollection<QuizSelection> selections)
    {
        var values = selections
            .Where(s => IsBaselineQuestion(s.Question.QuestionType))
            .GroupBy(s => s.Question.QuestionType)
            .ToDictionary(g => g.Key, g => g.Last().Answer.MappedValue);

        if (values.Count == 0)
            return string.Empty;

        var baselines = new CyclePhaseBaselines(
            new PhaseBaseline(
                values.GetValueOrDefault(QuizQuestionType.MenstrualPain, 3),
                values.GetValueOrDefault(QuizQuestionType.MenstrualEnergy, 2)),
            new PhaseBaseline(
                values.GetValueOrDefault(QuizQuestionType.FollicularPain, 1),
                values.GetValueOrDefault(QuizQuestionType.FollicularEnergy, 4)),
            new PhaseBaseline(
                values.GetValueOrDefault(QuizQuestionType.OvulatoryPain, 2),
                values.GetValueOrDefault(QuizQuestionType.OvulatoryEnergy, 5)),
            new PhaseBaseline(
                values.GetValueOrDefault(QuizQuestionType.LutealPain, 3),
                values.GetValueOrDefault(QuizQuestionType.LutealEnergy, 3)));

        return AdaptiveProfileMapper.SerializeBaselines(baselines);
    }

    private static bool IsBaselineQuestion(QuizQuestionType type) =>
        type is QuizQuestionType.MenstrualPain or QuizQuestionType.MenstrualEnergy
            or QuizQuestionType.FollicularPain or QuizQuestionType.FollicularEnergy
            or QuizQuestionType.OvulatoryPain or QuizQuestionType.OvulatoryEnergy
            or QuizQuestionType.LutealPain or QuizQuestionType.LutealEnergy;

    private static TEnum MapEnum<TEnum>(int value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.IsDefined(typeof(TEnum), value) ? (TEnum)(object)value : fallback;
    }

    private static TrainingExperienceLevel MapTrainingExperience(int mappedValue)
    {
        return MapEnum(mappedValue, TrainingExperienceLevel.Unknown);
    }

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
        QuizQuestionType questionType)
    {
        return values.TryGetValue(questionType, out var value) ? value : null;
    }

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
