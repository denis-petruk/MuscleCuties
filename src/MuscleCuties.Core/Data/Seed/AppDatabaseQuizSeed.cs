using Microsoft.EntityFrameworkCore;
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
        var seedQuestions = BuildQuizQuestions();
        var seedQuestionTypes = seedQuestions
            .Select(question => question.QuestionType)
            .ToHashSet();
        var existingQuestions = await QuizQuestions
            .Where(question => seedQuestionTypes.Contains(question.QuestionType))
            .Include(question => question.Answers)
            .ToListAsync();

        // A warm launch must not rewrite every quiz row. Under SQLite's FULL
        // durability setting those otherwise harmless updates force several
        // encrypted commits before the first page can load.
        if (IsQuizSeedCurrent(seedQuestions, existingQuestions))
            return;

        if (existingQuestions.Count > 0)
        {
            var temporaryOrder = TemporaryQuizQuestionOrderIndex;
            foreach (var question in existingQuestions.OrderBy(question => question.Id))
                question.OrderIndex = temporaryOrder++;

            await SaveChangesAsync();
        }

        var existingQuestionTypes = existingQuestions
            .Select(question => question.QuestionType)
            .ToHashSet();
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

    private static bool IsQuizSeedCurrent(
        IReadOnlyCollection<QuizQuestion> seedQuestions,
        IReadOnlyCollection<QuizQuestion> existingQuestions)
    {
        if (existingQuestions.Count != seedQuestions.Count)
            return false;

        foreach (var seed in seedQuestions)
        {
            var current = existingQuestions.FirstOrDefault(question => question.QuestionType == seed.QuestionType);
            if (current is null || current.Question != seed.Question || current.OrderIndex != seed.OrderIndex)
                return false;

            foreach (var seedAnswer in seed.Answers)
            {
                if (!current.Answers.Any(answer => answer.MappedValue == seedAnswer.MappedValue &&
                                                   answer.Text == seedAnswer.Text &&
                                                   answer.OrderIndex == seedAnswer.OrderIndex))
                    return false;
            }

            // Old answers remain stored for historical responses but must be
            // outside the active answer order, as the normal seed path does.
            if (current.Answers.Any(answer =>
                    seed.Answers.All(seedAnswer => seedAnswer.MappedValue != answer.MappedValue) &&
                    answer.OrderIndex < ObsoleteQuizAnswerOrderIndex))
                return false;
        }

        return true;
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
                answer.OrderIndex = obsoleteOrder++;
        }

        await SaveChangesAsync();
    }

    private static List<QuizQuestion> BuildQuizQuestions()
    {
        return
        [
            new QuizQuestion
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
            new QuizQuestion
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
            new QuizQuestion
            {
                Question = "How fast do you want to progress?",
                OrderIndex = 3,
                QuestionType = QuizQuestionType.GoalPace,
                Answers =
                [
                    Answer("Steady", 1, (int)WeightGoalPace.Steady),
                    Answer("Aggressive", 2, (int)WeightGoalPace.Aggressive)
                ]
            },
            new QuizQuestion
            {
                Question = "Training experience?",
                OrderIndex = 4,
                QuestionType = QuizQuestionType.ExperienceLevel,
                Answers =
                [
                    Answer("Beginner", 1, 1),
                    Answer("Intermediate", 2, 2),
                    Answer("Advanced", 3, 3)
                ]
            },
            new QuizQuestion
            {
                Question = "Training days per week?",
                OrderIndex = 5,
                QuestionType = QuizQuestionType.WorkoutDaysPerWeek,
                Answers =
                [
                    Answer("2 days", 1, 2),
                    Answer("3 days", 2, 3),
                    Answer("4 days", 3, 4),
                    Answer("5 days", 4, 5)
                ]
            },
            new QuizQuestion
            {
                Question = "Dietary preference?",
                OrderIndex = 6,
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
            new QuizQuestion
            {
                Question = "Session length?",
                OrderIndex = 7,
                QuestionType = QuizQuestionType.SessionDuration,
                Answers =
                [
                    Answer("30 min", 1, 1),
                    Answer("45 min", 2, 2),
                    Answer("60 min", 3, 3),
                    Answer("75 min", 4, 4),
                    Answer("90 min", 5, 5)
                ]
            },
            new QuizQuestion
            {
                Question = "Available equipment?",
                OrderIndex = 8,
                QuestionType = QuizQuestionType.Equipment,
                Answers =
                [
                    Answer("Full gym", 1, 1),
                    Answer("Dumbbells and bands", 2, 2),
                    Answer("Bodyweight only", 3, 3)
                ]
            },
            PainQuestion("Period discomfort?", 9, QuizQuestionType.MenstrualPain),
            EnergyQuestion("Period training energy?", 10, QuizQuestionType.MenstrualEnergy),
            PainQuestion("Follicular discomfort?", 11, QuizQuestionType.FollicularPain),
            EnergyQuestion("Follicular energy?", 12, QuizQuestionType.FollicularEnergy),
            PainQuestion("Ovulation discomfort?", 13, QuizQuestionType.OvulatoryPain),
            EnergyQuestion("Ovulation power?", 14, QuizQuestionType.OvulatoryEnergy),
            PainQuestion("Luteal symptoms?", 15, QuizQuestionType.LutealPain),
            EnergyQuestion("Luteal training energy?", 16, QuizQuestionType.LutealEnergy)
        ];
    }

    private static QuizQuestion PainQuestion(string question, int orderIndex, QuizQuestionType questionType)
    {
        return new QuizQuestion
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
    }

    private static QuizQuestion EnergyQuestion(string question, int orderIndex, QuizQuestionType questionType)
    {
        return new QuizQuestion
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
    }

    private static QuizAnswer Answer(string text, int orderIndex, int mappedValue)
    {
        return new QuizAnswer
        {
            Text = text,
            OrderIndex = orderIndex,
            MappedValue = mappedValue
        };
    }
}
