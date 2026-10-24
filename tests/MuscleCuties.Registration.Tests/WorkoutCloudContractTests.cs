using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.Server.Workout.ActiveSession;
using MuscleCuties.Server.Workout.Archive;
using MuscleCuties.Server.Workout.Injury;
using MuscleCuties.Server.Workout.Planning;
using MuscleCuties.Server.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.Archive;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.Registration.Tests;

public sealed class WorkoutCloudContractTests
{
    private static readonly DateOnly WorkoutDate = new(2026, 10, 19);
    private static readonly Guid SessionId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SetId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Xunit.Fact]
    public async Task CloudReviewedExerciseExclusionStoreReadsOnlyTheReviewedCatalogRoute()
    {
        var expected = new[] { new ReviewedExerciseExclusionOptionV020(101, "Barbell Hip Thrust") };
        var api = new RecordingWorkoutApiClient((_, responseType) =>
            responseType == typeof(ReviewedExerciseExclusionOptionV020[]) ? expected : null);
        var store = new CloudReviewedExerciseExclusionCatalogV020Store(api);

        var choices = await store.LoadAsync();

        Xunit.Assert.Equal(expected, choices);
        var call = Xunit.Assert.Single(api.Calls);
        Xunit.Assert.Equal("GET", call.Method);
        Xunit.Assert.Equal("/api/v1/workouts/plan/exercise-exclusions", call.Path);
    }

    [Xunit.Fact]
    public async Task CloudStoresPreserveWorkoutRoutesIdsAndMutationBodies()
    {
        var api = new RecordingWorkoutApiClient((path, responseType) => responseType == typeof(WorkoutOverviewResponse)
            ? new WorkoutOverviewResponse("Plan", "Follicular", [], [], [])
            : responseType == typeof(WorkoutSessionDetailDto)
                ? new WorkoutSessionDetailDto(101, "Workout", "", "", false, [], [])
                : responseType == typeof(List<WorkoutSwapCandidateDto>)
                    ? new List<WorkoutSwapCandidateDto> { new(19, "Swap", "match", "Bodyweight", "Glutes") }
                    : responseType == typeof(WorkoutMovementNoteDto)
                        ? new WorkoutMovementNoteDto(501, "Note")
                        : responseType == typeof(WorkoutArchiveDetailDto)
                            ? new WorkoutArchiveDetailDto(SessionId, "Workout", DateTime.UnixEpoch, null, 0, 0, null, null, [])
                            : responseType == typeof(WorkoutHistoryCountDto)
                                ? new WorkoutHistoryCountDto(1)
                                : responseType == typeof(List<HistoryMonthOptionDto>)
                                    ? new List<HistoryMonthOptionDto> { new(2026, 10) }
                                    : responseType == typeof(List<WorkoutArchiveSummaryDto>)
                                        ? new List<WorkoutArchiveSummaryDto>()
                                        : null);

        var overview = new CloudWorkoutOverviewStore(api);
        var session = new CloudWorkoutSessionActionStore(api);
        var archive = new CloudWorkoutArchiveStore(api);
        var reschedule = new RescheduleWorkoutDayRequest(101, WorkoutDate.AddDays(1));
        var log = new LogWorkoutSessionRequest(101, WorkoutDate, [new WorkoutExerciseLogDto(501, 12, 3, 10, 60f)]);
        var swap = new SwapWorkoutExerciseRequest(501, 19, true);
        var note = new SaveWorkoutMovementNoteRequest(501, "Note");

        await overview.LoadAsync(new WorkoutOverviewRequest(WorkoutDate), CancellationToken.None);
        await overview.RescheduleAsync(reschedule, CancellationToken.None);
        await session.GetDetailAsync(101, CancellationToken.None);
        await session.LogAsync(log, CancellationToken.None);
        await session.GetSwapCandidatesAsync(501, CancellationToken.None);
        await session.SwapAsync(swap, CancellationToken.None);
        await session.GetMovementNoteAsync(501, CancellationToken.None);
        await session.SaveMovementNoteAsync(note, CancellationToken.None);
        await archive.GetWorkoutHistoryAsync(25, 5);
        await archive.GetAvailableHistoryMonthsAsync();
        await archive.GetWorkoutHistoryByMonthAsync(2026, 10, 25, 5);
        await archive.GetTotalSessionsCountAsync();
        await archive.GetWorkoutHistoryCountByMonthAsync(2026, 10);
        await archive.GetWorkoutSessionDetailAsync(SessionId);
        await archive.UpdateWorkoutSessionNotesAsync(SessionId, "Recovered", 7);
        await archive.UpdateExerciseSetAsync(SetId, 62.5, 8);

        Xunit.Assert.Equal(new (string Method, string Path)[]
        {
            ("GET", "/api/v1/workouts/overview?startDate=2026-10-19"),
            ("POST", "/api/v1/workouts/reschedule"),
            ("GET", "/api/v1/workouts/day-sessions/101"),
            ("POST", "/api/v1/workouts/day-sessions/log"),
            ("GET", "/api/v1/workouts/day-exercises/501/swaps"),
            ("POST", "/api/v1/workouts/day-exercises/swap"),
            ("GET", "/api/v1/workouts/day-exercises/501/movement-note"),
            ("PUT", "/api/v1/workouts/day-exercises/movement-note"),
            ("GET", "/api/v1/workouts/history?limit=25&offset=5"),
            ("GET", "/api/v1/workouts/history/months"),
            ("GET", "/api/v1/workouts/history/month/2026/10?limit=25&offset=5"),
            ("GET", "/api/v1/workouts/history/count"),
            ("GET", "/api/v1/workouts/history/month/2026/10/count"),
            ("GET", $"/api/v1/workouts/history/sessions/{SessionId:D}"),
            ("PUT", $"/api/v1/workouts/history/sessions/{SessionId:D}/notes"),
            ("PUT", $"/api/v1/workouts/history/sets/{SetId:D}")
        }, api.Calls.Select(call => (call.Method, call.Path)));
        Xunit.Assert.Same(reschedule, api.Calls[1].Body);
        Xunit.Assert.Same(log, api.Calls[3].Body);
        Xunit.Assert.Same(swap, api.Calls[5].Body);
        Xunit.Assert.Same(note, api.Calls[7].Body);
        Xunit.Assert.Equal(new UpdateWorkoutSessionNotesRequest("Recovered", 7), api.Calls[14].Body);
        Xunit.Assert.Equal(new UpdateExerciseSetRequest(62.5, 8), api.Calls[15].Body);
    }

    [Xunit.Theory]
    [Xunit.InlineData(BackendApiStatus.OfflineRejected)]
    [Xunit.InlineData(BackendApiStatus.Unauthorized)]
    [Xunit.InlineData(BackendApiStatus.Conflict)]
    public async Task FailedWorkoutWritesAreReportedToCaller(BackendApiStatus status)
    {
        var api = new RecordingWorkoutApiClient((_, _) => null) { Status = status };
        var store = new CloudWorkoutSessionActionStore(api);

        var failure = await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.LogAsync(new LogWorkoutSessionRequest(101, WorkoutDate, []), CancellationToken.None));

        Xunit.Assert.Contains(status.ToString(), failure.Message, StringComparison.Ordinal);
    }

    [Xunit.Fact]
    public void WorkoutServerRoutesRequireAuthorization()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<GetWorkoutPlanningCatalogUseCase>();
        builder.Services.AddScoped<GenerateWorkoutPlanUseCase>();
        builder.Services.AddScoped<PostgresWorkoutPlanAndSessionStore>();
        builder.Services.AddScoped<WorkoutActiveSessionUseCase>();
        builder.Services.AddScoped<ServerWorkoutArchiveUseCase>();
        builder.Services.AddScoped<ServerWorkoutInjuryUseCase>();
        using var app = builder.Build();
        app.MapWorkoutPlanningEndpoints();
        app.MapWorkoutPlanAndSessionEndpoints();
        app.MapWorkoutActiveSessionEndpoints();
        app.MapWorkoutArchiveEndpoints();
        app.MapWorkoutInjuryEndpoints();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToDictionary(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single(),
                endpoint.RoutePattern.RawText!.TrimEnd('/')), endpoint => endpoint);

        foreach (var route in new (string Method, string Path)[]
        {
            ("GET", "/api/v1/workouts/catalog"), ("POST", "/api/v1/workouts/plan"),
            ("GET", "/api/v1/workouts/plan/exercise-exclusions"),
            ("GET", "/api/v1/workouts/overview"), ("POST", "/api/v1/workouts/reschedule"),
            ("GET", "/api/v1/workouts/day-sessions/{workoutDayId:int}"),
            ("POST", "/api/v1/workouts/day-sessions/log"),
            ("GET", "/api/v1/workouts/day-exercises/{workoutDayExerciseId:int}/swaps"),
            ("POST", "/api/v1/workouts/day-exercises/swap"),
            ("GET", "/api/v1/workouts/day-exercises/{workoutDayExerciseId:int}/movement-note"),
            ("PUT", "/api/v1/workouts/day-exercises/movement-note"),
            ("GET", "/api/v1/workouts/sessions/{sessionId:guid}"),
            ("POST", "/api/v1/workouts/sessions/start"),
            ("PUT", "/api/v1/workouts/sessions/{sessionId:guid}/sets"),
            ("POST", "/api/v1/workouts/sessions/{sessionId:guid}/finish"),
            ("POST", "/api/v1/workouts/sessions/{sessionId:guid}/cancel"),
            ("GET", "/api/v1/workouts/history"), ("GET", "/api/v1/workouts/history/months"),
            ("GET", "/api/v1/workouts/history/month/{year:int}/{month:int}"),
            ("GET", "/api/v1/workouts/history/count"),
            ("GET", "/api/v1/workouts/history/month/{year:int}/{month:int}/count"),
            ("GET", "/api/v1/workouts/history/sessions/{sessionId:guid}"),
            ("PUT", "/api/v1/workouts/history/sessions/{sessionId:guid}/notes"),
            ("PUT", "/api/v1/workouts/history/sets/{setId:guid}"),
            ("GET", "/api/v1/workouts/history/personal-records"),
            ("GET", "/api/v1/workouts/injuries/impact"),
            ("GET", "/api/v1/workouts/injuries/exercises/{exerciseDefinitionId:int}/contraindicated"),
            ("GET", "/api/v1/workouts/injuries/exercises/{exerciseDefinitionId:int}/swaps"),
            ("POST", "/api/v1/workouts/injuries/swap")
        })
        {
            Xunit.Assert.True(routes.TryGetValue(route, out var endpoint), $"Missing {route.Method} {route.Path}");
            Xunit.Assert.NotNull(endpoint!.Metadata.GetMetadata<IAuthorizeData>());
        }

        foreach (var route in new (string Method, string Path)[]
        {
            ("GET", "/api/v1/workout/history"), ("GET", "/api/v1/workout/history/months"),
            ("GET", "/api/v1/workout/history/month/{year:int}/{month:int}"),
            ("GET", "/api/v1/workout/history/count"),
            ("GET", "/api/v1/workout/history/month/{year:int}/{month:int}/count"),
            ("GET", "/api/v1/workout/history/sessions/{sessionId:guid}"),
            ("PUT", "/api/v1/workout/history/sessions/{sessionId:guid}/notes"),
            ("PUT", "/api/v1/workout/history/sets/{setId:guid}"),
            ("GET", "/api/v1/workout/history/personal-records"),
            ("GET", "/api/v1/workouts/injuries/contraindicated/{exerciseDefinitionId:int}"),
            ("GET", "/api/v1/workouts/injuries/swaps/{exerciseDefinitionId:int}"),
            ("GET", "/api/v1/workout/injuries/impact"),
            ("GET", "/api/v1/workout/injuries/exercises/{exerciseDefinitionId:int}/contraindicated"),
            ("GET", "/api/v1/workout/injuries/contraindicated/{exerciseDefinitionId:int}"),
            ("GET", "/api/v1/workout/injuries/exercises/{exerciseDefinitionId:int}/swaps"),
            ("GET", "/api/v1/workout/injuries/swaps/{exerciseDefinitionId:int}"),
            ("POST", "/api/v1/workout/injuries/swap"),
            ("GET", "/api/v1/workouts/exercises/{exerciseDefinitionId:int}/contraindicated"),
            ("GET", "/api/v1/workouts/exercises/{exerciseDefinitionId:int}/swaps"),
            ("POST", "/api/v1/workouts/exercises/swap"),
            ("GET", "/api/v1/workout/exercises/{exerciseDefinitionId:int}/contraindicated"),
            ("GET", "/api/v1/workout/exercises/{exerciseDefinitionId:int}/swaps"),
            ("POST", "/api/v1/workout/exercises/swap")
        })
        {
            Xunit.Assert.True(routes.TryGetValue(route, out var endpoint), $"Missing {route.Method} {route.Path}");
            Xunit.Assert.NotNull(endpoint!.Metadata.GetMetadata<IAuthorizeData>());
        }
    }

    private sealed record RecordedCall(string Method, string Path, object? Body);

    private sealed class RecordingWorkoutApiClient(Func<string, Type, object?> responder) : IMuscleCutiesBackendApiClient
    {
        public BackendApiStatus Status { get; init; } = BackendApiStatus.Success;
        public List<RecordedCall> Calls { get; } = [];

        public Task<BackendApiResponse<TResponse>> GetAsync<TResponse>(string relativePath,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new RecordedCall("GET", relativePath, null));
            var data = responder(relativePath, typeof(TResponse));
            return Task.FromResult(new BackendApiResponse<TResponse>(Status, data is TResponse typed ? typed : default));
        }

        public Task<BackendApiResponse<TResponse>> SendMutationAsync<TRequest, TResponse>(HttpMethod method,
            string relativePath, TRequest payload, string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new RecordedCall(method.Method, relativePath, payload));
            var data = responder(relativePath, typeof(TResponse));
            return Task.FromResult(new BackendApiResponse<TResponse>(Status, data is TResponse typed ? typed : default));
        }

        public Task<BackendApiResponse<bool>> DeleteAsync(string relativePath, string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
