

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The onboarding quiz through the backend: its questions, its answers and the account they create.</summary>
public sealed class CloudOnboardingQuizStore(
    IPendingRegistrationService pending,
    MobileJourneyCloudGateway cloudGateway,
    IMuscleCutiesBackendApiClient apiClient,
    IBackendSessionTokenStore? tokenStore = null) : IOnboardingQuizStore
{
    private const string RegistrationQuizRoute = "/api/v1/accounts/registration/quiz";
    private const string AuthenticatedOnboardingCompleteRoute = "/api/v1/accounts/onboarding/complete";
    private const string OnboardingAlreadyCompletedCode = "onboardingAlreadyCompleted";
    private const string RegistrationQuizUnavailableMessage =
        "Registration questions are temporarily unavailable. Please try again.";
    private const string RegistrationConflictCode = "registrationConflict";
    private const string PrivacyConsentRequiredCode = "privacyConsentRequired";
    private const string ConflictingCycleLengthsCode = "conflictingCycleLengths";
    private const string AvailableWeekdaysQuestionPrompt = "Which days of the week are you available to train?";
    private const int SyntheticWeekdayAnswerIdBase = 10_000;
    private const int DefaultWorkoutDaysPerWeek = 3;
    private const int MinimumWorkoutDaysPerWeek = 2;
    private const int MaximumWorkoutDaysPerWeek = 7;

    private static readonly (string Label, int OrderIndex, DayOfWeek Day)[] CanonicalWeekdayOptions =
    [
        ("Monday", 1, DayOfWeek.Monday),
        ("Tuesday", 2, DayOfWeek.Tuesday),
        ("Wednesday", 3, DayOfWeek.Wednesday),
        ("Thursday", 4, DayOfWeek.Thursday),
        ("Friday", 5, DayOfWeek.Friday),
        ("Saturday", 6, DayOfWeek.Saturday),
        ("Sunday", 7, DayOfWeek.Sunday)
    ];

    private readonly OnboardingAccountScope _accountScope = new(tokenStore);
    private IReadOnlyList<QuizQuestionDto>? _rawServerQuestions;
    private IReadOnlyList<QuizQuestionDto>? _cachedClientQuestions;

    /// <summary>Loads the onboarding quiz, optionally refreshed from the backend.</summary>
    public async Task<IReadOnlyList<QuizQuestionDto>> LoadQuestionsAsync(bool refresh,
        CancellationToken cancellationToken)
    {
        if (!refresh && _cachedClientQuestions is { Count: > 0 })
            return _cachedClientQuestions;

        var response = await apiClient.GetAsync<QuizQuestionDto[]>(RegistrationQuizRoute,
            cancellationToken).ConfigureAwait(false);
        if (response.Status != BackendApiStatus.Success || response.Data is not { Length: > 0 })
            throw new InvalidOperationException(RegistrationQuizUnavailableMessage);

        _rawServerQuestions = response.Data;
        _cachedClientQuestions = UpgradeLegacyScheduleQuestionIfNeeded(response.Data);
        return _cachedClientQuestions;
    }

    /// <summary>Saves the quiz answers.</summary>
    public async Task<QuizCommitResult> CommitAsync(
        IReadOnlyList<QuizSelectionDto> selections,
        CancellationToken cancellationToken)
    {
        if (await _accountScope.ReadAsync(cancellationToken).ConfigureAwait(false) is not null)
        {
            var stagedProfile = pending.StagedProfile;
            if (stagedProfile is null)
                return new QuizCommitResult(false, false,
                    "Please complete your profile details before finishing onboarding.");

            var (normalizedSelections, resolvedWorkoutDaysPerWeek) =
                NormalizeSelectionsForServerCatalog(selections, _rawServerQuestions);

            var onboardingRequest = new
            {
                profile = new
                {
                    name = stagedProfile.Name,
                    birthDate = DateOnly.FromDateTime(stagedProfile.BirthDate),
                    heightCm = (decimal)stagedProfile.Height,
                    weightKg = (decimal)stagedProfile.Weight,
                    workoutDaysPerWeek = resolvedWorkoutDaysPerWeek,
                    cycleLengthDays = stagedProfile.CycleLength,
                    selectedActivityTypes = stagedProfile.SelectedActivities.Select(a => (int)a).ToList(),
                    useMetricSystem = stagedProfile.UseMetricSystem,
                    profileImagePath = stagedProfile.ProfileImagePath
                },
                quizResponses = normalizedSelections
                    .Select(s => new { questionId = s.QuestionId, answerId = s.AnswerId })
                    .ToList()
            };

            var onboardingResponse = await apiClient.SendMutationAsync<object, JsonElement>(
                HttpMethod.Post,
                AuthenticatedOnboardingCompleteRoute,
                onboardingRequest,
                idempotencyKey: Guid.NewGuid().ToString("N"),
                cancellationToken).ConfigureAwait(false);

            if (onboardingResponse.Status == BackendApiStatus.Success ||
                (onboardingResponse.Status == BackendApiStatus.Conflict &&
                 string.Equals(onboardingResponse.ErrorCode, OnboardingAlreadyCompletedCode, StringComparison.OrdinalIgnoreCase)))
            {
                await cloudGateway.MarkOnboardingCompleteAsync(cancellationToken).ConfigureAwait(false);
                pending.Clear();
                return new QuizCommitResult(true, false, null);
            }

            return new QuizCommitResult(false, false,
                GetRegistrationFailureMessage(onboardingResponse.Status, onboardingResponse.ErrorCode));
        }

        var reg = pending.CurrentRegistration;
        var profile = pending.StagedProfile;
        if (reg is not null && profile is not null)
        {
            if (reg.CredentialType != UserCredentialType.Password ||
                string.IsNullOrWhiteSpace(reg.Password) ||
                !reg.IsVerified || !Guid.TryParse(reg.ChallengeId, out _) ||
                string.IsNullOrWhiteSpace(reg.VerificationToken) ||
                string.IsNullOrWhiteSpace(reg.PrivacyNoticeVersion) ||
                reg.PrivacyConsentAcceptedAtUtc is null)
                return new QuizCommitResult(false, true, "Complete email verification before creating an account.");

            var (normalizedSelections, resolvedWorkoutDaysPerWeek) =
                NormalizeSelectionsForServerCatalog(selections, _rawServerQuestions);

            var request = new
            {
                email = reg.Email,
                username = reg.Username,
                password = reg.Password,
                emailChallengeId = reg.ChallengeId,
                emailVerificationToken = reg.VerificationToken,
                privacyConsent = new
                {
                    noticeVersion = reg.PrivacyNoticeVersion,
                    accepted = true,
                    acceptedAtUtc = reg.PrivacyConsentAcceptedAtUtc
                },
                profile = new
                {
                    name = profile.Name,
                    birthDate = DateOnly.FromDateTime(profile.BirthDate),
                    heightCm = (decimal)profile.Height,
                    weightKg = (decimal)profile.Weight,
                    workoutDaysPerWeek = resolvedWorkoutDaysPerWeek,
                    cycleLengthDays = profile.CycleLength,
                    selectedActivityTypes = profile.SelectedActivities.Select(a => (int)a).ToList(),
                    useMetricSystem = profile.UseMetricSystem
                },
                quizResponses = normalizedSelections
                    .Select(s => new { questionId = s.QuestionId, answerId = s.AnswerId })
                    .ToList()
            };

            var response = await apiClient.SendMutationAsync<object, JsonElement>(
                HttpMethod.Post,
                "/api/v1/accounts/registration",
                request,
                idempotencyKey: Guid.NewGuid().ToString("N"),
                cancellationToken).ConfigureAwait(false);

            if (response.Status == BackendApiStatus.Success)
            {
                var signIn = await cloudGateway.SignInWithPasswordAsync(reg.Email, reg.Password!,
                    cancellationToken).ConfigureAwait(false);
                pending.Clear();
                return signIn.Status == BackendApiStatus.Success && signIn.Data is not null
                    ? new QuizCommitResult(true, false, null)
                    : new QuizCommitResult(true, true,
                        "Your account was created. Please sign in to continue.");
            }

            return new QuizCommitResult(false, true, GetRegistrationFailureMessage(response.Status, response.ErrorCode));
        }

        return new QuizCommitResult(false, false,
            "Your registration session expired. Please start again.");
    }

    /// <summary>Upgrades a stored legacy schedule question to its current form.</summary>
    private static IReadOnlyList<QuizQuestionDto> UpgradeLegacyScheduleQuestionIfNeeded(
        IReadOnlyList<QuizQuestionDto> serverQuestions)
    {
        if (serverQuestions.Any(q => q.QuestionType == QuizQuestionType.AvailableWeekdays) ||
            serverQuestions.All(q => q.QuestionType != QuizQuestionType.WorkoutDaysPerWeek))
            return serverQuestions;

        var weekdayAnswers = CanonicalWeekdayOptions
            .Select(option => new QuizAnswerDto(
                SyntheticWeekdayAnswerIdBase + option.OrderIndex,
                option.Label,
                option.OrderIndex,
                (int)option.Day))
            .ToArray();

        return serverQuestions
            .Select(question => question.QuestionType == QuizQuestionType.WorkoutDaysPerWeek
                ? new QuizQuestionDto(
                    question.Id,
                    AvailableWeekdaysQuestionPrompt,
                    question.OrderIndex,
                    QuizQuestionType.AvailableWeekdays,
                    weekdayAnswers)
                : question)
            .ToArray();
    }

    /// <summary>Splits the collected answers into the selections the server stores and the profile values they imply.</summary>
    private static (IReadOnlyList<QuizSelectionDto> Selections, int WorkoutDaysPerWeek)
        NormalizeSelectionsForServerCatalog(
            IReadOnlyList<QuizSelectionDto> selections,
            IReadOnlyList<QuizQuestionDto>? rawServerQuestions)
    {
        if (rawServerQuestions is null || rawServerQuestions.Count == 0)
            return (selections, DefaultWorkoutDaysPerWeek);

        var nativeWeekdaysQuestion = rawServerQuestions.FirstOrDefault(
            q => q.QuestionType == QuizQuestionType.AvailableWeekdays);
        if (nativeWeekdaysQuestion is not null)
        {
            var count = selections.Count(s => s.QuestionId == nativeWeekdaysQuestion.Id);
            var clamped = Math.Clamp(
                count > 0 ? count : DefaultWorkoutDaysPerWeek,
                MinimumWorkoutDaysPerWeek,
                MaximumWorkoutDaysPerWeek);
            return (selections, clamped);
        }

        var legacyDaysQuestion = rawServerQuestions.FirstOrDefault(
            q => q.QuestionType == QuizQuestionType.WorkoutDaysPerWeek);
        if (legacyDaysQuestion is null || legacyDaysQuestion.Answers.Count == 0)
            return (selections, DefaultWorkoutDaysPerWeek);

        var scheduleSelections = selections
            .Where(s => s.QuestionId == legacyDaysQuestion.Id)
            .ToArray();

        var directServerAnswer = scheduleSelections.Length == 1
            ? legacyDaysQuestion.Answers.FirstOrDefault(a => a.Id == scheduleSelections[0].AnswerId)
            : null;

        var requestedDays = directServerAnswer?.MappedValue
                            ?? (scheduleSelections.Length > 0 ? scheduleSelections.Length : DefaultWorkoutDaysPerWeek);
        var clampedWorkoutDays = Math.Clamp(requestedDays, MinimumWorkoutDaysPerWeek, MaximumWorkoutDaysPerWeek);

        var matchedAnswer = directServerAnswer ?? legacyDaysQuestion.Answers
            .OrderBy(a => Math.Abs(a.MappedValue - clampedWorkoutDays))
            .ThenBy(a => a.OrderIndex)
            .First();

        var normalized = selections
            .Where(s => s.QuestionId != legacyDaysQuestion.Id)
            .Append(new QuizSelectionDto(legacyDaysQuestion.Id, matchedAnswer.Id))
            .ToArray();

        return (normalized, clampedWorkoutDays);
    }

    /// <summary>The message for a refused registration, chosen from the server's code so the user is told what to fix.</summary>
    private static string GetRegistrationFailureMessage(BackendApiStatus status, string? errorCode)
    {
        if (status == BackendApiStatus.OfflineRejected)
            return "You're offline. Reconnect and try again.";
        if (status == BackendApiStatus.Unauthorized)
            return "Your email verification expired. Start registration again to get a new code.";
        if (status == BackendApiStatus.Conflict)
            return errorCode == RegistrationConflictCode
                ? "This email or username is already in use. Try signing in or choose different details."
                : "Your account could not be saved. Please try again.";

        if (status == BackendApiStatus.ValidationFailed)
        {
            return errorCode switch
            {
                nameof(CompleteAccountRegistrationStatus.InvalidEmail) =>
                    "Check your email address and start registration again.",
                nameof(CompleteAccountRegistrationStatus.InvalidUsername) =>
                    "Check your username and start registration again.",
                nameof(CompleteAccountRegistrationStatus.InvalidPassword) =>
                    "Check your password and start registration again.",
                nameof(CompleteAccountRegistrationStatus.InvalidProfile) or ConflictingCycleLengthsCode =>
                    "Check your profile details, then try again.",
                nameof(CompleteAccountRegistrationStatus.InvalidQuiz) =>
                    "We couldn't save your preferences. Please try again later.",
                nameof(CompleteAccountRegistrationStatus.InvalidWorkoutPlan) =>
                    "Your workout plan could not be prepared. Please try again.",
                PrivacyConsentRequiredCode =>
                    "Privacy consent needs to be renewed. Start registration again.",
                _ => "We couldn't finish registration. Please try again."
            };
        }

        return "We couldn't finish registration. Please try again.";
    }
}
