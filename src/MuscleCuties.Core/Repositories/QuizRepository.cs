using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Repositories;

public class QuizRepository(AppDatabase db) : BaseRepository<QuizQuestion>(db), IQuizRepository
{
    public async Task<List<QuizQuestion>> GetQuestionsWithAnswersAsync() =>
        await _db.QuizQuestions
            .Include(q => q.Answers.OrderBy(a => a.OrderIndex))
            .OrderBy(q => q.OrderIndex)
            .ToListAsync();

    public async Task<bool> AreQuestionsSeededAsync() =>
        await _db.QuizQuestions.AnyAsync();

    public async Task<string?> GetAnswerTextAsync(int answerId) =>
        await _db.QuizAnswers
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
