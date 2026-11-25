using System.Security.Claims;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Server.Workout.Planning;

/// <summary>The workout-progress routes used by the trends card.</summary>
public static class WorkoutProgressEndpointMappings
{
    private const string ProgressRoute = "/api/v1/workouts/progress";
    private const string ForegroundTimeZoneRoute = "/api/v1/workouts/foreground-timezone";
    private const string ReadyStatus = "Ready";

    /// <summary>Registers the progress endpoints.</summary>
    public static IEndpointRouteBuilder MapWorkoutProgressEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ProgressRoute, async (ClaimsPrincipal principal, IWorkoutProgressStore store,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();
            return Results.Ok(await store.ReadAsync(userId, cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization();

        endpoints.MapPut(ForegroundTimeZoneRoute, async (WorkoutForegroundTimeZoneRequest request,
            ClaimsPrincipal principal, IWorkoutProgressStore store, CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();
            var result = await store.AcceptForegroundTimeZoneAsync(userId, request, cancellationToken).ConfigureAwait(false);
            return result.Status == ReadyStatus ? Results.Ok(result) : Results.Conflict(result);
        }).RequireAuthorization();
        return endpoints;
    }
}
