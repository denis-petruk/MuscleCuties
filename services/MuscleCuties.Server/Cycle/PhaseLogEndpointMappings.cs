using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Cycle.PhaseLog;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.Server.Cycle;

/// <summary>The phase-log routes: recording a day's phase and flow, and the ordering check behind them.</summary>
public static class PhaseLogEndpointMappings
{
    private const string Route = "/api/v1/cycle/history/phases";

    /// <summary>Registers the phase-log endpoints.</summary>
    public static IEndpointRouteBuilder MapCyclePhaseLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        /// <summary>Saves one day's cycle phase and flow for the authenticated caller.</summary>
        async Task<IResult> HandleSavePhaseLog(
            SavePhaseLogBody body,
            ClaimsPrincipal principal,
            [FromServices] ISaveCyclePhaseLogUseCase useCase,
            CancellationToken cancellationToken)
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
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

        return endpoints;
    }


    /// <summary>Reads the phase name the client sent, reporting failure instead of guessing a default, so a typo cannot be</summary>
    /// saved as a real phase.</summary>
    private static bool TryParsePhase(string? value, out CyclePhaseDto phase)
    {
        phase = default;
        return value is not null && Enum.GetNames<CyclePhaseDto>().Contains(value, StringComparer.OrdinalIgnoreCase) &&
            Enum.TryParse(value, ignoreCase: true, out phase);
    }

    /// <summary>The same for flow intensity.</summary>
    private static bool TryParseFlow(string? value, out FlowIntensityDto flow)
    {
        flow = default;
        return value is not null && Enum.GetNames<FlowIntensityDto>().Contains(value, StringComparer.OrdinalIgnoreCase) &&
            Enum.TryParse(value, ignoreCase: true, out flow);
    }

    /// <summary>A day's phase log; the date is optional so the server's own day can be used, and the note is free text.</summary>
    public sealed record SavePhaseLogBody(DateOnly? LocalDate, string? Phase, string? FlowIntensity, string? Note, DateOnly? Date = null);
    /// <summary>The result of a phase write, as the code the client acts on.</summary>
    public sealed record PhaseLogResponse(string Outcome);
    /// <summary>A refused log that would break the phase order, carrying the phase that would have been accepted.</summary>
    public sealed record PhaseOrderConflictResponse(string? SuggestedPhase);
}
