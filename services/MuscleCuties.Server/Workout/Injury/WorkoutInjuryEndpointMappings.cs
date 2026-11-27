using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.Server.Workout.Injury;

/// <summary>The routes that read injuries back for the workout screens.</summary>
public static class WorkoutInjuryEndpointMappings
{
    private const string InjuriesRoute = "/api/v1/workouts/injuries";

    /// <summary>Registers the workout injury endpoints.</summary>
    public static IEndpointRouteBuilder MapWorkoutInjuryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(InjuriesRoute).RequireAuthorization();

        group.MapGet("/impact", async (
            ClaimsPrincipal principal,
            [FromServices] ServerWorkoutInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var impact = await useCase.GetImpactAsync(userId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(impact);
        });

        return endpoints;
    }

}
