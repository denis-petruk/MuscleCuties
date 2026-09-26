using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Repositories.Common;

namespace MuscleCuties.Core.Repositories.Quiz;

public class QuizRepository(AppDatabase db) : BaseRepository<QuizQuestion>(db), IQuizRepository
{
    private const int CurrentAnswerOrderLimit = 1_000;

    public async Task<List<QuizQuestion>> GetQuestionsWithAnswersAsync()
    {
        var questions = await _db.QuizQuestions
            .AsNoTracking()
            .Where(q => q.QuestionType != QuizQuestionType.CycleTrackingMode)
            .OrderBy(q => q.OrderIndex)
            .ThenBy(q => q.Id)
            .ToListAsync();

        if (questions.Count == 0)
            return questions;

        var questionIds = questions.Select(question => question.Id).ToList();
        var answers = await _db.QuizAnswers
            .AsNoTracking()
            .Where(answer => questionIds.Contains(answer.QuestionId))
            .OrderBy(answer => answer.QuestionId)
            .ThenBy(answer => answer.OrderIndex)
            .ThenBy(answer => answer.Id)
            .ToListAsync();

        var activeAnswersByQuestionId = answers
            .Where(answer => answer.OrderIndex < CurrentAnswerOrderLimit)
            .GroupBy(answer => answer.QuestionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var fallbackAnswersByQuestionId = answers
            .GroupBy(answer => answer.QuestionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var question in questions)
            question.Answers = activeAnswersByQuestionId.GetValueOrDefault(question.Id) ??
                               fallbackAnswersByQuestionId.GetValueOrDefault(question.Id) ??
                               [];

        return questions;
    }

    public async Task AddResponsesAsync(List<UserQuizResponse> responses)
    {
        await _db.UserQuizResponses.AddRangeAsync(responses);
        await _db.SaveChangesAsync();
    }

}
