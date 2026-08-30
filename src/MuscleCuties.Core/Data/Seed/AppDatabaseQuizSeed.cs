using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Diagnostics;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Data;

public partial class AppDatabase
{
    private const int TemporaryQuizQuestionOrderIndex = 50_000;
    private const int TemporaryQuizAnswerOrderIndex = 10_000;
    private const int ObsoleteQuizAnswerOrderIndex = 1_000;

    private async Task SeedQuizQuestionsAsync()
    {
        AppDebugLog.Write("QuizSeed", "SeedQuizQuestions start.");
        var seedQuestions = BuildQuizQuestions();
        var seedQuestionTypes = seedQuestions
            .Select(question => question.QuestionType)
            .ToHashSet();
        var existingQuestions = await QuizQuestions
            .Where(question => seedQuestionTypes.Contains(question.QuestionType))
            .ToListAsync();
        AppDebugLog.Write(
            "QuizSeed",
            $"Seed question types={seedQuestionTypes.Count}, existing seeded question rows={existingQuestions.Count}.");

        if (existingQuestions.Count > 0)
        {
            var temporaryOrder = TemporaryQuizQuestionOrderIndex;
            foreach (var question in existingQuestions.OrderBy(question => question.Id))
                question.OrderIndex = temporaryOrder++;

            await SaveChangesAsync();
            AppDebugLog.Write("QuizSeed", "Moved existing quiz questions to temporary order range.");
        }

        var existingQuestionTypes = existingQuestions
            .Select(question => question.QuestionType)
            .ToHashSet();
        var missingQuestions = seedQuestions
            .Where(question => !existingQuestionTypes.Contains(question.QuestionType))
            .ToList();
        AppDebugLog.Write("QuizSeed", $"Missing seeded questions count={missingQuestions.Count}.");

        if (missingQuestions.Count > 0)
        {
            await QuizQuestions.AddRangeAsync(missingQuestions);
            await SaveChangesAsync();
            AppDebugLog.Write("QuizSeed", "Inserted missing quiz questions.");
        }

        await RefreshExistingQuizAnswersAsync(seedQuestions);
        AppDebugLog.Write("QuizSeed", "SeedQuizQuestions finished.");
    }

    private async Task RefreshExistingQuizAnswersAsync(IReadOnlyCollection<QuizQuestion> seedQuestions)
    {
        AppDebugLog.Write("QuizSeed", "RefreshExistingQuizAnswers start.");
        var questions = await QuizQuestions
            .Include(question => question.Answers)
            .ToListAsync();
        AppDebugLog.Write("QuizSeed", $"Questions loaded for answer refresh={questions.Count}.");

        foreach (var question in questions)
        {
            if (!seedQuestions.Any(seed => seed.QuestionType == question.QuestionType))
                continue;

            var temporaryOrder = TemporaryQuizAnswerOrderIndex;
            foreach (var answer in question.Answers.OrderBy(answer => answer.Id))
                answer.OrderIndex = temporaryOrder++;
        }

        await SaveChangesAsync();
        AppDebugLog.Write("QuizSeed", "Moved existing quiz answers to temporary order range.");

        foreach (var question in questions)
        {
            var seedQuestion = seedQuestions.FirstOrDefault(seed => seed.QuestionType == question.QuestionType);
            if (seedQuestion is null)
                continue;

            AppDebugLog.Write(
                "QuizSeed",
                $"Refreshing question type={question.QuestionType}, existingAnswers={question.Answers.Count}, seededAnswers={seedQuestion.Answers.Count}.");
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
                AppDebugLog.Write(
                    "QuizSeed",
                    $"Added missing answer mappedValue={seedAnswer.MappedValue} for question type={question.QuestionType}.");
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
        AppDebugLog.Write("QuizSeed", "RefreshExistingQuizAnswers finished.");
    }

    private static List<QuizQuestion> BuildQuizQuestions() =>
    [
        new()
        {
            Question = "Current cycle phase?",
            OrderIndex = 1,
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
            Question = "Main fitness goal?",
            OrderIndex = 2,
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
            Question = "Training experience?",
            OrderIndex = 3,
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
            Question = "Training days per week?",
            OrderIndex = 4,
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
            Question = "Dietary preference?",
            OrderIndex = 5,
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
        PainQuestion("Period discomfort?", 6, QuizQuestionType.MenstrualPain),
        EnergyQuestion("Period training energy?", 7, QuizQuestionType.MenstrualEnergy),
        PainQuestion("Follicular discomfort?", 9, QuizQuestionType.FollicularPain),
        EnergyQuestion("Follicular energy?", 10, QuizQuestionType.FollicularEnergy),
        PainQuestion("Ovulation discomfort?", 11, QuizQuestionType.OvulatoryPain),
        EnergyQuestion("Ovulation power?", 12, QuizQuestionType.OvulatoryEnergy),
        PainQuestion("Luteal symptoms?", 13, QuizQuestionType.LutealPain),
        EnergyQuestion("Luteal training energy?", 14, QuizQuestionType.LutealEnergy)
    ];

    private static QuizQuestion PainQuestion(string question, int orderIndex, QuizQuestionType questionType) => new()
    {
        Question = question,
        OrderIndex = orderIndex,
        QuestionType = questionType,
        Answers =
        [
            Answer("Barely there", 1, 1),
            Answer("Manageable", 2, 2),
            Answer("Noticeable", 3, 3),
            Answer("Rough", 4, 4),
            Answer("Stops my day", 5, 5)
        ]
    };

    private static QuizQuestion EnergyQuestion(string question, int orderIndex, QuizQuestionType questionType) => new()
    {
        Question = question,
        OrderIndex = orderIndex,
        QuestionType = questionType,
        Answers =
        [
            Answer("Couch-level", 1, 1),
            Answer("Slow but moving", 2, 2),
            Answer("Steady", 3, 3),
            Answer("Strong", 4, 4),
            Answer("Ready to push", 5, 5)
        ]
    };

    private static QuizAnswer Answer(string text, int orderIndex, int mappedValue) => new()
    {
        Text = text,
        OrderIndex = orderIndex,
        MappedValue = mappedValue
    };
}
