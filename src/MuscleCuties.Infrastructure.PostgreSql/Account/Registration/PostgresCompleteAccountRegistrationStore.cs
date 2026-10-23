using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;
using Npgsql;
using CoreWorkoutType = MuscleCuties.Core.Models.Enums.Workout.WorkoutType;
using RegistrationWorkoutType = MuscleCuties.UseCases.Features.Account.Registration.Contracts.WorkoutType;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Registration;

public sealed class PostgresCompleteAccountRegistrationStore(
    PostgresDbContext dbContext,
    TimeProvider timeProvider,
    GenerateWorkoutPlanUseCase workoutPlanner) : ICompleteAccountRegistrationStore
{
    private const string RegistrationPurpose = "Registration";
    private const string PasswordCredentialType = "Password";
    private const string EmptyProfileImagePath = "";
    private const string NoNutritionTargetsJson = "{}";
    private const string NoReproductiveContextJson = "{}";
    private const string InitialSnapshotReason = "Initial";
    private const string OnboardingPhaseNote = "Set from onboarding quiz";
    private const int SessionMinutesByThirtyMinuteChoice = 30;
    private const int SessionMinutesByFortyFiveMinuteChoice = 45;
    private const int SessionMinutesBySixtyMinuteChoice = 60;
    private const int SessionMinutesBySeventyFiveMinuteChoice = 75;
    private const int SessionMinutesByNinetyMinuteChoice = 90;
    private const int ThirtyMinuteChoiceValue = 1;
    private const int FortyFiveMinuteChoiceValue = 2;
    private const int SixtyMinuteChoiceValue = 3;
    private const int SeventyFiveMinuteChoiceValue = 4;
    private const int NinetyMinuteChoiceValue = 5;
    private const int FullGymChoiceValue = 1;
    private const int HomeEquipmentChoiceValue = 2;
    private const int BodyweightEquipmentChoiceValue = 3;
    private const int RegistrationEmailChallengePurpose = 1;
    private const int MaximumPrivacyNoticeVersionCharacters = 64;

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

        return questions.Select(question => new AccountRegistrationQuizQuestion(
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

        var registrationRequest = request;
        if (request.InitialWorkoutPlanDays is null)
        {
            try
            {
                registrationRequest = await GenerateInitialPlanAsync(request, profileProjection,
                    now.UtcDateTime, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                return new CompleteAccountRegistrationResult(
                    CompleteAccountRegistrationStatus.InvalidWorkoutPlan,
                    ErrorMessage: "The initial workout plan is temporarily unavailable.");
            }
        }

        var initialPlanDays = registrationRequest.InitialWorkoutPlanDays;
        if (initialPlanDays is null || string.IsNullOrWhiteSpace(registrationRequest.InitialWorkoutPlanName))
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidWorkoutPlan);

        var exerciseIds = initialPlanDays.SelectMany(day => day.Exercises)
            .Select(exercise => exercise.ExerciseId).Distinct().ToArray();
        var knownExerciseIds = await dbContext.Exercises.AsNoTracking()
            .Where(exercise => exerciseIds.Contains(exercise.Id))
            .Select(exercise => exercise.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (knownExerciseIds.Count != exerciseIds.Length)
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
            profileProjection.WorkoutDaysPerWeek = request.Profile.WorkoutDaysPerWeek;
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

            var planId = Guid.CreateVersion7();
            dbContext.WorkoutPlans.Add(new WorkoutPlanRecord
            {
                Id = planId,
                UserId = userId,
                Name = registrationRequest.InitialWorkoutPlanName.Trim(),
                IsActive = true,
                CyclePhaseTarget = profile.CurrentCyclePhase,
                CreatedAt = now.UtcDateTime
            });

            foreach (var day in initialPlanDays)
            {
                var dayId = Guid.CreateVersion7();
                dbContext.WorkoutDays.Add(new WorkoutDayRecord
                {
                    Id = dayId,
                    UserId = userId,
                    WorkoutPlanId = planId,
                    DayOfWeek = day.DayOfWeek,
                    WorkoutType = day.WorkoutType.ToString(),
                    Name = day.Name.Trim(),
                    WeekIndex = day.WeekIndex
                });
                foreach (var exercise in day.Exercises)
                {
                    dbContext.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord
                    {
                        Id = Guid.CreateVersion7(),
                        UserId = userId,
                        WorkoutDayId = dayId,
                        ExerciseId = exercise.ExerciseId,
                        Sets = exercise.Sets,
                        Reps = exercise.Reps
                    });
                }
            }

            if (profile.CurrentCyclePhase is not null)
            {
                dbContext.CyclePhaseLogs.Add(new CyclePhaseLogRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    Phase = profile.CurrentCyclePhase,
                    LoggedAt = now.UtcDateTime,
                    Note = OnboardingPhaseNote,
                    CreatedAt = now.UtcDateTime
                });
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

    private async Task<CompleteAccountRegistrationRequest> GenerateInitialPlanAsync(
        CompleteAccountRegistrationRequest request,
        UserProfileRecord profile,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserGoal>(profile.Goal, out var goal) ||
            !Enum.TryParse<TrainingExperienceLevel>(profile.TrainingExperienceLevel, out var experience) ||
            !Enum.TryParse<Equipment>(profile.EquipmentLevel, out var equipment) ||
            !Enum.TryParse<CyclePhase>(profile.CurrentCyclePhase, out var phase))
            throw new InvalidOperationException("The registration profile cannot generate a workout plan.");

        var generated = await workoutPlanner.ExecuteAsync(Guid.Empty,
            new GenerateWorkoutPlanRequest(
                goal,
                experience,
                request.Profile.WorkoutDaysPerWeek,
                profile.SessionDurationMinutes,
                request.Profile.SelectedActivityTypes.Select(activity => (WorkoutActivityType)activity).ToArray(),
                StrengthTrainingStyle.ComfortableModerate,
                equipment,
                phase,
                AllowsCyclePhaseAdjustments: false,
                CreatedAtUtc: nowUtc,
                Today: DateOnly.FromDateTime(nowUtc)),
            cancellationToken).ConfigureAwait(false);

        var days = generated.WorkoutDays
            .Where(day => day.WorkoutType == CoreWorkoutType.Strength && day.Exercises.Count > 0)
            .Select(day => new InitialWorkoutPlanDayInput(
                day.WeekIndex + InitialWorkoutPlanRule.MinimumWeekIndex,
                day.DayOfWeek,
                day.WorkoutType switch
                {
                    CoreWorkoutType.Strength => RegistrationWorkoutType.Strength,
                    CoreWorkoutType.Cardio => RegistrationWorkoutType.Cardio,
                    CoreWorkoutType.Recovery => RegistrationWorkoutType.Recovery,
                    _ => RegistrationWorkoutType.Rest
                },
                day.Name,
                day.Exercises.Select(exercise => new InitialWorkoutPlanExerciseInput(
                    exercise.ExerciseId, exercise.Sets, exercise.Reps)).ToArray()))
            .ToArray();

        var validation = InitialWorkoutPlanRule.Validate(days.Select(day =>
            new InitialWorkoutPlanDayObservation(
                day.WeekIndex,
                day.DayOfWeek,
                day.Exercises.Select(exercise => new InitialWorkoutPlanExerciseObservation(
                    exercise.ExerciseId, exercise.Sets, exercise.Reps)).ToArray(),
                Enum.IsDefined(day.WorkoutType),
                !string.IsNullOrWhiteSpace(day.Name))).ToArray());
        if (!validation.IsValid || string.IsNullOrWhiteSpace(generated.Name))
            throw new InvalidOperationException("The generated workout plan is invalid.");

        return request with
        {
            InitialWorkoutPlanName = generated.Name,
            InitialWorkoutPlanDays = days
        };
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
            !TrySingle(QuizQuestionType.SessionDuration, out var sessionChoice) ||
            !TryMapSessionDuration(sessionChoice, out var sessionMinutes) ||
            !TrySingle(QuizQuestionType.Equipment, out var equipmentChoice) ||
            !TryMapEquipment(equipmentChoice, out var equipmentLevel))
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
            CycleTrackingMode = CycleTrackingMode.ManualPhaseLogging.ToString(),
            CurrentCyclePhase = ((CyclePhase)phaseValue).ToString(),
            DietaryTags = string.Join(',', dietary.Select(answer => ((DietaryTag)answer.MappedValue).ToString())
                .Distinct(StringComparer.Ordinal)),
            SessionDurationMinutes = sessionMinutes,
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

    private static bool TryMapSessionDuration(int answer, out int minutes)
    {
        minutes = answer switch
        {
            ThirtyMinuteChoiceValue => SessionMinutesByThirtyMinuteChoice,
            FortyFiveMinuteChoiceValue => SessionMinutesByFortyFiveMinuteChoice,
            SixtyMinuteChoiceValue => SessionMinutesBySixtyMinuteChoice,
            SeventyFiveMinuteChoiceValue => SessionMinutesBySeventyFiveMinuteChoice,
            NinetyMinuteChoiceValue => SessionMinutesByNinetyMinuteChoice,
            _ => 0
        };
        return minutes > 0;
    }

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
