using MuscleCuties.Core.Models.Entities.Quiz;

namespace MuscleCuties.Core.Services.Quiz;

public interface IQuizService
{
    Task<List<QuizQuestion>> GetOnboardingQuestionsAsync();
    Task SaveAnswersAsync(int userId, List<UserQuizResponse> responses);
    Task<bool> IsOnboardingCompleteAsync(int userId);
}
