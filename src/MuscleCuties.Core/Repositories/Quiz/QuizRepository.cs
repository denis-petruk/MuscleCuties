using MuscleCuties.Core.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Diagnostics;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Quiz;

namespace MuscleCuties.Core.Repositories.Quiz;

public class QuizRepository(AppDatabase db) : BaseRepository<QuizQuestion>(db), IQuizRepository
{
    private const int CurrentAnswerOrderLimit = 1_000;

    public async Task<List<QuizQuestion>> GetQuestionsWithAnswersAsync()
    {
        AppDebugLog.Write("QuizRepo", "GetQuestionsWithAnswers start.");
        var questions = await _db.QuizQuestions
            .AsNoTracking()
            .Where(q => q.QuestionType != QuizQuestionType.CycleTrackingMode)
            .OrderBy(q => q.OrderIndex)
            .ThenBy(q => q.Id)
            .ToListAsync();
        AppDebugLog.Write("QuizRepo", $"Loaded active questions count={questions.Count}.");

        if (questions.Count == 0)
        {
            AppDebugLog.Write("QuizRepo", "Returning empty questions list.");
            return questions;
        }

        var questionIds = questions.Select(question => question.Id).ToList();
        var answers = await _db.QuizAnswers
            .AsNoTracking()
            .Where(answer => questionIds.Contains(answer.QuestionId))
            .OrderBy(answer => answer.QuestionId)
            .ThenBy(answer => answer.OrderIndex)
            .ThenBy(answer => answer.Id)
            .ToListAsync();
        AppDebugLog.Write("QuizRepo", $"Loaded answers for active questions count={answers.Count}.");

        var activeAnswersByQuestionId = answers
            .Where(answer => answer.OrderIndex < CurrentAnswerOrderLimit)
            .GroupBy(answer => answer.QuestionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var fallbackAnswersByQuestionId = answers
            .GroupBy(answer => answer.QuestionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        AppDebugLog.Write(
            "QuizRepo",
            $"Active answer groups={activeAnswersByQuestionId.Count}, fallback answer groups={fallbackAnswersByQuestionId.Count}.");

        foreach (var question in questions)
        {
            question.Answers = activeAnswersByQuestionId.GetValueOrDefault(question.Id) ??
                               fallbackAnswersByQuestionId.GetValueOrDefault(question.Id) ??
                               [];
            AppDebugLog.Write(
                "QuizRepo",
                $"Question id={question.Id}, type={question.QuestionType}, order={question.OrderIndex}, answers={question.Answers.Count}.");
        }

        AppDebugLog.Write("QuizRepo", "GetQuestionsWithAnswers finished.");
        return questions;
    }

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
