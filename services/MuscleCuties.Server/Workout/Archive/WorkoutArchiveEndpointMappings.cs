using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.Server.Workout.Archive;

public static class WorkoutArchiveEndpointMappings
{
    private const string HistoryRoute = "/api/v1/workouts/history";

    public static IEndpointRouteBuilder MapWorkoutArchiveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(HistoryRoute).RequireAuthorization();

        group.MapGet("/count", async (
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutArchiveUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var count = await useCase.GetTotalSessionsCountAsync(userId, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(new WorkoutHistoryCountDto(count));
        });

        group.MapGet("/month/{year:int}/{month:int}/count", async (
            int year,
            int month,
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutArchiveUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var count = await useCase.GetWorkoutHistoryCountByMonthAsync(userId, year, month, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(new WorkoutHistoryCountDto(count));
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest();
            }
        });

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
