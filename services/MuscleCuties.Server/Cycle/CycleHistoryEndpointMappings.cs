using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Cycle.History;

namespace MuscleCuties.Server.Cycle;

/// <summary>The cycle-history routes: saving a cycle, a symptom log and reading the history back.</summary>
public static class CycleHistoryEndpointMappings
{
    private const string Route = "/api/v1/cycle/history";

    /// <summary>Registers the cycle-history endpoints.</summary>
    public static IEndpointRouteBuilder MapCycleHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Route).RequireAuthorization();
        group.MapGet("", async (DateOnly from, DateOnly through, ClaimsPrincipal principal, [FromServices] CycleHistoryUseCase useCase, CancellationToken ct) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await useCase.LoadAsync(userId, from, through, ct).ConfigureAwait(false)); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(); }
        });
        group.MapPut("", async (SaveCycleBody body, ClaimsPrincipal principal, [FromServices] CycleHistoryUseCase useCase, CancellationToken ct) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            try
            {
                var cycleId = body.Id != Guid.Empty ? body.Id : Guid.CreateVersion7();
                var value = await useCase.SaveCycleAsync(new SaveCycleObservation(userId, cycleId, body.StartDate, body.EndDate), ct).ConfigureAwait(false);
                return value is null ? Results.NotFound() : Results.Ok(value);
            }
            catch (CycleHistoryIdConflictException) { return Results.Conflict(); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(); }
        });
        group.MapPut("/cycles", async (SaveCycleBody body, ClaimsPrincipal principal, [FromServices] CycleHistoryUseCase useCase, CancellationToken ct) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            try
            {
                var value = await useCase.SaveCycleAsync(new SaveCycleObservation(userId, body.Id, body.StartDate, body.EndDate), ct).ConfigureAwait(false);
                return value is null ? Results.NotFound() : Results.Ok(value);
            }
            catch (CycleHistoryIdConflictException) { return Results.Conflict(); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(); }
        });
        group.MapPut("/symptoms", async (SaveSymptomBody body, ClaimsPrincipal principal, [FromServices] CycleHistoryUseCase useCase, CancellationToken ct) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            try
            {
                var value = await useCase.SaveSymptomAsync(new SaveCycleSymptom(userId, body.Id, body.CycleId, body.Date, body.Type, body.Severity, body.Notes), ct).ConfigureAwait(false);
                return value is null ? Results.NotFound() : Results.Ok(value);
            }
            catch (CycleHistoryIdConflictException) { return Results.Conflict(); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(); }
        });
        group.MapDelete("/symptoms/{id:guid}", async (Guid id, ClaimsPrincipal principal, [FromServices] CycleHistoryUseCase useCase, CancellationToken ct) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await useCase.DeleteSymptomAsync(userId, id, ct).ConfigureAwait(false) ? Results.NoContent() : Results.NotFound();
        });
        return endpoints;
    }

    /// <summary>A cycle as the client knows it: its id (so a re-send is the same cycle) and its start and optional end.</summary>
    public sealed record SaveCycleBody(Guid Id, DateOnly StartDate, DateOnly? EndDate);
    /// <summary>One symptom on one day of a cycle, with its severity.</summary>
    public sealed record SaveSymptomBody(Guid Id, Guid CycleId, DateOnly Date, string Type, int Severity, string? Notes);
}
