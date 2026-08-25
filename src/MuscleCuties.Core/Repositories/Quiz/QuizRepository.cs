using MuscleCuties.Core.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Quiz;

namespace MuscleCuties.Core.Repositories.Quiz;

public class QuizRepository(AppDatabase db) : BaseRepository<QuizQuestion>(db), IQuizRepository
{
    private const int CurrentAnswerOrderLimit = 1_000;

    public async Task<List<QuizQuestion>> GetQuestionsWithAnswersAsync() =>
        await _db.QuizQuestions
            .AsNoTracking()
            .Where(q => q.QuestionType != QuizQuestionType.CycleTrackingMode)
            .Include(q => q.Answers
                .Where(a => a.OrderIndex < CurrentAnswerOrderLimit)
                .OrderBy(a => a.OrderIndex))
            .OrderBy(q => q.OrderIndex)
            .ToListAsync();

    public async Task<bool> AreQuestionsSeededAsync() =>
        await _db.QuizQuestions.AnyAsync();

    public async Task<string?> GetAnswerTextAsync(int answerId) =>
        await _db.QuizAnswers
            .AsNoTracking()
            .Where(a => a.Id == answerId)
            .Select(a => a.Text)
            .FirstOrDefaultAsync();

    public async Task AddResponsesAsync(List<UserQuizResponse> responses)
    {
        await _db.UserQuizResponses.AddRangeAsync(responses);
        await _db.SaveChangesAsync();
    }

    public async Task AddRangeQuestionsAsync(List<QuizQuestion> questions)
    {
        await _db.QuizQuestions.AddRangeAsync(questions);
        await _db.SaveChangesAsync();
    }
}
