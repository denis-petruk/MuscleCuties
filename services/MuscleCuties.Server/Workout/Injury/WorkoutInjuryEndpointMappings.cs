using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.Server.Workout.Injury;

public static class WorkoutInjuryEndpointMappings
{
    private const string InjuriesRoute = "/api/v1/workouts/injuries";

    public static IEndpointRouteBuilder MapWorkoutInjuryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(InjuriesRoute).RequireAuthorization();

        group.MapGet("/impact", async (
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var impact = await useCase.GetImpactAsync(userId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(impact);
        });

        group.MapGet("/exercises/{exerciseDefinitionId:int}/contraindicated", async (
            int exerciseDefinitionId,
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var check = await useCase.CheckContraindicationAsync(userId, exerciseDefinitionId, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(check);
        });

        group.MapGet("/exercises/{exerciseDefinitionId:int}/swaps", async (
            int exerciseDefinitionId,
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var candidates = await useCase.GetSafeSwapCandidatesAsync(userId, exerciseDefinitionId, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(candidates);
        });

        group.MapPost("/swap", async (
            [FromBody] SwapExerciseRequest request,
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var success = await useCase.SwapExerciseAsync(
                    userId,
                    request.WorkoutDayExerciseId,
                    request.NewExerciseDefinitionId,
                    request.SavePreference,
                    cancellationToken).ConfigureAwait(false);

                if (!success)
                    return Results.NotFound(new SwapExerciseResponse(false, "Workout day exercise not found."));

                return Results.Ok(new SwapExerciseResponse(true, "Exercise swapped successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new SwapExerciseResponse(false, ex.Message));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new SwapExerciseResponse(false, ex.Message));
            }
        });

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
