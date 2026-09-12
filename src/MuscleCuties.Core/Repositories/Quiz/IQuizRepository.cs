using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Quiz;

public interface IQuizRepository : IRepository<QuizQuestion>
{
    Task<List<QuizQuestion>> GetQuestionsWithAnswersAsync();
    Task<bool> AreQuestionsSeededAsync();
    Task AddResponsesAsync(List<UserQuizResponse> responses);
    Task AddRangeQuestionsAsync(List<QuizQuestion> questions);
    Task<string?> GetAnswerTextAsync(int answerId);
}
