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

    [PostgresTheory]
    [Xunit.InlineData("(int)UserGoal.MaintainHealth", (int)UserGoal.MaintainHealth)]
    [Xunit.InlineData("(int)CyclePhase.Luteal", (int)CyclePhase.Luteal)]
    [Xunit.InlineData("(int)WeightGoalPace.Aggressive", (int)WeightGoalPace.Aggressive)]
    [Xunit.InlineData("(int)DietaryTag.None", (int)DietaryTag.None)]
    public void CanonicalQuizEnumAnswerMapsToItsDomainValue(string manifestValue, int expected)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(manifestValue));

        Xunit.Assert.Equal(expected, PostgresCanonicalCatalogSeeder.ParseMappedValue(document.RootElement));
    }

    [PostgresFact]
    public void InvalidCanonicalQuizAnswerMappingCannotSilentlyUseAnswerOrder()
    {
        using var document = JsonDocument.Parse("\"(int)UserGoal.Unknown\"");

        Xunit.Assert.Throws<InvalidDataException>(() =>
            PostgresCanonicalCatalogSeeder.ParseMappedValue(document.RootElement));
    }

    [PostgresFact]
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

    }

    private static string NormalizeExerciseKey(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
