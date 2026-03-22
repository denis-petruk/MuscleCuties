using MuscleCuties.Models;

namespace MuscleCuties.Repositories;

public interface IQuizRepository : IRepository<QuizQuestion>
{
    Task<List<QuizQuestion>> GetQuestionsWithAnswersAsync();
    Task<bool> AreQuestionsSeededAsync();
    Task AddResponsesAsync(List<UserQuizResponse> responses);
    Task AddRangeQuestionsAsync(List<QuizQuestion> questions);
    Task<string?> GetAnswerTextAsync(int answerId);
}
