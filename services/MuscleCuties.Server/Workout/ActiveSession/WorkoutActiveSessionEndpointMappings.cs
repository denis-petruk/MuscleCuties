using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;

namespace MuscleCuties.Server.Workout.ActiveSession;

public static class WorkoutActiveSessionEndpointMappings
{
    private const string SessionsRoute = "/api/v1/workouts/sessions";

    public static IEndpointRouteBuilder MapWorkoutActiveSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(SessionsRoute).RequireAuthorization();

        group.MapGet("/planned/{workoutDayId:int}", async (int workoutDayId, ClaimsPrincipal principal,
            [FromServices] WorkoutActiveSessionUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            var snapshot = await useCase.GetPlannedSessionAsync(userId, workoutDayId, cancellationToken).ConfigureAwait(false);
            return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
        });

        group.MapPost("/{sessionId:guid}/timing/resume", async (Guid sessionId, TimingResumeBody body,
            HttpContext context, [FromServices] WorkoutActiveSessionUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var userId)) return Results.Unauthorized();
            try
            {
                var result = await useCase.ResumeTimingAsync(userId, new ResumeWorkoutTimingCommand(
                    IdempotencyKeyResolver.Resolve(context, body.IdempotencyKey), sessionId), cancellationToken).ConfigureAwait(false);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (WorkoutActivityNoLongerSafeException exception)
            { return Results.Conflict(new { code = "WorkoutActivityNoLongerSafe", message = exception.Message }); }
            catch (PostgresIdempotencyConflictException) { return Results.Conflict(); }
            catch (InvalidOperationException) { return Results.Conflict(); }
            catch (ArgumentException) { return Results.BadRequest(); }
        });

        group.MapPost("/{sessionId:guid}/timing/checkpoint", async (Guid sessionId, TimingCheckpointBody body,
            HttpContext context, [FromServices] WorkoutActiveSessionUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var userId)) return Results.Unauthorized();
            try
            {
                var result = await useCase.CheckpointTimingAsync(userId, new CheckpointWorkoutTimingCommand(
                    IdempotencyKeyResolver.Resolve(context, body.IdempotencyKey), sessionId, body.IntervalId,
                    body.MeasuredSeconds, body.Pause), cancellationToken).ConfigureAwait(false);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (PostgresIdempotencyConflictException) { return Results.Conflict(); }
            catch (InvalidOperationException) { return Results.Conflict(); }
            catch (ArgumentException) { return Results.BadRequest(); }
        });

        group.MapGet("/{sessionId:guid}", async (
            Guid sessionId,
            ClaimsPrincipal principal,
            [FromServices] WorkoutActiveSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();
            var snapshot = await useCase.GetSessionAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
            return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
        });

        group.MapPost("/start", async (
            StartWorkoutSessionBody body,
            HttpContext context,
            [FromServices] WorkoutActiveSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var userId))
                return Results.Unauthorized();
            var idempotencyKey = IdempotencyKeyResolver.Resolve(context, body.IdempotencyKey);
            try
            {
                var snapshot = await useCase.StartSessionAsync(
                    userId,
                    new StartWorkoutSessionCommand(
                        idempotencyKey,
                        body.SessionId,
                        body.WorkoutName,
                        body.StartedAtUtc,
                        body.Notes,
                        body.PlannedWorkoutDayId),
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(snapshot);
            }
            catch (PostgresIdempotencyConflictException)
            {
                return Results.Conflict();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
        });

        group.MapPut("/{sessionId:guid}/sets", async (
            Guid sessionId,
            UpsertWorkoutSessionSetBody body,
            HttpContext context,
            [FromServices] WorkoutActiveSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var userId))
                return Results.Unauthorized();
            var idempotencyKey = IdempotencyKeyResolver.Resolve(context, body.IdempotencyKey);
            try
            {
                var snapshot = await useCase.UpsertSetAsync(
                    userId,
                    new UpsertWorkoutSessionSetCommand(idempotencyKey, sessionId, body.Set),
                    cancellationToken).ConfigureAwait(false);
                return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
            }
            catch (PostgresIdempotencyConflictException)
            {
                return Results.Conflict();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
        });

        group.MapPost("/{sessionId:guid}/finish", async (
            Guid sessionId,
            FinishWorkoutSessionBody body,
            HttpContext context,
            [FromServices] WorkoutActiveSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var userId))
                return Results.Unauthorized();
            var idempotencyKey = IdempotencyKeyResolver.Resolve(context, body.IdempotencyKey);
            try
            {
                var snapshot = await useCase.FinishSessionAsync(
                    userId,
                    new FinishWorkoutSessionCommand(
                        idempotencyKey,
                        sessionId,
                        body.CompletedAtUtc,
                        body.TotalDurationSeconds,
                        body.RpeRating,
                        body.Notes,
                        body.FinalSets),
                    cancellationToken).ConfigureAwait(false);
                return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
            }
            catch (PostgresIdempotencyConflictException)
            {
                return Results.Conflict();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
        });

        group.MapPost("/{sessionId:guid}/cancel", async (
            Guid sessionId,
            CancelWorkoutSessionBody body,
            HttpContext context,
            [FromServices] WorkoutActiveSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var userId))
                return Results.Unauthorized();
            var idempotencyKey = IdempotencyKeyResolver.Resolve(context, body.IdempotencyKey);
            try
            {
                var snapshot = await useCase.CancelSessionAsync(
                    userId,
                    new CancelWorkoutSessionCommand(idempotencyKey, sessionId),
                    cancellationToken).ConfigureAwait(false);
                return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
            }
            catch (PostgresIdempotencyConflictException)
            {
                return Results.Conflict();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
        });

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;

    public sealed record StartWorkoutSessionBody(
        string? IdempotencyKey,
        Guid SessionId,
        string WorkoutName,
        DateTime StartedAtUtc,
        string? Notes = null,
        int? PlannedWorkoutDayId = null);

    public sealed record UpsertWorkoutSessionSetBody(
        string? IdempotencyKey,
        ActiveExerciseSetInput Set);

    public sealed record FinishWorkoutSessionBody(
        string? IdempotencyKey,
        DateTime CompletedAtUtc,
        int TotalDurationSeconds,
        int? RpeRating = null,
        string? Notes = null,
        IReadOnlyList<ActiveExerciseSetInput>? FinalSets = null);

    public sealed record CancelWorkoutSessionBody(string? IdempotencyKey);
    public sealed record TimingResumeBody(string? IdempotencyKey);
    public sealed record TimingCheckpointBody(string? IdempotencyKey, Guid IntervalId, int MeasuredSeconds, bool Pause);
}
