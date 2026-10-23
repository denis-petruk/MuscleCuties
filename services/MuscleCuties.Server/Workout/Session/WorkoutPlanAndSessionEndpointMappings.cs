using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.Server.Workout.Session;

public static class WorkoutPlanAndSessionEndpointMappings
{
    public static IEndpointRouteBuilder MapWorkoutPlanAndSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/workouts").RequireAuthorization();

        group.MapGet("/overview", async (
            ClaimsPrincipal principal,
            [FromServices] PostgresWorkoutPlanAndSessionStore store,
            [FromQuery] DateOnly? startDate,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var effectiveStart = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var response = await store.LoadOverviewAsync(
                userId,
                new WorkoutOverviewRequest(effectiveStart),
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        });

        group.MapPost("/reschedule", async (
            [FromBody] RescheduleWorkoutDayRequest request,
            ClaimsPrincipal principal,
            [FromServices] PostgresWorkoutPlanAndSessionStore store,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                await store.RescheduleAsync(userId, request, cancellationToken).ConfigureAwait(false);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/day-sessions/{workoutDayId:int}", async (
            int workoutDayId,
            ClaimsPrincipal principal,
            [FromServices] PostgresWorkoutPlanAndSessionStore store,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var detail = await store.GetDetailAsync(userId, workoutDayId, cancellationToken).ConfigureAwait(false);
                return Results.Ok(detail);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest();
            }
        });

        group.MapPost("/day-sessions/log", async (
            [FromBody] LogWorkoutSessionRequest request,
            ClaimsPrincipal principal,
            [FromServices] PostgresWorkoutPlanAndSessionStore store,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                await store.LogAsync(userId, request, cancellationToken).ConfigureAwait(false);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/day-exercises/{workoutDayExerciseId:int}/swaps", async (
            int workoutDayExerciseId,
            ClaimsPrincipal principal,
            [FromServices] PostgresWorkoutPlanAndSessionStore store,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var candidates = await store.GetSwapCandidatesAsync(userId, workoutDayExerciseId, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(candidates);
        });

        group.MapPost("/day-exercises/swap", async (
            [FromBody] SwapWorkoutExerciseRequest request,
            ClaimsPrincipal principal,
            [FromServices] PostgresWorkoutPlanAndSessionStore store,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                await store.SwapAsync(userId, request, cancellationToken).ConfigureAwait(false);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/day-exercises/{workoutDayExerciseId:int}/movement-note", async (
            int workoutDayExerciseId,
            ClaimsPrincipal principal,
            [FromServices] PostgresWorkoutPlanAndSessionStore store,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var note = await store.GetMovementNoteAsync(userId, workoutDayExerciseId, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(note);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest();
            }
        });

        group.MapPut("/day-exercises/movement-note", async (
            [FromBody] SaveWorkoutMovementNoteRequest request,
            ClaimsPrincipal principal,
            [FromServices] PostgresWorkoutPlanAndSessionStore store,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                await store.SaveMovementNoteAsync(userId, request, cancellationToken).ConfigureAwait(false);
                return Results.Ok();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
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
