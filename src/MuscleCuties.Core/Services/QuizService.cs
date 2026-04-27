using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Core.Services;

public class QuizService : IQuizService
{
    private readonly IUserRepository _userRepository;
    private readonly IQuizRepository _quizRepository;
    private readonly AppDatabase _db;

    public QuizService(IUserRepository userRepository, IQuizRepository quizRepository, AppDatabase db)
    {
        _userRepository = userRepository;
        _quizRepository = quizRepository;
        _db = db;
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
        profile ??= new UserProfile { UserId = userId, Name = string.Empty, DateOfBirth = DateTime.UtcNow };

        var baseline = await _userRepository.GetBaselineProfileAsync(userId);
        var isNewBaseline = baseline == null;
        baseline ??= new UserBaselineProfile { UserId = userId };

        foreach (var response in responses)
        {
            response.UserId = userId;
            response.AnsweredAt = DateTime.UtcNow;

            if (!questionMap.TryGetValue(response.QuestionId, out var question)) continue;
            var answer = question.Answers.FirstOrDefault(a => a.Id == response.AnswerId);
            if (answer == null) continue;

            switch (question.QuestionType)
            {
                case QuizQuestionType.Goal:
                    profile.Goal = (UserGoal)answer.MappedValue;
                    break;
                case QuizQuestionType.ExperienceLevel:
                    profile.ExperienceLevel = answer.MappedValue;
                    break;
                case QuizQuestionType.WorkoutDaysPerWeek:
                    profile.WorkoutDaysPerWeek = answer.MappedValue;
                    break;
                case QuizQuestionType.DietaryPreference:
                    profile.DietaryPreference = (DietaryTag)answer.MappedValue;
                    break;
                case QuizQuestionType.MenstrualPain:
                    baseline.PainMenstrual = answer.MappedValue;
                    break;
                case QuizQuestionType.MenstrualEnergy:
                    baseline.EnergyMenstrual = answer.MappedValue;
                    break;
                case QuizQuestionType.FollicularPain:
                    baseline.PainFollicular = answer.MappedValue;
                    break;
                case QuizQuestionType.FollicularEnergy:
                    baseline.EnergyFollicular = answer.MappedValue;
                    break;
                case QuizQuestionType.OvulatoryPain:
                    baseline.PainOvulatory = answer.MappedValue;
                    break;
                case QuizQuestionType.OvulatoryEnergy:
                    baseline.EnergyOvulatory = answer.MappedValue;
                    break;
                case QuizQuestionType.LutealPain:
                    baseline.PainLuteal = answer.MappedValue;
                    break;
                case QuizQuestionType.LutealEnergy:
                    baseline.EnergyLuteal = answer.MappedValue;
                    break;
            }
        }

        if (isNew)
            await _userRepository.AddProfileAsync(profile);
        else
            await _userRepository.UpdateProfileAsync(profile);

        if (isNewBaseline)
            await _userRepository.AddBaselineProfileAsync(baseline);
        else
            await _userRepository.UpdateBaselineProfileAsync(baseline);

        await _quizRepository.AddResponsesAsync(responses);

        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.IsOnboardingComplete = true;
            await _userRepository.UpdateAsync(user);
        }
    }

    public async Task<bool> IsOnboardingCompleteAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        return user?.IsOnboardingComplete ?? false;
    }
}
