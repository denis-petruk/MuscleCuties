

namespace MuscleCuties.Infrastructure.Cloud;

public sealed class CloudProfilePersonalInfoStore(
    IMuscleCutiesBackendApiClient apiClient) : IProfilePersonalInfoStore
{
    private const string ProfileRoute = "/api/v1/profile";
    private const string PasswordChangeRoute = "/api/v1/auth/password/change";
    // Missing values stay missing: an absent birth date maps to default(DateTime), which the view model
    // treats as "not stored", an absent cycle length maps to 0 and an unknown experience to Unknown.
    private static readonly DateTime UnsetBirthDate = default;

    public async Task<LoadPersonalInfoResult> LoadAsync(CancellationToken cancellationToken)
    {
        var response = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken).ConfigureAwait(false);
        var snapshot = CloudStoreResponse.RequireData(response, "personal info profile");
        var birthDate = snapshot.DateOfBirth?.ToDateTime(TimeOnly.MinValue) ?? UnsetBirthDate;
        var goal = Enum.TryParse<UserGoal>(snapshot.Goal, true, out var parsedGoal) && Enum.IsDefined(parsedGoal)
            ? (int)parsedGoal
            : (int)UserGoal.MaintainHealth;
        var pace = Enum.TryParse<WeightGoalPace>(snapshot.WeightGoalPace, true, out var parsedPace) && Enum.IsDefined(parsedPace)
            ? (int)parsedPace
            : (int)WeightGoalPace.Steady;
        var experience = Enum.TryParse<TrainingExperienceLevel>(snapshot.TrainingExperienceLevel, true, out var parsedExp) &&
                         Enum.IsDefined(parsedExp)
            ? (int)parsedExp
            : (int)TrainingExperienceLevel.Unknown;
        var context = ReproductiveContextCodec.Parse(snapshot.ReproductiveContextJson);

        var profile = new PersonalInfoProfileDto(
            snapshot.Name,
            birthDate,
            (float)snapshot.Height,
            (float)snapshot.Weight,
            goal,
            pace,
            experience,
            snapshot.CycleLength > 0 ? snapshot.CycleLength : PersonalInfoConstraints.UnsetCycleLength,
            (int)context.Pregnancy,
            (int)context.Contraception);

        return new LoadPersonalInfoResult(snapshot.Email, profile);
    }

    public async Task<bool> SaveWithSnapshotAsync(SavePersonalInfoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var currentResponse = await apiClient.GetAsync<ServerProfileSnapshot>(ProfileRoute, cancellationToken).ConfigureAwait(false);
        if (currentResponse.Status != BackendApiStatus.Success || currentResponse.Data is null)
            return false;

        var goal = Enum.IsDefined(typeof(UserGoal), request.Goal)
            ? ((UserGoal)request.Goal).ToString()
            : currentResponse.Data.Goal;
        var pace = Enum.IsDefined(typeof(WeightGoalPace), request.WeightGoalPace)
            ? ((WeightGoalPace)request.WeightGoalPace).ToString()
            : currentResponse.Data.WeightGoalPace;
        // Personal info never edits experience; an unknown value echoes the stored one so it is not invented.
        var experience = Enum.IsDefined(typeof(TrainingExperienceLevel), request.TrainingExperienceLevel) &&
                         request.TrainingExperienceLevel != (int)TrainingExperienceLevel.Unknown
            ? ((TrainingExperienceLevel)request.TrainingExperienceLevel).ToString()
            : currentResponse.Data.TrainingExperienceLevel;
        var requestedBirthDate = request.BirthDate == UnsetBirthDate
            ? (DateOnly?)null
            : DateOnly.FromDateTime(request.BirthDate);
        // Only write the birth date when the person set or changed it; an unset date is never sent.
        var setDateOfBirth = requestedBirthDate is not null && requestedBirthDate != currentResponse.Data.DateOfBirth;
        // An unset cycle length keeps a stored value; when nothing is stored, it stays null
        // rather than becoming a fabricated zero.
        int? cycleLength = request.CycleLength == PersonalInfoConstraints.UnsetCycleLength
            ? currentResponse.Data.CycleLength > 0 ? currentResponse.Data.CycleLength : null
            : request.CycleLength;
        var pregnancy = Enum.IsDefined(typeof(PregnancyStatus), request.PregnancyStatus)
            ? (PregnancyStatus)request.PregnancyStatus
            : PregnancyStatus.Unknown;
        var contraception = Enum.IsDefined(typeof(ContraceptionStatus), request.ContraceptionStatus)
            ? (ContraceptionStatus)request.ContraceptionStatus
            : ContraceptionStatus.Unknown;
        var reproductiveContextJson = ReproductiveContextCodec.Serialize(new ReproductiveContext(pregnancy, contraception));

        var response = await apiClient.SendMutationAsync<object, CloudProfileWriteResponse>(
            HttpMethod.Put,
            ProfileRoute,
            new
            {
                expectedVersion = currentResponse.Data.Version,
                name = request.Name.Trim(),
                dateOfBirth = setDateOfBirth ? requestedBirthDate : null,
                setDateOfBirth,
                height = (decimal)request.Height,
                weight = (decimal)request.Weight,
                goal,
                weightGoalPace = pace,
                trainingExperienceLevel = experience,
                cycleLength,
                reproductiveContextJson
            },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        return response.Status == BackendApiStatus.Success;
    }

    public async Task<ChangeAccountPasswordResult> ChangePasswordWithSnapshotAsync(
        ChangeAccountPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await apiClient.SendMutationAsync<object, object>(
            HttpMethod.Post,
            PasswordChangeRoute,
            new { currentPassword = request.CurrentPassword, newPassword = request.NewPassword },
            idempotencyKey: Guid.NewGuid().ToString("N"),
            cancellationToken).ConfigureAwait(false);

        return response.Status == BackendApiStatus.Success
            ? new ChangeAccountPasswordResult(true, "Password updated.")
            : new ChangeAccountPasswordResult(false, response.ErrorCode ?? "Could not change password.");
    }
}
