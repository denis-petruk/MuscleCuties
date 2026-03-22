using MuscleCuties.Models;
using MuscleCuties.Models.Enums;
using MuscleCuties.Repositories;

namespace MuscleCuties.Services;

public class QuizService : IQuizService
{
    private readonly IUserRepository _userRepository;
    private readonly IQuizRepository _quizRepository;

    public QuizService(IUserRepository userRepository, IQuizRepository quizRepository)
    {
        _userRepository = userRepository;
        _quizRepository = quizRepository;
    }

    public async Task<List<QuizQuestion>> GetOnboardingQuestionsAsync()
    {
        await SeedIfEmptyAsync();
        return await _quizRepository.GetQuestionsWithAnswersAsync();
    }

    public async Task SaveAnswersAsync(int userId, List<UserQuizResponse> responses)
    {
        var profile = await _userRepository.GetProfileAsync(userId);
        var isNew = profile == null;
        profile ??= new UserProfile { UserId = userId, Name = string.Empty };

        foreach (var response in responses)
        {
            var answerText = await _quizRepository.GetAnswerTextAsync(response.AnswerId);
            if (answerText == null) continue;

            switch (response.QuestionId)
            {
                case 1:
                    profile.Goal = answerText switch
                    {
                        "Fat Loss" => UserGoal.FatLoss,
                        "Muscle Tone" => UserGoal.MuscleTone,
                        "Strength" => UserGoal.Strength,
                        _ => UserGoal.MaintainHealth
                    };
                    break;

                case 2:
                    profile.WorkoutDaysPerWeek = answerText switch
                    {
                        "1-2" => 2,
                        "3-4" => 4,
                        "5+" => 5,
                        _ => 3
                    };
                    break;

                case 3:
                    profile.CycleLength = answerText switch
                    {
                        "Less than 25 days" => 24,
                        "25-30 days" => 28,
                        "31-35 days" => 33,
                        _ => 28
                    };
                    break;
            }

            response.UserId = userId;
            response.AnsweredAt = DateTime.UtcNow;
        }

        if (isNew)
            await _userRepository.AddProfileAsync(profile);
        else
            await _userRepository.UpdateProfileAsync(profile);

        await _quizRepository.AddResponsesAsync(responses);
        await MarkOnboardingCompleteAsync(userId);
    }

    public async Task<bool> IsOnboardingCompleteAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        return user?.IsOnboardingComplete ?? false;
    }

    private async Task MarkOnboardingCompleteAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return;

        user.IsOnboardingComplete = true;
        await _userRepository.UpdateAsync(user);
    }

    private async Task SeedIfEmptyAsync()
    {
        if (await _quizRepository.AreQuestionsSeededAsync()) return;

        var questions = new List<QuizQuestion>
        {
            new() { OrderIndex = 1, Question = "What is your main goal?",
                Answers = [
                    new() { Text = "Fat Loss", OrderIndex = 1 },
                    new() { Text = "Muscle Tone", OrderIndex = 2 },
                    new() { Text = "Strength", OrderIndex = 3 },
                    new() { Text = "Maintain Health", OrderIndex = 4 }
                ]},
            new() { OrderIndex = 2, Question = "How many days per week do you train?",
                Answers = [
                    new() { Text = "1-2", OrderIndex = 1 },
                    new() { Text = "3-4", OrderIndex = 2 },
                    new() { Text = "5+", OrderIndex = 3 }
                ]},
            new() { OrderIndex = 3, Question = "How long is your average cycle?",
                Answers = [
                    new() { Text = "Less than 25 days", OrderIndex = 1 },
                    new() { Text = "25-30 days", OrderIndex = 2 },
                    new() { Text = "31-35 days", OrderIndex = 3 },
                    new() { Text = "Irregular", OrderIndex = 4 }
                ]}
        };

        await _quizRepository.AddRangeQuestionsAsync(questions);
    }
}
