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
        PainQuestion("During your period, how much does discomfort usually change your day?", 5, QuizQuestionType.MenstrualPain),
        EnergyQuestion("During your period, how much energy do you usually have for training?", 6, QuizQuestionType.MenstrualEnergy),
        PainQuestion("After bleeding eases, how much body discomfort is still hanging around?", 7, QuizQuestionType.FollicularPain),
        EnergyQuestion("In follicular days, how ready do you feel to build momentum?", 8, QuizQuestionType.FollicularEnergy),
        PainQuestion("Around ovulation, how much does your body ask you to hold back?", 9, QuizQuestionType.OvulatoryPain),
        EnergyQuestion("Around ovulation, how available does power usually feel?", 10, QuizQuestionType.OvulatoryEnergy),
        PainQuestion("In luteal days, how loud are PMS, soreness, or bloating?", 11, QuizQuestionType.LutealPain),
        EnergyQuestion("In luteal days, how much energy do you usually have left for training?", 12, QuizQuestionType.LutealEnergy)
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
