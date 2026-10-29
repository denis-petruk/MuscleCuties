using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Registration;

public sealed class PostgresCompleteAccountRegistrationStore(
    PostgresDbContext dbContext,
    TimeProvider timeProvider) : ICompleteAccountRegistrationStore
{
    private const string RegistrationPurpose = "Registration";
    private const string PasswordCredentialType = "Password";
    private const string EmptyProfileImagePath = "";
    private const string NoNutritionTargetsJson = "{}";
    private const string NoReproductiveContextJson = "{}";
    private const string NoRequestedActivitySessionsPerWeekJson = "{}";
    private const string InitialSnapshotReason = "Initial";
    private const string OnboardingPhaseNote = "Set from onboarding quiz";
    private const int FullGymChoiceValue = 1;
    private const int HomeEquipmentChoiceValue = 2;
    private const int BodyweightEquipmentChoiceValue = 3;
    private const int DefaultNutritionActivityDaysPerWeek = 3;
    private const int RegistrationEmailChallengePurpose = 1;
    private const int MaximumPrivacyNoticeVersionCharacters = 64;
    private const int UnknownCycleLength = 0;
    private static readonly DateOnly MinimumSupportedCycleDate = new(1970, 1, 1);

    public async Task<RegistrationPrivacyConsent?> GetPendingPrivacyConsentAsync(
        string emailReceiptId,
        string challengeId,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(challengeId, out var parsedChallengeId))
            return null;
        var consent = await dbContext.EmailChallengeSessions.AsNoTracking()
            .Where(item => item.Id == parsedChallengeId && item.ReceiptId == emailReceiptId &&
                           item.Purpose == RegistrationEmailChallengePurpose &&
                           item.NormalizedEmail == normalizedEmail)
            .Select(item => new { item.PrivacyNoticeVersion, item.PrivacyNoticeAcceptedAtUtc })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return consent?.PrivacyNoticeVersion is { Length: > 0 } version &&
               consent.PrivacyNoticeAcceptedAtUtc is { } acceptedAt
            ? new RegistrationPrivacyConsent(version, true, new DateTimeOffset(DateTime.SpecifyKind(acceptedAt, DateTimeKind.Utc)))
            : null;
    }

    public async Task<IReadOnlyList<AccountRegistrationQuizQuestion>> LoadQuizQuestionsAsync(
        CancellationToken cancellationToken)
    {
        var questions = await dbContext.QuizQuestions
            .AsNoTracking()
            .Include(question => question.Answers)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return questions
            .Where(question => question.QuestionType.IsAvailableInCurrentOnboarding())
            .Select(question => new AccountRegistrationQuizQuestion(
            question.Id,
            question.Question,
            question.OrderIndex,
            question.QuestionType,
            question.Answers.OrderBy(answer => answer.OrderIndex)
                .Select(answer => new AccountRegistrationQuizAnswer(answer.Id, answer.Text,
                    answer.OrderIndex, answer.MappedValue))
                .ToArray())).ToArray();
    }

    public async Task<CompleteAccountRegistrationResult> CreateAccountAsync(
        CompleteAccountRegistrationRequest request,
        string normalizedEmail,
        string normalizedUsername,
        string passwordHash,
        string emailReceiptId,
        DateTimeOffset emailReceiptExpiresAt,
        RegistrationPrivacyConsent privacyConsent,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (emailReceiptExpiresAt <= now || string.IsNullOrWhiteSpace(privacyConsent.NoticeVersion) ||
            privacyConsent.NoticeVersion.Length > MaximumPrivacyNoticeVersionCharacters ||
            privacyConsent.AcceptedAtUtc == default || privacyConsent.AcceptedAtUtc.Offset != TimeSpan.Zero ||
            privacyConsent.AcceptedAtUtc > now)
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidEmailVerification);

        var pendingConsent = await GetPendingPrivacyConsentAsync(
                emailReceiptId, request.EmailChallengeId, normalizedEmail, cancellationToken)
            .ConfigureAwait(false);
        if (pendingConsent is null || pendingConsent != privacyConsent)
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.PrivacyConsentRequired);

        var questions = await dbContext.QuizQuestions
            .AsNoTracking()
            .Include(question => question.Answers)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var responses = request.QuizResponses.GroupBy(response => response.QuestionId)
            .ToDictionary(group => group.Key, group => group.Select(response => response.AnswerId).ToHashSet());
        var selectedAnswers = questions.SelectMany(question => question.Answers
                .Where(answer => responses.TryGetValue(question.Id, out var answerIds) && answerIds.Contains(answer.Id))
                .Select(answer => new SelectedQuizAnswer(question.QuestionType, question.Id, answer.Id,
                    answer.MappedValue)))
            .ToArray();
        var profileProjection = TryProjectProfile(request, selectedAnswers, now.UtcDateTime);
        if (profileProjection is null)
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidQuiz);

        if (request.InitialWorkoutPlanDays is not null ||
            !string.IsNullOrWhiteSpace(request.InitialWorkoutPlanName))
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidWorkoutPlan);

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        dbContext.ChangeTracker.Clear();
        var userId = Guid.CreateVersion7();
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (await dbContext.Users.AsNoTracking()
                    .AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken)
                    .ConfigureAwait(false))
                return await RollbackAndReturnAsync(transaction,
                    new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.EmailAlreadyRegistered))
                    .ConfigureAwait(false);

            if (await dbContext.Users.AsNoTracking()
                    .AnyAsync(user => user.Username != null && user.Username.ToLower() == normalizedUsername,
                        cancellationToken)
                    .ConfigureAwait(false))
                return await RollbackAndReturnAsync(transaction,
                    new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.UsernameTaken))
                    .ConfigureAwait(false);

            var user = new UserRecord
            {
                Id = userId,
                Email = normalizedEmail,
                Username = request.Username.Trim(),
                CredentialType = PasswordCredentialType,
                PasswordHash = passwordHash,
                PrivacyNoticeVersion = privacyConsent.NoticeVersion,
                PrivacyNoticeAcceptedAtUtc = privacyConsent.AcceptedAtUtc.UtcDateTime,
                CreatedAt = now.UtcDateTime,
                UpdatedAt = now.UtcDateTime,
                IsOnboardingComplete = false
            };
            dbContext.Users.Add(user);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var receipt = new EmailChallengeRecord
            {
                Id = emailReceiptId,
                Purpose = RegistrationPurpose,
                RedeemedAtUtc = now.UtcDateTime
            };
            dbContext.EmailChallenges.Add(receipt);

            profileProjection.Id = Guid.CreateVersion7();
            profileProjection.UserId = userId;
            profileProjection.Name = string.IsNullOrWhiteSpace(request.Profile.Name)
                ? request.Username.Trim()
                : request.Profile.Name.Trim();
            profileProjection.DateOfBirth = DateOnly.FromDateTime(request.Profile.BirthDate);
            profileProjection.Height = Convert.ToDecimal(request.Profile.HeightCm, CultureInfo.InvariantCulture);
            profileProjection.Weight = Convert.ToDecimal(request.Profile.WeightKg, CultureInfo.InvariantCulture);
            profileProjection.CycleLength = request.Profile.CycleLength;
            profileProjection.ProfileImagePath = EmptyProfileImagePath;
            profileProjection.UpdatedAt = now.UtcDateTime;
            var profile = profileProjection;
            dbContext.UserProfiles.Add(profile);

            var snapshot = new UserProfileSnapshotRecord
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                SnapshotReason = InitialSnapshotReason,
                ProfileJson = JsonSerializer.Serialize(profile),
                CreatedAt = now.UtcDateTime
            };
            dbContext.UserProfileSnapshots.Add(snapshot);

            foreach (var selectedAnswer in selectedAnswers)
            {
                dbContext.UserQuizResponses.Add(new UserQuizResponseRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    QuizQuestionId = selectedAnswer.QuestionId,
                    QuizAnswerId = selectedAnswer.AnswerId,
                    AnsweredAt = now.UtcDateTime,
                    UserProfileSnapshotId = snapshot.Id
                });
            }

            if (Enum.TryParse<CyclePhase>(profile.CurrentCyclePhase, true, out var onboardingPhase) &&
                Enum.IsDefined(onboardingPhase) && onboardingPhase != CyclePhase.Unknown)
            {
                // The quiz phase is the first captured observation, so it also anchors the first cycle.
                // Without the anchor the account has a phase log but no active cycle to count days from.
                var onboardingLocalDate = DateOnly.FromDateTime(now.UtcDateTime);
                var onboardingCycleStart = CyclePhaseRule.GetImpliedCycleStart(
                    onboardingPhase, onboardingLocalDate, profile.CycleLength);
                if (onboardingCycleStart >= MinimumSupportedCycleDate)
                {
                    var onboardingCycle = new CycleLogRecord
                    {
                        Id = Guid.CreateVersion7(),
                        UserId = userId,
                        StartDate = onboardingCycleStart,
                        EndDate = null,
                        CycleLength = UnknownCycleLength,
                        CreatedAt = now.UtcDateTime
                    };
                    dbContext.CycleLogs.Add(onboardingCycle);
                    dbContext.CyclePhaseLogs.Add(new CyclePhaseLogRecord
                    {
                        Id = Guid.CreateVersion7(),
                        UserId = userId,
                        CycleLogId = onboardingCycle.Id,
                        Phase = onboardingPhase.ToString(),
                        LocalDate = onboardingLocalDate,
                        LoggedAt = UtcMidnight(onboardingLocalDate),
                        Note = OnboardingPhaseNote,
                        CreatedAt = now.UtcDateTime
                    });
                }
            }

            user.IsOnboardingComplete = true;
            user.UpdatedAt = now.UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.Created, userId);
        }
        catch (DbUpdateException exception) when (FindPostgresException(exception)?.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RollbackAsync(transaction).ConfigureAwait(false);
            var postgresException = FindPostgresException(exception);
            var status = postgresException?.ConstraintName switch
            {
                "uq_users_normalized_email" => CompleteAccountRegistrationStatus.EmailAlreadyRegistered,
                "uq_users_normalized_username" => CompleteAccountRegistrationStatus.UsernameTaken,
                _ => CompleteAccountRegistrationStatus.EmailReceiptAlreadyUsed
            };
            return new CompleteAccountRegistrationResult(status);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await RollbackAsync(transaction).ConfigureAwait(false);
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.Failed,
                ErrorMessage: "The registration changed concurrently. Please try again.");
        }
        catch (DbUpdateException exception) when
            (FindPostgresException(exception)?.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await RollbackAsync(transaction).ConfigureAwait(false);
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.Failed,
                ErrorMessage: "The registration changed concurrently. Please try again.");
        }
        catch (RetryLimitExceededException exception) when
            (FindPostgresException(exception)?.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await RollbackAsync(transaction).ConfigureAwait(false);
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.Failed,
                ErrorMessage: "The registration changed concurrently. Please try again.");
        }
        catch (Exception exception) when
            (FindPostgresException(exception)?.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await RollbackAsync(transaction).ConfigureAwait(false);
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.Failed,
                ErrorMessage: "The registration changed concurrently. Please try again.");
        }
        catch
        {
            await RollbackAsync(transaction).ConfigureAwait(false);
            throw;
        }
        }).ConfigureAwait(false);
    }

    private static UserProfileRecord? TryProjectProfile(
        CompleteAccountRegistrationRequest request,
        IReadOnlyList<SelectedQuizAnswer> selectedAnswers,
        DateTime now)
    {
        var byType = selectedAnswers.GroupBy(answer => answer.QuestionType)
            .ToDictionary(group => group.Key, group => group.ToArray());
        if (!TrySingle(QuizQuestionType.Goal, out var goalValue) || !Enum.IsDefined((UserGoal)goalValue) ||
            !TrySingle(QuizQuestionType.ExperienceLevel, out var experienceValue) ||
            !Enum.IsDefined((TrainingExperienceLevel)experienceValue) ||
            !TrySingle(QuizQuestionType.CurrentCyclePhase, out var phaseValue) ||
            !Enum.IsDefined((CyclePhase)phaseValue) ||
            !TrySingle(QuizQuestionType.SessionDuration, out var sessionDurationChoice) ||
            !ActivitySessionDurationRule.TryResolveStrengthQuizSessionDurationMinutes(
                sessionDurationChoice, out var strengthSessionDurationMinutes) ||
            !TrySingle(QuizQuestionType.Equipment, out var equipmentChoice) ||
            !TryMapEquipment(equipmentChoice, out var equipmentLevel))
            return null;

        var availableWeekdays = byType.GetValueOrDefault(QuizQuestionType.AvailableWeekdays) ?? [];
        var weekdays = availableWeekdays.Select(answer => answer.MappedValue).Distinct().ToArray();
        if (weekdays.Length is < WorkoutPreferencesRule.MinimumDaysPerWeek or > WorkoutPreferencesRule.MaximumDaysPerWeek ||
            weekdays.Any(day => !Enum.IsDefined(typeof(DayOfWeek), day)))
            return null;

        var goal = (UserGoal)goalValue;
        var pace = WeightGoalPace.Steady;
        if (byType.TryGetValue(QuizQuestionType.GoalPace, out var paceAnswers))
        {
            if (paceAnswers.Length != 1 || !Enum.IsDefined((WeightGoalPace)paceAnswers[0].MappedValue))
                return null;
            pace = (WeightGoalPace)paceAnswers[0].MappedValue;
        }
        else if (goal is UserGoal.FatLoss or UserGoal.Strength)
        {
            return null;
        }

        var dietary = byType.GetValueOrDefault(QuizQuestionType.DietaryPreference) ?? [];
        if (dietary.Any(answer => !Enum.IsDefined((DietaryTag)answer.MappedValue)))
            return null;

        var unitSystem = request.Profile.UseMetricSystem ? "Metric" : "Imperial";
        return new UserProfileRecord
        {
            Goal = goal.ToString(),
            WeightGoalPace = pace.ToString(),
            TrainingExperienceLevel = ((TrainingExperienceLevel)experienceValue).ToString(),
            CurrentCyclePhase = ((CyclePhase)phaseValue).ToString(),
            WorkoutDaysPerWeek = weekdays.Length,
            AvailableWorkoutWeekdaysJson = JsonSerializer.Serialize(weekdays
                .Select(value => ((DayOfWeek)value).ToString()).OrderBy(value => value, StringComparer.Ordinal)),
            ActivitySessionDurationsJson = JsonSerializer.Serialize(
                RegistrationActivityDurationDefaults.ForSelectedActivities(
                    request.Profile.SelectedActivityTypes, strengthSessionDurationMinutes)),
            RequestedActivitySessionsPerWeekJson = JsonSerializer.Serialize(
                RegistrationActivityDurationDefaults.ForSelectedActivityFrequencies(
                    request.Profile.SelectedActivityTypes)),
            DietaryTags = string.Join(',', dietary.Select(answer => ((DietaryTag)answer.MappedValue).ToString())
                .Distinct(StringComparer.Ordinal)),
            EquipmentLevel = equipmentLevel,
            PhaseBaselinesJson = BuildPhaseBaselines(byType),
            PreferredWorkoutActivityTypes = string.Join(',', request.Profile.SelectedActivityTypes
                .Distinct().OrderBy(value => value).Select(value => value.ToString(CultureInfo.InvariantCulture))),
            UnitSystem = unitSystem,
            BodyWeightUnit = request.Profile.UseMetricSystem ? "kg" : "lbs",
            FoodMassUnit = "g",
            HeightUnit = request.Profile.UseMetricSystem ? "cm" : "in",
            DistanceUnit = request.Profile.UseMetricSystem ? "km" : "mi",
            EnergyUnit = "kcal",
            NutritionGoalsJson = NoNutritionTargetsJson,
            ReproductiveContextJson = NoReproductiveContextJson,
            UpdatedAt = now
        };

        bool TrySingle(QuizQuestionType type, out int value)
        {
            value = 0;
            return byType.TryGetValue(type, out var answers) && answers.Length == 1 &&
                   (value = answers[0].MappedValue) >= 0;
        }
    }

    private static string BuildPhaseBaselines(
        IReadOnlyDictionary<QuizQuestionType, SelectedQuizAnswer[]> byType)
    {
        var values = byType.ToDictionary(pair => pair.Key, pair => pair.Value[0].MappedValue);
        var baselines = new
        {
            Menstrual = new { Pain = values[QuizQuestionType.MenstrualPain], Energy = values[QuizQuestionType.MenstrualEnergy] },
            Follicular = new { Pain = values[QuizQuestionType.FollicularPain], Energy = values[QuizQuestionType.FollicularEnergy] },
            Ovulatory = new { Pain = values[QuizQuestionType.OvulatoryPain], Energy = values[QuizQuestionType.OvulatoryEnergy] },
            Luteal = new { Pain = values[QuizQuestionType.LutealPain], Energy = values[QuizQuestionType.LutealEnergy] }
        };
        return JsonSerializer.Serialize(baselines);
    }

    private static DateTime UtcMidnight(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static bool TryMapEquipment(int answer, out string equipment)
    {
        equipment = answer switch
        {
            FullGymChoiceValue => Equipment.FullGym.ToString(),
            HomeEquipmentChoiceValue => Equipment.HomeDumbbellsBands.ToString(),
            BodyweightEquipmentChoiceValue => Equipment.Bodyweight.ToString(),
            _ => string.Empty
        };
        return !string.IsNullOrEmpty(equipment);
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException postgresException)
                return postgresException;
        return null;
    }

    private static async Task<T> RollbackAndReturnAsync<T>(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        T result)
    {
        await RollbackAsync(transaction).ConfigureAwait(false);
        return result;
    }

    private static async Task RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // A failed rollback must not replace the generic registration failure.
        }
    }

    private sealed record SelectedQuizAnswer(
        QuizQuestionType QuestionType,
        int QuestionId,
        int AnswerId,
        int MappedValue);
}
