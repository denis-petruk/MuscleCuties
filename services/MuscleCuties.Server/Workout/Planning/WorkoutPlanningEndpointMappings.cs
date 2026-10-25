using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.Server.Workout.Planning;

public static class WorkoutPlanningEndpointMappings
{
    private const string CatalogRoute = "/api/v1/workouts/catalog";
    private const string PlanRoute = "/api/v1/workouts/plan";
    private const string StrengthPreviewRoute = "/api/v1/workouts/plan/strength-preview";
    private const string ExerciseExclusionCatalogRoute = "/api/v1/workouts/plan/exercise-exclusions";
    private const string StrengthProposalsRoute = "/api/v1/workouts/plan/strength-proposals";
    private const string StrengthProposalAcceptRoute =
        "/api/v1/workouts/plan/strength-proposals/{proposalId:guid}/accept";

    public static IEndpointRouteBuilder MapWorkoutPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(CatalogRoute, async (
                [FromServices] GetWorkoutPlanningCatalogUseCase catalog,
                CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ExecuteAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization();

        endpoints.MapGet(ExerciseExclusionCatalogRoute, async (
                ClaimsPrincipal principal,
                [FromServices] GetReviewedExerciseExclusionCatalogV020UseCase catalog,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetUserId(principal, out _))
                    return Results.Unauthorized();
                return Results.Ok(await catalog.ExecuteAsync(cancellationToken).ConfigureAwait(false));
            })
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

        endpoints.MapPost(StrengthPreviewRoute, async (
                StrengthPlanPreviewRequestV020 request,
                ClaimsPrincipal principal,
                [FromServices] PreviewStrengthPlanV020UseCase preview,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetUserId(principal, out var ownerUserId))
                    return Results.Unauthorized();
                var result = await preview.ExecuteAsync(ownerUserId, request, cancellationToken)
                    .ConfigureAwait(false);
                return result.Status == "InvalidInput"
                    ? Results.BadRequest(result)
                    : Results.Ok(result);
            })
            .RequireAuthorization();

        endpoints.MapPost(StrengthProposalsRoute, async (
                StrengthPlanPreviewRequestV020 request,
                ClaimsPrincipal principal,
                [FromServices] CreateStrengthPlanProposalV020UseCase proposals,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetUserId(principal, out var ownerUserId))
                    return Results.Unauthorized();

                var result = await proposals.ExecuteAsync(ownerUserId, request, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .RequireAuthorization();

        endpoints.MapPost(StrengthProposalAcceptRoute, async (
                Guid proposalId,
                ClaimsPrincipal principal,
                [FromServices] AcceptStrengthPlanProposalV020UseCase proposals,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetUserId(principal, out var ownerUserId))
                    return Results.Unauthorized();

                var request = new AcceptStrengthPlanProposalRequestV020(proposalId);
                var result = await proposals.ExecuteAsync(ownerUserId, request, cancellationToken)
                    .ConfigureAwait(false);
                return result.Status switch
                {
                    "Accepted" => Results.Ok(result),
                    "InvalidInput" => Results.BadRequest(result),
                    "NotFound" => Results.NotFound(result),
                    "Conflict" or "Stale" => Results.Conflict(result),
                    "Limited" or "InvalidProposal" => Results.UnprocessableEntity(result),
                    _ => Results.Conflict(result)
                };
            })
            .RequireAuthorization();

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
