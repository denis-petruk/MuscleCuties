using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Cycle;

namespace MuscleCuties.Core.Services.Quiz;

public class QuizService : IQuizService
{
    private readonly ICycleService _cycleService;
    private readonly IQuizRepository _quizRepository;
    private readonly IUserRepository _userRepository;

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

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    public async Task SaveAnswersAsync(int userId, List<UserQuizResponse> responses)
    {
        if (responses.Count == 0)
            return;

        var questions = await _quizRepository.GetQuestionsWithAnswersAsync();
        var questionMap = questions.ToDictionary(q => q.Id);
        var answeredAt = DateTime.UtcNow;
        var selections = QuizSelectionEvaluator.BuildValidSelections(userId, responses, questionMap, answeredAt);

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

        QuizSelectionEvaluator.ApplyQuizSelectionsToProfile(
            profile, selections, null, false, 0);

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
