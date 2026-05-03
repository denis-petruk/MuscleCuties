using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Core.Services;

public class QuizService : IQuizService
{
    private readonly IUserRepository _userRepository;
    private readonly IQuizRepository _quizRepository;
    private readonly IWorkoutService _workoutService;

    public QuizService(IUserRepository userRepository, IQuizRepository quizRepository, IWorkoutService workoutService)
    {
        _userRepository = userRepository;
        _quizRepository = quizRepository;
        _workoutService = workoutService;
    }

    public async Task<List<QuizQuestion>> GetOnboardingQuestionsAsync()
    {
        return await _quizRepository.GetQuestionsWithAnswersAsync();
    }

    public async Task SaveAnswersAsync(int userId, List<UserQuizResponse> responses)
    {
        var questions = await _quizRepository.GetQuestionsWithAnswersAsync();
        var questionMap = questions.ToDictionary(q => q.Id);

        var profile = await _userRepository.GetProfileAsync(userId);
        var isNew = profile == null;
        profile ??= new UserProfile
        {
            UserId = userId,
            Name = string.Empty,
            DateOfBirth = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var response in responses)
        {
            response.UserId = userId;
            response.AnsweredAt = DateTime.UtcNow;

            if (!questionMap.TryGetValue(response.QuizQuestionId, out var question)) continue;
            var answer = question.Answers.FirstOrDefault(a => a.Id == response.QuizAnswerId);
            if (answer == null) continue;

            switch (question.QuestionType)
            {
                case QuizQuestionType.Goal:
                    profile.Goal = (UserGoal)answer.MappedValue;
                    break;
                case QuizQuestionType.WorkoutDaysPerWeek:
                    profile.WorkoutDaysPerWeek = answer.MappedValue;
                    break;
                case QuizQuestionType.DietaryPreference:
                    profile.DietaryTags = ((DietaryTag)answer.MappedValue).ToString();
                    break;
            }
        }

        profile.UpdatedAt = DateTime.UtcNow;

        if (isNew)
            await _userRepository.AddProfileAsync(profile);
        else
            await _userRepository.UpdateProfileAsync(profile);

        var snapshotReason = isNew ? "Initial" : "QuizRetake";
        var snapshot = new UserProfileSnapshot
        {
            UserId = userId,
            SnapshotReason = snapshotReason,
            ProfileJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                profile.Name,
                profile.DateOfBirth,
                profile.Height,
                profile.Weight,
                Goal = profile.Goal.ToString(),
                profile.WorkoutDaysPerWeek,
                profile.CycleLength,
                profile.DietaryTags
            }),
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddSnapshotAsync(snapshot);

        foreach (var response in responses)
            response.UserProfileSnapshotId = snapshot.Id;

        await _quizRepository.AddResponsesAsync(responses);

        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.IsOnboardingComplete = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
        }

        await _workoutService.GenerateUserPlansAsync(userId, profile.Goal, profile.WorkoutDaysPerWeek);
    }

    public async Task<bool> IsOnboardingCompleteAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        return user?.IsOnboardingComplete ?? false;
    }
}