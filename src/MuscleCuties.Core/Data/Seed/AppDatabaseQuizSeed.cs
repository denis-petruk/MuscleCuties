using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Data;

public partial class AppDatabase
{
    private const int TemporaryQuizAnswerOrderIndex = 10_000;
    private const int ObsoleteQuizAnswerOrderIndex = 1_000;

    private async Task SeedQuizQuestionsAsync()
    {
        var seedQuestions = BuildQuizQuestions();
        var existingQuestionTypes = await QuizQuestions
            .Select(question => question.QuestionType)
            .ToListAsync();
        var missingQuestions = seedQuestions
            .Where(question => !existingQuestionTypes.Contains(question.QuestionType))
            .ToList();

        if (missingQuestions.Count > 0)
        {
            await QuizQuestions.AddRangeAsync(missingQuestions);
            await SaveChangesAsync();
        }

        await RefreshExistingQuizAnswersAsync(seedQuestions);
    }

    private async Task RefreshExistingQuizAnswersAsync(IReadOnlyCollection<QuizQuestion> seedQuestions)
    {
        var questions = await QuizQuestions
            .Include(question => question.Answers)
            .ToListAsync();

        foreach (var question in questions)
        {
            if (!seedQuestions.Any(seed => seed.QuestionType == question.QuestionType))
                continue;

            var temporaryOrder = TemporaryQuizAnswerOrderIndex;
            foreach (var answer in question.Answers.OrderBy(answer => answer.Id))
                answer.OrderIndex = temporaryOrder++;
        }

        await SaveChangesAsync();

        foreach (var question in questions)
        {
            var seedQuestion = seedQuestions.FirstOrDefault(seed => seed.QuestionType == question.QuestionType);
            if (seedQuestion is null)
                continue;

            question.Question = seedQuestion.Question;
            question.OrderIndex = seedQuestion.OrderIndex;

            foreach (var seedAnswer in seedQuestion.Answers)
            {
                var answer = question.Answers.FirstOrDefault(existing =>
                    existing.MappedValue == seedAnswer.MappedValue);
                if (answer is not null)
                {
                    answer.Text = seedAnswer.Text;
                    answer.OrderIndex = seedAnswer.OrderIndex;
                    continue;
                }

                question.Answers.Add(new QuizAnswer
                {
                    Text = seedAnswer.Text,
                    OrderIndex = seedAnswer.OrderIndex,
                    MappedValue = seedAnswer.MappedValue
                });
            }

            var obsoleteOrder = ObsoleteQuizAnswerOrderIndex;
            var seededValues = seedQuestion.Answers
                .Select(answer => answer.MappedValue)
                .ToHashSet();
            foreach (var answer in question.Answers
                         .Where(answer => !seededValues.Contains(answer.MappedValue))
                         .OrderBy(answer => answer.Id))
            {
                answer.OrderIndex = obsoleteOrder++;
            }
        }

        await SaveChangesAsync();
    }

    private static List<QuizQuestion> BuildQuizQuestions() =>
    [
        new()
        {
            Question = "How would you like to track your cycle?",
            OrderIndex = -2,
            QuestionType = QuizQuestionType.CycleTrackingMode,
            Answers =
            [
                Answer("Manual", 1, (int)CycleTrackingMode.ManualPhaseLogging),
                Answer("Flo", 2, (int)CycleTrackingMode.FloConnector),
                Answer("Lunar", 3, (int)CycleTrackingMode.LunarConnector)
            ]
        },
        new()
        {
            Question = "What phase are you in today?",
            OrderIndex = -1,
            QuestionType = QuizQuestionType.CurrentCyclePhase,
            Answers =
            [
                Answer("Menstrual", 1, (int)CyclePhase.Menstrual),
                Answer("Follicular", 2, (int)CyclePhase.Follicular),
                Answer("Ovulatory", 3, (int)CyclePhase.Ovulatory),
                Answer("Luteal", 4, (int)CyclePhase.Luteal)
            ]
        },
        new()
        {
            Question = "What is your primary fitness goal?",
            OrderIndex = 1,
            QuestionType = QuizQuestionType.Goal,
            Answers =
            [
                Answer("Lose fat", 1, (int)UserGoal.FatLoss),
                Answer("Build muscle tone", 2, (int)UserGoal.MuscleTone),
                Answer("Get stronger", 3, (int)UserGoal.Strength),
                Answer("Maintain health", 4, (int)UserGoal.MaintainHealth)
            ]
        },
        new()
        {
            Question = "How experienced are you with structured training?",
            OrderIndex = 2,
            QuestionType = QuizQuestionType.ExperienceLevel,
            Answers =
            [
                Answer("Beginner", 1, 1),
                Answer("Intermediate", 2, 2),
                Answer("Advanced", 3, 3)
            ]
        },
        new()
        {
            Question = "How many days per week do you want to train?",
            OrderIndex = 3,
            QuestionType = QuizQuestionType.WorkoutDaysPerWeek,
            Answers =
            [
                Answer("2 days", 1, 2),
                Answer("3 days", 2, 3),
                Answer("4 days", 3, 4),
                Answer("5 days", 4, 5)
            ]
        },
        new()
        {
            Question = "Do you follow a dietary preference?",
            OrderIndex = 4,
            QuestionType = QuizQuestionType.DietaryPreference,
            Answers =
            [
                Answer("No preference", 1, (int)DietaryTag.None),
                Answer("Vegetarian", 2, (int)DietaryTag.Vegetarian),
                Answer("Vegan", 3, (int)DietaryTag.Vegan),
                Answer("Gluten-free", 4, (int)DietaryTag.GlutenFree),
                Answer("Lactose-free", 5, (int)DietaryTag.LactoseFree)
            ]
        },
        ScaleQuestion("How strong is your menstrual-phase discomfort usually?", 5, QuizQuestionType.MenstrualPain),
        ScaleQuestion("How is your menstrual-phase energy usually?", 6, QuizQuestionType.MenstrualEnergy),
        ScaleQuestion("How strong is your follicular-phase discomfort usually?", 7, QuizQuestionType.FollicularPain),
        ScaleQuestion("How is your follicular-phase energy usually?", 8, QuizQuestionType.FollicularEnergy),
        ScaleQuestion("How strong is your ovulatory-phase discomfort usually?", 9, QuizQuestionType.OvulatoryPain),
        ScaleQuestion("How is your ovulatory-phase energy usually?", 10, QuizQuestionType.OvulatoryEnergy),
        ScaleQuestion("How strong is your luteal-phase discomfort usually?", 11, QuizQuestionType.LutealPain),
        ScaleQuestion("How is your luteal-phase energy usually?", 12, QuizQuestionType.LutealEnergy)
    ];

    private static QuizQuestion ScaleQuestion(string question, int orderIndex, QuizQuestionType questionType) => new()
    {
        Question = question,
        OrderIndex = orderIndex,
        QuestionType = questionType,
        Answers =
        [
            Answer("Very low", 1, 1),
            Answer("Low", 2, 2),
            Answer("Moderate", 3, 3),
            Answer("High", 4, 4),
            Answer("Very high", 5, 5)
        ]
    };

    private static QuizAnswer Answer(string text, int orderIndex, int mappedValue) => new()
    {
        Text = text,
        OrderIndex = orderIndex,
        MappedValue = mappedValue
    };
}
