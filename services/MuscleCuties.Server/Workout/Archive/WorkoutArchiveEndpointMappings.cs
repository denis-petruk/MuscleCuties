using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.Server.Workout.Archive;

public static class WorkoutArchiveEndpointMappings
{
    private const string HistoryRoutePlural = "/api/v1/workouts/history";
    private const string HistoryRouteSingular = "/api/v1/workout/history";

    public static IEndpointRouteBuilder MapWorkoutArchiveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapGroup(endpoints.MapGroup(HistoryRoutePlural).RequireAuthorization());
        MapGroup(endpoints.MapGroup(HistoryRouteSingular).RequireAuthorization());

        return endpoints;
    }

    private static void MapGroup(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutArchiveUseCase useCase,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var safeLimit = limit.GetValueOrDefault(50);
            var safeOffset = offset.GetValueOrDefault(0);
            var sessions = await useCase.GetWorkoutHistoryAsync(userId, safeLimit, safeOffset, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(sessions);
        });

        group.MapGet("/months", async (
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutArchiveUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var months = await useCase.GetAvailableHistoryMonthsAsync(userId, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(months);
        });

        group.MapGet("/month/{year:int}/{month:int}", async (
            int year,
            int month,
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutArchiveUseCase useCase,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var safeLimit = limit.GetValueOrDefault(100);
            var safeOffset = offset.GetValueOrDefault(0);
            try
            {
                var sessions = await useCase.GetWorkoutHistoryByMonthAsync(
                    userId, year, month, safeLimit, safeOffset, cancellationToken).ConfigureAwait(false);
                return Results.Ok(sessions);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest();
            }
        });

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

        group.MapGet("/sessions/{sessionId:guid}", async (
            Guid sessionId,
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutArchiveUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var detail = await useCase.GetWorkoutSessionDetailAsync(userId, sessionId, cancellationToken)
                .ConfigureAwait(false);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        group.MapPut("/sessions/{sessionId:guid}/notes", async (
            Guid sessionId,
            [FromBody] UpdateWorkoutSessionNotesRequest request,
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutArchiveUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var updated = await useCase.UpdateWorkoutSessionNotesAsync(
                    userId, sessionId, request.Notes, request.RpeRating, cancellationToken)
                    .ConfigureAwait(false);
                return updated ? Results.Ok() : Results.NotFound();
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest();
            }
        });

        group.MapPut("/sets/{setId:guid}", async (
            Guid setId,
            [FromBody] UpdateExerciseSetRequest request,
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutArchiveUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var updated = await useCase.UpdateExerciseSetAsync(
                    userId, setId, request.WeightKg, request.RepsCompleted, cancellationToken)
                    .ConfigureAwait(false);
                return updated ? Results.Ok() : Results.NotFound();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
        });

        group.MapGet("/personal-records", async (
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutArchiveUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var records = await useCase.GetPersonalRecordsAsync(userId, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(records);
        });
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
