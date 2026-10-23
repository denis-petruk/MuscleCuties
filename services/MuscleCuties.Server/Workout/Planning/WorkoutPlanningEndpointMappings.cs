using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.Server.Workout.Planning;

public static class WorkoutPlanningEndpointMappings
{
    private const string CatalogRoute = "/api/v1/workouts/catalog";
    private const string PlanRoute = "/api/v1/workouts/plan";

    public static IEndpointRouteBuilder MapWorkoutPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(CatalogRoute, async (
                [FromServices] GetWorkoutPlanningCatalogUseCase catalog,
                CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ExecuteAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization();

        endpoints.MapPost(PlanRoute, async (
                GenerateWorkoutPlanRequest request,
                ClaimsPrincipal principal,
                [FromServices] GenerateWorkoutPlanUseCase planner,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetUserId(principal, out var ownerUserId))
                    return Results.Unauthorized();
                try
                {
                    var plan = await planner.ExecuteAsync(ownerUserId, request, cancellationToken).ConfigureAwait(false);
                    return Results.Ok(plan);
                }
                catch (InvalidOperationException exception)
                {
                    return Results.UnprocessableEntity(new { error = exception.Message });
                }
            })
            .RequireAuthorization();

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
