using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Account.Registration;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Npgsql;
using RegistrationWorkoutType = MuscleCuties.UseCases.Features.Account.Registration.Contracts.WorkoutType;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class RegistrationPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string SyntheticDatabasePrefix = "mc_";
    private const string RegistrationPurpose = "Registration";
    private const string SyntheticHash = "synthetic-test-hash";
    private const string SyntheticMac = "synthetic-test-mac";
    private const string SyntheticKeyId = "synthetic-test-key";
    private const int RegistrationChallengePurpose = 1;
    private const int SixtyMinuteAnswerValue = 3;
    private const int ThirtyMinuteAnswerValue = 1;
    private const int FullGymAnswerValue = 1;
    private const int BodyweightAnswerValue = 3;
    private const int TestAgeYears = 30;
    private const int ReceiptLifetimeHours = 1;
    private const int UsernameNonceCharacters = 12;
    private const int TestCycleDays = 28;
    private const int TestTrainingDays = 3;
    private const float TestHeightCm = 170f;
    private const float TestWeightKg = 70f;

    [Xunit.Theory]
    [Xunit.InlineData(SixtyMinuteAnswerValue, FullGymAnswerValue)]
    [Xunit.InlineData(ThirtyMinuteAnswerValue, BodyweightAnswerValue)]
    public async Task NewAccountPersistsServerGeneratedInitialPlan(int sessionAnswer, int equipmentAnswer)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        Xunit.Assert.Equal(LocalHost, connection.Host);
        Xunit.Assert.StartsWith(SyntheticDatabasePrefix, connection.Database, StringComparison.Ordinal);

        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connectionString).Options;
        await using var database = new PostgresDbContext(options);
        var store = new PostgresCompleteAccountRegistrationStore(database, TimeProvider.System,
            new GenerateWorkoutPlanUseCase(new PostgresWorkoutPlanningReferenceDataSource(database)));
        var now = DateTimeOffset.UtcNow;
        var nonce = Guid.NewGuid().ToString("N");
        var email = $"registration-{nonce}@example.invalid";
        var username = $"registration_{nonce[..UsernameNonceCharacters]}";
        var receiptId = Guid.NewGuid().ToString("N");
        var challengeId = Guid.NewGuid();
        var consent = new RegistrationPrivacyConsent(PrivacyNotice.CurrentVersion, true, now);
        Guid? userId = null;

        database.EmailChallengeSessions.Add(new EmailChallengeSessionRecord
        {
            Id = challengeId,
            NormalizedEmail = email,
            Purpose = RegistrationChallengePurpose,
            HmacKeyId = SyntheticKeyId,
            CodeMac = SyntheticMac,
            CreatedAtUtc = now.UtcDateTime,
            ExpiresAtUtc = now.AddHours(ReceiptLifetimeHours).UtcDateTime,
            ResendAvailableAtUtc = now.UtcDateTime,
            ReceiptId = receiptId,
            PrivacyNoticeVersion = consent.NoticeVersion,
            PrivacyNoticeAcceptedAtUtc = consent.AcceptedAtUtc.UtcDateTime
        });
        await database.SaveChangesAsync();

        try
        {
            var quiz = await store.LoadQuizQuestionsAsync(CancellationToken.None);
            var selections = quiz.Select(question => new QuizSelectionDto(question.Id,
                question.QuestionType switch
                {
                    QuizQuestionType.Goal => question.Answers.Single(answer =>
                        answer.MappedValue == (int)UserGoal.MaintainHealth).Id,
                    QuizQuestionType.ExperienceLevel => question.Answers.Single(answer =>
                        answer.MappedValue == (int)TrainingExperienceLevel.Beginner).Id,
                    QuizQuestionType.CurrentCyclePhase => question.Answers.Single(answer =>
                        answer.MappedValue == (int)CyclePhase.Follicular).Id,
                    QuizQuestionType.SessionDuration => question.Answers.Single(answer =>
                        answer.MappedValue == sessionAnswer).Id,
                    QuizQuestionType.Equipment => question.Answers.Single(answer =>
                        answer.MappedValue == equipmentAnswer).Id,
                    _ => question.Answers.First().Id
                })).ToArray();
            var request = new CompleteAccountRegistrationRequest(
                email,
                username,
                "synthetic-only-password",
                "synthetic-only-receipt-token",
                challengeId.ToString(),
                new ProfileSetupDraft("Synthetic Registration", now.UtcDateTime.AddYears(-TestAgeYears),
                    TestHeightCm, TestWeightKg, string.Empty, TestTrainingDays, TestCycleDays,
                    [(int)WorkoutActivityType.HighVolumeStrength], true),
                selections);

            var result = await store.CreateAccountAsync(request, email, username, SyntheticHash,
                receiptId, now.AddHours(ReceiptLifetimeHours), consent, CancellationToken.None);
            userId = result.UserId;
            Xunit.Assert.Equal(CompleteAccountRegistrationStatus.Created, result.Status);
            Xunit.Assert.NotNull(userId);
            await SyntheticTenantSession.SetAsync(database, userId.Value);

            var plan = await database.WorkoutPlans.AsNoTracking().SingleAsync(item => item.UserId == userId);
            var days = await database.WorkoutDays.AsNoTracking()
                .Where(item => item.UserId == userId && item.WorkoutPlanId == plan.Id)
                .ToArrayAsync();
            Xunit.Assert.NotEmpty(days);
            Xunit.Assert.All(days, day =>
            {
                Xunit.Assert.InRange(day.WeekIndex, InitialWorkoutPlanRule.MinimumWeekIndex,
                    InitialWorkoutPlanRule.MaximumWeekIndex);
                Xunit.Assert.Equal(RegistrationWorkoutType.Strength.ToString(), day.WorkoutType);
            });
            Xunit.Assert.True(await database.WorkoutDayExercises.AsNoTracking()
                .AnyAsync(item => item.UserId == userId));
        }
        finally
        {
            if (userId is not null)
                await database.Users.Where(item => item.Id == userId).ExecuteDeleteAsync();
            await database.EmailChallenges.Where(item => item.Id == receiptId).ExecuteDeleteAsync();
            await database.EmailChallengeSessions.Where(item => item.Id == challengeId).ExecuteDeleteAsync();
        }
    }
}
