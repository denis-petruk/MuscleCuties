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
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class CanonicalRegistrationCatalogTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_TEST_POSTGRES";
    private const int TrainingDaysPerWeek = 3;
    private const int SessionMinutes = 60;
    private const int MinimumRegistrationTrainingDays = 1;
    private const int MaximumRegistrationTrainingDays = 5;
    private const int ShortRegistrationSessionMinutes = 30;
    private const int LongRegistrationSessionMinutes = 90;
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
    public async Task CanonicalCatalogCanGenerateAValidInitialRegistrationPlan()
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
        var names = exercises.GroupBy(exercise => FourWeekPlanRule.NormalizeExerciseKey(exercise.Name), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Xunit.Assert.All(definitions, definition =>
            Xunit.Assert.True(names.TryGetValue(FourWeekPlanRule.NormalizeExerciseKey(definition.Name), out var count) && count == 1,
                $"Planning exercise {definition.Name} needs exactly one catalog match."));

        var planner = new GenerateWorkoutPlanUseCase(referenceData);
        var nowUtc = DateTime.UtcNow;
        if (!WorkoutPlanGenerationRule.CanGeneratePlans)
        {
            var blocked = await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
                planner.ExecuteAsync(Guid.Empty, new GenerateWorkoutPlanRequest(
                    UserGoal.MaintainHealth, TrainingExperienceLevel.Beginner,
                    TrainingDaysPerWeek, SessionMinutes,
                    [WorkoutActivityType.HighVolumeStrength],
                    Equipment.FullGym, CyclePhase.Follicular, false, nowUtc,
                    DateOnly.FromDateTime(nowUtc))));
            Xunit.Assert.Equal(WorkoutPlanGenerationRule.GenerationUnavailableMessage, blocked.Message);
            return;
        }
        var plan = await planner.ExecuteAsync(Guid.Empty,
            new GenerateWorkoutPlanRequest(
                UserGoal.MaintainHealth,
                TrainingExperienceLevel.Beginner,
                TrainingDaysPerWeek,
                SessionMinutes,
                [WorkoutActivityType.HighVolumeStrength],
                Equipment.FullGym,
                CyclePhase.Follicular,
                AllowsCyclePhaseAdjustments: false,
                CreatedAtUtc: nowUtc,
                Today: DateOnly.FromDateTime(nowUtc)));

        var activeDays = plan.WorkoutDays
            .Where(day => day.WorkoutType == WorkoutType.Strength && day.Exercises.Count > 0)
            .ToArray();
        var validation = InitialWorkoutPlanRule.Validate(activeDays.Select(day =>
            new InitialWorkoutPlanDayObservation(
                day.WeekIndex + InitialWorkoutPlanRule.MinimumWeekIndex,
                day.DayOfWeek,
                day.Exercises.Select(exercise => new InitialWorkoutPlanExerciseObservation(
                    exercise.ExerciseId, exercise.Sets, exercise.Reps)).ToArray(),
                Enum.IsDefined(day.WorkoutType),
                !string.IsNullOrWhiteSpace(day.Name))).ToArray());
        Xunit.Assert.True(validation.IsValid, validation.ErrorMessage);
    }

    [Xunit.Fact]
    public async Task CanonicalCatalogCanGenerateSevenDayPreferencePlan()
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

        var nowUtc = DateTime.UtcNow;
        var equipmentChoices = new[] { Equipment.FullGym, Equipment.HomeDumbbellsBands, Equipment.Bodyweight };
        var sessionChoices = new[] { ShortRegistrationSessionMinutes, SessionMinutes, LongRegistrationSessionMinutes };

        if (!WorkoutPlanGenerationRule.CanGeneratePlans)
        {
            await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
                new GenerateWorkoutPlanUseCase(referenceData).ExecuteAsync(Guid.Empty,
                    new GenerateWorkoutPlanRequest(UserGoal.MaintainHealth,
                        TrainingExperienceLevel.Beginner, SevenWorkoutDaysPerWeek, SessionMinutes,
                        [WorkoutActivityType.HighVolumeStrength],
                        Equipment.FullGym, CyclePhase.Follicular, false, nowUtc,
                        DateOnly.FromDateTime(nowUtc))));
            return;
        }

        foreach (var equipment in equipmentChoices)
        foreach (var sessionMinutes in sessionChoices)
        {
            var plan = await new GenerateWorkoutPlanUseCase(referenceData).ExecuteAsync(Guid.Empty,
                new GenerateWorkoutPlanRequest(
                    UserGoal.MaintainHealth,
                    TrainingExperienceLevel.Beginner,
                    SevenWorkoutDaysPerWeek,
                    sessionMinutes,
                    [WorkoutActivityType.HighVolumeStrength],
                    equipment,
                    CyclePhase.Follicular,
                    AllowsCyclePhaseAdjustments: false,
                    CreatedAtUtc: nowUtc,
                    Today: DateOnly.FromDateTime(nowUtc)));

            Xunit.Assert.NotEmpty(plan.WorkoutDays);
            Xunit.Assert.All(plan.WorkoutDays.GroupBy(day => day.WeekIndex), week =>
                Xunit.Assert.Equal(SevenWorkoutDaysPerWeek, week.Count()));
            var strengthDays = plan.WorkoutDays
                .Where(day => day.WorkoutType == WorkoutType.Strength && day.Exercises.Count > 0)
                .ToArray();
            var validation = InitialWorkoutPlanRule.Validate(strengthDays.Select(day =>
                new InitialWorkoutPlanDayObservation(
                    day.WeekIndex + InitialWorkoutPlanRule.MinimumWeekIndex,
                    day.DayOfWeek,
                    day.Exercises.Select(exercise => new InitialWorkoutPlanExerciseObservation(
                        exercise.ExerciseId, exercise.Sets, exercise.Reps)).ToArray(),
                    Enum.IsDefined(day.WorkoutType),
                    !string.IsNullOrWhiteSpace(day.Name))).ToArray());
            Xunit.Assert.True(validation.IsValid,
                $"{equipment} / {sessionMinutes} minutes: {validation.ErrorMessage}");
        }
    }

    [Xunit.Fact]
    public async Task CanonicalCatalogSupportsEveryRegistrationTrainingDayAndEquipmentChoice()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a seeded local PostgreSQL test database.");

        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var database = new PostgresDbContext(options);
        var planner = new GenerateWorkoutPlanUseCase(new PostgresWorkoutPlanningReferenceDataSource(database));
        var nowUtc = DateTime.UtcNow;
        var equipmentChoices = new[] { Equipment.FullGym, Equipment.HomeDumbbellsBands, Equipment.Bodyweight };
        var sessionChoices = new[] { ShortRegistrationSessionMinutes, SessionMinutes, LongRegistrationSessionMinutes };

        if (!WorkoutPlanGenerationRule.CanGeneratePlans)
        {
            await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
                planner.ExecuteAsync(Guid.Empty, new GenerateWorkoutPlanRequest(
                    UserGoal.MaintainHealth, TrainingExperienceLevel.Beginner,
                    MinimumRegistrationTrainingDays, SessionMinutes,
                    [WorkoutActivityType.HighVolumeStrength],
                    Equipment.FullGym, CyclePhase.Follicular, false, nowUtc,
                    DateOnly.FromDateTime(nowUtc))));
            return;
        }

        for (var daysPerWeek = MinimumRegistrationTrainingDays;
             daysPerWeek <= MaximumRegistrationTrainingDays;
             daysPerWeek++)
        foreach (var equipment in equipmentChoices)
        foreach (var sessionMinutes in sessionChoices)
        {
            var plan = await planner.ExecuteAsync(Guid.Empty,
                new GenerateWorkoutPlanRequest(
                    UserGoal.MaintainHealth,
                    TrainingExperienceLevel.Beginner,
                    daysPerWeek,
                    sessionMinutes,
                    [WorkoutActivityType.HighVolumeStrength],
                    equipment,
                    CyclePhase.Follicular,
                    AllowsCyclePhaseAdjustments: false,
                    CreatedAtUtc: nowUtc,
                    Today: DateOnly.FromDateTime(nowUtc)));

            var activeDays = plan.WorkoutDays
                .Where(day => day.WorkoutType == WorkoutType.Strength && day.Exercises.Count > 0)
                .ToArray();
            var validation = InitialWorkoutPlanRule.Validate(activeDays.Select(day =>
                new InitialWorkoutPlanDayObservation(
                    day.WeekIndex + InitialWorkoutPlanRule.MinimumWeekIndex,
                    day.DayOfWeek,
                    day.Exercises.Select(exercise => new InitialWorkoutPlanExerciseObservation(
                        exercise.ExerciseId, exercise.Sets, exercise.Reps)).ToArray(),
                    Enum.IsDefined(day.WorkoutType),
                    !string.IsNullOrWhiteSpace(day.Name))).ToArray());
            Xunit.Assert.True(validation.IsValid,
                $"days={daysPerWeek}, equipment={equipment}, sessionMinutes={sessionMinutes}: {validation.ErrorMessage}");
        }
    }
}
