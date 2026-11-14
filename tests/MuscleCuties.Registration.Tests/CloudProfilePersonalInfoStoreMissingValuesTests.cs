using System.Text.Json;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo;
using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using Xunit;

namespace MuscleCuties.Registration.Tests;

/// <summary>
/// S-02: missing personal info stays missing. The store never invents a birth date, an
/// experience level or a cycle length, and it writes the birth date only when it changed.
/// </summary>
public sealed class CloudProfilePersonalInfoStoreMissingValuesTests
{
    private static readonly DateOnly StoredBirthDate = new(1996, 3, 2);

    [Fact]
    public async Task Missing_values_load_as_unset_instead_of_defaults()
    {
        var store = new CloudProfilePersonalInfoStore(new FakeApiClient(Snapshot(dateOfBirth: null, experience: "",
            cycleLength: 0)));

        var profile = Assert.IsType<PersonalInfoProfileDto>((await store.LoadAsync(CancellationToken.None)).Profile);

        Assert.Equal(default, profile.DateOfBirth);
        Assert.Equal((int)TrainingExperienceLevel.Unknown, profile.TrainingExperienceLevel);
        Assert.Equal(PersonalInfoConstraints.UnsetCycleLength, profile.CycleLength);
    }

    [Fact]
    public async Task Stored_values_load_unchanged()
    {
        var store = new CloudProfilePersonalInfoStore(new FakeApiClient(Snapshot(StoredBirthDate, "Beginner", 30)));

        var profile = Assert.IsType<PersonalInfoProfileDto>((await store.LoadAsync(CancellationToken.None)).Profile);

        Assert.Equal(StoredBirthDate.ToDateTime(TimeOnly.MinValue), profile.DateOfBirth);
        Assert.Equal((int)TrainingExperienceLevel.Beginner, profile.TrainingExperienceLevel);
        Assert.Equal(30, profile.CycleLength);
    }

    [Fact]
    public async Task Unchanged_birth_date_and_unset_values_are_not_written()
    {
        var api = new FakeApiClient(Snapshot(StoredBirthDate, "", 0));
        var store = new CloudProfilePersonalInfoStore(api);

        Assert.True(await store.SaveWithSnapshotAsync(Request(StoredBirthDate.ToDateTime(TimeOnly.MinValue),
            TrainingExperienceLevel.Unknown, PersonalInfoConstraints.UnsetCycleLength), CancellationToken.None));

        var body = api.LastBody();
        Assert.False(body.GetProperty("setDateOfBirth").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("dateOfBirth").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("cycleLength").ValueKind);
        // The stored (empty) experience is echoed; Unknown is never written as a new value.
        Assert.Equal(string.Empty, body.GetProperty("trainingExperienceLevel").GetString());
    }

    [Fact]
    public async Task A_birth_date_the_person_set_is_written()
    {
        var api = new FakeApiClient(Snapshot(dateOfBirth: null, "Beginner", 28));
        var store = new CloudProfilePersonalInfoStore(api);
        var chosen = new DateTime(1994, 7, 9);

        Assert.True(await store.SaveWithSnapshotAsync(
            Request(chosen, TrainingExperienceLevel.Beginner, 28), CancellationToken.None));

        var body = api.LastBody();
        Assert.True(body.GetProperty("setDateOfBirth").GetBoolean());
        Assert.Equal("1994-07-09", body.GetProperty("dateOfBirth").GetString());
        Assert.Equal(28, body.GetProperty("cycleLength").GetInt32());
    }

    [Fact]
    public async Task Use_case_blocks_an_unset_birth_date_and_accepts_an_unset_cycle_length()
    {
        var api = new FakeApiClient(Snapshot(StoredBirthDate, "Beginner", 0));
        var useCase = new ProfilePersonalInfoUseCase(new CloudProfilePersonalInfoStore(api));

        var unsetBirthDate = await useCase.SaveAsync(Request(default, TrainingExperienceLevel.Beginner, 0));
        var unsetCycle = await useCase.SaveAsync(Request(StoredBirthDate.ToDateTime(TimeOnly.MinValue),
            TrainingExperienceLevel.Beginner, PersonalInfoConstraints.UnsetCycleLength));

        Assert.Equal(PersonalInfoSaveError.InvalidBirthDate, unsetBirthDate.Error);
        Assert.True(unsetCycle.Succeeded);
        Assert.Single(api.Mutations);
    }

    [Fact]
    public void View_model_treats_a_default_birth_date_as_not_stored_and_blocks_saving_it()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "ViewModels",
            "Profile", "ProfilePersonalInfoViewModel.cs"));

        Assert.Contains("_hasStoredBirthDate = profile.DateOfBirth != default;", source, StringComparison.Ordinal);
        Assert.Contains("if (!_hasStoredBirthDate && !_birthDateWasExplicitlyChanged)", source, StringComparison.Ordinal);
        Assert.Contains("Ui_Profile_Error_BirthDateRequired", source, StringComparison.Ordinal);
    }

    private static SavePersonalInfoRequest Request(DateTime birthDate, TrainingExperienceLevel experience,
        int cycleLength) =>
        new("person@example.test", "Synthetic", birthDate, 165f, 60f, (int)UserGoal.MaintainHealth,
            (int)WeightGoalPace.Steady, (int)experience, cycleLength);

    private static ServerProfileSnapshot Snapshot(DateOnly? dateOfBirth, string experience, int cycleLength) =>
        new("Synthetic", dateOfBirth, 165m, 60m, "MaintainHealth", "Steady", experience, 3, "[1,3,5]",
            cycleLength, "", "{}", "FullGym", "GeneralStrength", "Metric", "kg", "g", "cm", "km", "kcal", "{}",
            Version: 7, Email: "person@example.test");

    private static string RepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(current, "MuscleCuties.sln")))
            current = Directory.GetParent(current)?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        return current;
    }

    private sealed class FakeApiClient(ServerProfileSnapshot snapshot) : IMuscleCutiesBackendApiClient
    {
        public List<object?> Mutations { get; } = [];

        public JsonElement LastBody() =>
            JsonSerializer.SerializeToElement(Mutations[^1], new JsonSerializerOptions(JsonSerializerDefaults.Web));

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, (TResponse)(object)snapshot));

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(HttpMethod method,
            string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Mutations.Add(payload);
            return Task.FromResult(new BackendApiResponse<TResponse>(BackendApiStatus.Success, default));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackendApiResponse<bool>(BackendApiStatus.Success, true));
    }
}
