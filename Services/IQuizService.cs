using MuscleCuties.Models;

namespace MuscleCuties.Services;

public interface IQuizService
{
    Task<List<QuizQuestion>> GetOnboardingQuestionsAsync();
    Task SaveAnswersAsync(int userId, List<UserQuizResponse> responses);
    Task<bool> IsOnboardingCompleteAsync(int userId);
}