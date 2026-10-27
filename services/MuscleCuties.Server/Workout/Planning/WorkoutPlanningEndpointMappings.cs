using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.Server.Workout.Planning;

public static class WorkoutPlanningEndpointMappings
{
    private const string CatalogRoute = "/api/v1/workouts/catalog";
    private const string LegacyPlanRoute = "/api/v1/workouts/plan";
    private const string StrengthPreviewRoute = "/api/v1/workouts/plan/strength-preview";
    private const string ExerciseExclusionCatalogRoute = "/api/v1/workouts/plan/exercise-exclusions";
    private const string StrengthProposalsRoute = "/api/v1/workouts/plan/strength-proposals";
    private const string StrengthProposalAcceptRoute =
        "/api/v1/workouts/plan/strength-proposals/{proposalId:guid}/accept";
    private const string LeaseActionRoute = "/api/v1/workouts/plan/lease/{action}";
    private const string AnswersRoute = "/api/v1/workouts/plan/answers";

    private const string StatusAccepted = "Accepted";
    private const string StatusInvalidInput = "InvalidInput";
    private const string StatusNotFound = "NotFound";
    private const string StatusConflict = "Conflict";
    private const string StatusStale = "Stale";
    private const string StatusLimited = "Limited";
    private const string StatusInvalidProposal = "InvalidProposal";

    private const string LeaseActionRenew = "renew";
    private const string LeaseActionClose = "close";
    private const string LeaseActionTakeover = "takeover";

    private const string ErrorCodeWorkoutPreviewOpen = "WorkoutPreviewOpen";
    private const string ErrorCodeWorkoutPlanningInputsChanged = "WorkoutPlanningInputsChanged";
    private const string ErrorCodeWorkoutAnswersUnavailable = "WorkoutAnswersUnavailable";
    private const string ErrorCodeLegacyPlanBypassDisabled = "LegacyPlanBypassDisabled";
    private const string ErrorMessageLegacyPlanBypassDisabled =
        "Direct plan generation is retired. All plans must be created via strength proposals and accepted through canonical lifecycle.";
    private const string ErrorMessageLegacyPlanReadDisabled =
        "Direct plan reads are retired. Query /api/v1/workouts/overview or /api/v1/workouts/progress instead.";

    public static IEndpointRouteBuilder MapWorkoutPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(CatalogRoute, async (
                [FromServices] GetWorkoutPlanningCatalogUseCase catalog,
                CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ExecuteAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization();

        // Legacy plan route rejects direct generation/activation bypasses and delegates to canonical lifecycle.
        endpoints.MapPost(LegacyPlanRoute, () =>
            Results.Conflict(new
            {
                code = ErrorCodeLegacyPlanBypassDisabled,
                message = ErrorMessageLegacyPlanBypassDisabled
            }))
            .RequireAuthorization();

        endpoints.MapGet(LegacyPlanRoute, () =>
            Results.Conflict(new
            {
                code = ErrorCodeLegacyPlanBypassDisabled,
                message = ErrorMessageLegacyPlanReadDisabled
            }))
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
                return result.Status == StatusInvalidInput
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

                try
                {
                    var result = await proposals.ExecuteAsync(ownerUserId, request, cancellationToken)
                        .ConfigureAwait(false);
                    return Results.Ok(result);
                }
                catch (WorkoutPreviewAlreadyOpenException exception)
                {
                    return Results.Conflict(new { code = ErrorCodeWorkoutPreviewOpen, message = exception.Message });
                }
                catch (WorkoutPlanningInputsChangedException exception)
                {
                    return Results.Conflict(new { code = ErrorCodeWorkoutPlanningInputsChanged, message = exception.Message });
                }
            })
            .RequireAuthorization();

        endpoints.MapPost(StrengthProposalAcceptRoute, async (
                Guid proposalId,
                AcceptStrengthPlanProposalRequestV020 request,
                ClaimsPrincipal principal,
                [FromServices] AcceptStrengthPlanProposalV020UseCase proposals,
                CancellationToken cancellationToken) =>
            {
                if (!TryGetUserId(principal, out var ownerUserId))
                    return Results.Unauthorized();

                if (request.ProposalId != proposalId)
                    return Results.BadRequest();
                var result = await proposals.ExecuteAsync(ownerUserId, request, cancellationToken)
                    .ConfigureAwait(false);
                return result.Status switch
                {
                    StatusAccepted => Results.Ok(result),
                    StatusInvalidInput => Results.BadRequest(result),
                    StatusNotFound => Results.NotFound(result),
                    StatusConflict or StatusStale => Results.Conflict(result),
                    StatusLimited or StatusInvalidProposal => Results.UnprocessableEntity(result),
                    _ => Results.Conflict(result)
                };
            })
            .RequireAuthorization();

        endpoints.MapGet("/api/v1/workouts/plan/lease/active", async (ClaimsPrincipal principal,
            [FromServices] IStrengthPlanProposalStoreV020 store, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(await store.GetActivePreviewAsync(userId, cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization();

        endpoints.MapPost(LeaseActionRoute, async (
                string action, PlanningLeaseRequest request, ClaimsPrincipal principal,
                [FromServices] IStrengthPlanProposalStoreV020 store, CancellationToken cancellationToken) =>
            {
                if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
                var result = action switch
                {
                    LeaseActionRenew => await store.RenewAsync(userId, request, cancellationToken).ConfigureAwait(false),
                    LeaseActionClose => await store.CloseAsync(userId, request, cancellationToken).ConfigureAwait(false),
                    LeaseActionTakeover => await store.TakeoverAsync(userId, request.ProposalId, cancellationToken).ConfigureAwait(false),
                    _ => null
                };
                return result is null ? Results.NotFound() : Results.Ok(result);
            }).RequireAuthorization();

        endpoints.MapGet(AnswersRoute, async (ClaimsPrincipal principal,
            [FromServices] IStrengthPlanProposalStoreV020 store, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(await store.ReadAnswersAsync(userId, cancellationToken).ConfigureAwait(false));
        }).RequireAuthorization();

        endpoints.MapPut(AnswersRoute, async (SaveActivityAnswersRequest request,
            ClaimsPrincipal principal, [FromServices] IStrengthPlanProposalStoreV020 store, CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await store.SaveAnswersAsync(userId, request, cancellationToken).ConfigureAwait(false)); }
            catch (WorkoutPlanningInputsChangedException exception)
            { return Results.Conflict(new { code = ErrorCodeWorkoutPlanningInputsChanged, message = exception.Message }); }
            catch (InvalidOperationException exception)
            { return Results.Conflict(new { code = ErrorCodeWorkoutAnswersUnavailable, message = exception.Message }); }
        }).RequireAuthorization();

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
