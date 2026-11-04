using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Account.Registration;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class CanonicalRegistrationCatalogTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_TEST_POSTGRES";
    private const int TrainingDaysPerWeek = 3;
    private const int SevenWorkoutDaysPerWeek = 7;
    private const int SevenDayMuscleBudgetCount = 13;

    [Xunit.Theory]
    [Xunit.InlineData("(int)UserGoal.MaintainHealth", (int)UserGoal.MaintainHealth)]
    [Xunit.InlineData("(int)CyclePhase.Luteal", (int)CyclePhase.Luteal)]
    [Xunit.InlineData("(int)WeightGoalPace.Aggressive", (int)WeightGoalPace.Aggressive)]
    [Xunit.InlineData("(int)DietaryTag.None", (int)DietaryTag.None)]
    public void CanonicalQuizEnumAnswerMapsToItsDomainValue(string manifestValue, int expected)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(manifestValue));

        Xunit.Assert.Equal(expected, PostgresCanonicalCatalogSeeder.ParseMappedValue(document.RootElement));
    }

    [Xunit.Fact]
    public void InvalidCanonicalQuizAnswerMappingCannotSilentlyUseAnswerOrder()
    {
        using var document = JsonDocument.Parse("\"(int)UserGoal.Unknown\"");

        Xunit.Assert.Throws<InvalidDataException>(() =>
            PostgresCanonicalCatalogSeeder.ParseMappedValue(document.RootElement));
    }

    [Xunit.Fact]
    public async Task CanonicalCatalogHasValidRegistrationAndPlanningReferenceData()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a seeded local PostgreSQL test database.");

        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var database = new PostgresDbContext(options);
        var quiz = await new PostgresCompleteAccountRegistrationStore(database, TimeProvider.System)
            .LoadQuizQuestionsAsync(CancellationToken.None);
        Xunit.Assert.NotEmpty(quiz);
        Xunit.Assert.All(quiz, question => Xunit.Assert.NotEmpty(question.Answers));
        Xunit.Assert.Equal((int)UserGoal.MaintainHealth,
            quiz.Single(question => question.QuestionType == QuizQuestionType.Goal)
                .Answers.Single(answer => answer.Text == "Maintain health").MappedValue);
        Xunit.Assert.Equal((int)CyclePhase.Luteal,
            quiz.Single(question => question.QuestionType == QuizQuestionType.CurrentCyclePhase)
                .Answers.Single(answer => answer.Text == "Luteal").MappedValue);
        Xunit.Assert.Equal((int)CyclePhase.Unknown,
            quiz.Single(question => question.QuestionType == QuizQuestionType.CurrentCyclePhase)
                .Answers.Single(answer => answer.Text == "Not sure").MappedValue);
        Xunit.Assert.Equal((int)WeightGoalPace.Aggressive,
            quiz.Single(question => question.QuestionType == QuizQuestionType.GoalPace)
                .Answers.Single(answer => answer.Text == "Aggressive").MappedValue);

        var referenceData = new PostgresWorkoutPlanningReferenceDataSource(database);
        var template = await referenceData.FindWeekTemplateAsync(TrainingDaysPerWeek);
        Xunit.Assert.True(WeekPlanRule.TryValidateTemplate(template, TrainingDaysPerWeek,
            out var sequence, out var templateFailure),
            string.Join("; ", templateFailure?.Validation.Entries.Select(entry => entry.Message) ?? []));
        var archetypes = await referenceData.GetArchetypesAsync(
            sequence.Where(id => id > 0).Distinct().ToArray());
        Xunit.Assert.True(WeekPlanRule.TryValidateArchetypes(archetypes,
            sequence.Where(id => id > 0).Distinct().ToArray(), TrainingDaysPerWeek,
            out _, out var archetypeFailure),
            string.Join("; ", archetypeFailure?.Validation.Entries.Select(entry => entry.Message) ?? []) +
            " Archetypes: " + string.Join(" | ", archetypes.Select(archetype =>
                $"{archetype.Id}:{archetype.Slots.Count}")) +
            " Samples: " + string.Join(" | ", archetypes.SelectMany(archetype => archetype.Slots)
                .Select(slot => slot.AllowedPatterns).Distinct().Take(12)));

        var definitions = await referenceData.GetExerciseDefinitionsAsync();
        var exercises = await referenceData.GetExercisesAsync();
        Xunit.Assert.All(exercises, exercise =>
            Xunit.Assert.DoesNotContain("Canonical exercise:", exercise.Description, StringComparison.Ordinal));
        Xunit.Assert.True(exercises.Select(exercise => exercise.PrimaryMuscle).Distinct().Skip(1).Any(),
            "Workout exercises need varied primary-muscle metadata.");
        var names = exercises.GroupBy(exercise => NormalizeExerciseKey(exercise.Name), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Xunit.Assert.All(definitions, definition =>
            Xunit.Assert.True(names.TryGetValue(NormalizeExerciseKey(definition.Name), out var count) && count == 1,
                $"Planning exercise {definition.Name} needs exactly one catalog match."));

    }

    [Xunit.Fact]
    public async Task CanonicalCatalogHasSevenDayReferenceData()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a seeded local PostgreSQL test database.");

        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var database = new PostgresDbContext(options);
        var referenceData = new PostgresWorkoutPlanningReferenceDataSource(database);
        var template = await referenceData.FindWeekTemplateAsync(SevenWorkoutDaysPerWeek);
        Xunit.Assert.True(WeekPlanRule.TryValidateTemplate(template, SevenWorkoutDaysPerWeek,
            out _, out var failure), failure?.Validation.Entries.FirstOrDefault()?.Message);
        Xunit.Assert.Equal(SevenDayMuscleBudgetCount,
            (await referenceData.GetVolumeBudgetsAsync(SevenWorkoutDaysPerWeek)).Count);

    }
    private static string NormalizeExerciseKey(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
