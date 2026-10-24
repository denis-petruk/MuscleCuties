using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Cycle.PhaseLog;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.Server.Cycle;

public static class PhaseLogEndpointMappings
{
    private const string Route = "/api/v1/cycle/history/phases";
    private const string LegacyRoute = "/api/v1/cycle/phases";

    public static IEndpointRouteBuilder MapCyclePhaseLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        async Task<IResult> HandleSavePhaseLog(
            SavePhaseLogBody body,
            ClaimsPrincipal principal,
            [FromServices] ISaveCyclePhaseLogUseCase useCase,
            CancellationToken cancellationToken)
        {
            if (!TryUser(principal, out var userId))
                return Results.Unauthorized();

            if (!TryParsePhase(body.Phase, out var phase))
                return Results.BadRequest();

            var effectiveDate = body.LocalDate ?? body.Date;
            if (!effectiveDate.HasValue)
                return Results.BadRequest();

            FlowIntensityDto? flow = null;
            if (body.FlowIntensity is not null)
            {
                if (!TryParseFlow(body.FlowIntensity, out var parsedFlow))
                    return Results.BadRequest();
                flow = parsedFlow;
            }

            var result = await useCase.ExecuteAsync(
                new SaveCyclePhaseLogInput(userId.ToString("D"), effectiveDate.Value, phase, flow, body.Note),
                cancellationToken).ConfigureAwait(false);

            return result.Outcome switch
            {
                SaveCyclePhaseLogOutcomeKind.Saved => Results.Ok(new PhaseLogResponse("Saved")),
                SaveCyclePhaseLogOutcomeKind.InvalidInput => Results.BadRequest(),
                SaveCyclePhaseLogOutcomeKind.PhaseOrderConflict => Results.Conflict(new PhaseOrderConflictResponse(result.SuggestedPhase?.ToString())),
                // Store Conflict means a generic persistence/constraint conflict (including an opaque owner/date collision).
                SaveCyclePhaseLogOutcomeKind.Conflict => Results.Conflict(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        endpoints.MapPut(Route, HandleSavePhaseLog).RequireAuthorization();
        endpoints.MapPut(LegacyRoute, HandleSavePhaseLog).RequireAuthorization();

        return endpoints;
    }

    private static bool TryUser(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;

    private static bool TryParsePhase(string? value, out CyclePhaseDto phase)
    {
        phase = default;
        return value is not null && Enum.GetNames<CyclePhaseDto>().Contains(value, StringComparer.OrdinalIgnoreCase) &&
            Enum.TryParse(value, ignoreCase: true, out phase);
    }

    private static bool TryParseFlow(string? value, out FlowIntensityDto flow)
    {
        flow = default;
        return value is not null && Enum.GetNames<FlowIntensityDto>().Contains(value, StringComparer.OrdinalIgnoreCase) &&
            Enum.TryParse(value, ignoreCase: true, out flow);
    }

    public sealed record SavePhaseLogBody(DateOnly? LocalDate, string? Phase, string? FlowIntensity, string? Note, DateOnly? Date = null);
    public sealed record PhaseLogResponse(string Outcome);
    public sealed record PhaseOrderConflictResponse(string? SuggestedPhase);
}
