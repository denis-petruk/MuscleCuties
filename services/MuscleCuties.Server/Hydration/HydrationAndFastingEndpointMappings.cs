using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MuscleCuties.UseCases.Features.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

namespace MuscleCuties.Server.Hydration;

/// <summary>The current user and day for the hydration routes, read from the request rather than passed by the client.</summary>
public sealed class HttpContextBodyMeasurementSession(IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider)
    : IBodyMeasurementSession
{
    public Guid CurrentUserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            return principal is not null && ApiApplication.TryGetUserId(principal, out var id) ? id : Guid.Empty;
        }
    }

    /// <summary>Today in UTC from the server clock, so a request without a date still lands on one day.</summary>
    public DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}

/// <summary>The hydration, water-preset and body-measurement routes.</summary>
public static class HydrationAndFastingEndpointMappings
{
    /// <summary>Registers the hydration, water-preset and body-measurement endpoints.</summary>
    public static IEndpointRouteBuilder MapHydrationAndProgressEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var hydration = endpoints.MapGroup("/api/v1/hydration").RequireAuthorization();

        hydration.MapGet("/presets", async (
            ClaimsPrincipal principal,
            [FromServices] GetWaterPresetsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(await useCase.ExecuteAsync(new GetWaterPresetsInputDto(userId), cancellationToken).ConfigureAwait(false));
        });

        hydration.MapPost("/presets/initialize", async (
            ClaimsPrincipal principal,
            [FromServices] InitializeWaterPresetsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(await useCase.ExecuteAsync(new InitializeWaterPresetsInputDto(userId), cancellationToken).ConfigureAwait(false));
        });

        hydration.MapPost("/presets", async (
            [FromBody] CreateWaterPresetBody body,
            ClaimsPrincipal principal,
            [FromServices] CreateWaterPresetUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(
                new CreateWaterPresetInputDto(userId, body.Label, body.Millilitres, body.OperationId, body.ExpectedVersion),
                cancellationToken)).ConfigureAwait(false);
        });

        hydration.MapPut("/presets/{presetId:guid}", async (
            Guid presetId,
            [FromBody] UpdateWaterPresetBody body,
            ClaimsPrincipal principal,
            [FromServices] UpdateWaterPresetUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(
                new UpdateWaterPresetInputDto(userId, presetId, body.Label, body.Millilitres, body.OperationId, body.ExpectedVersion),
                cancellationToken)).ConfigureAwait(false);
        });

        hydration.MapDelete("/presets/{presetId:guid}", async (
            Guid presetId,
            [FromQuery] Guid operationId,
            [FromQuery] string? expectedVersion,
            ClaimsPrincipal principal,
            [FromServices] DeleteWaterPresetUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(
                new DeleteWaterPresetInputDto(userId, presetId, operationId, expectedVersion),
                cancellationToken)).ConfigureAwait(false);
        });

        hydration.MapPut("/presets/reorder", async (
            [FromBody] ReorderWaterPresetsBody body,
            ClaimsPrincipal principal,
            [FromServices] ReorderWaterPresetsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(
                new ReorderWaterPresetsInputDto(userId, body.OrderedIds, body.OperationId, body.ExpectedVersion),
                cancellationToken)).ConfigureAwait(false);
        });

        hydration.MapGet("/intake", async (
            [FromQuery] DateOnly date,
            ClaimsPrincipal principal,
            [FromServices] GetWaterIntakeUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(new GetWaterIntakeInputDto(userId, date),
                cancellationToken)).ConfigureAwait(false);
        });

        hydration.MapPost("/intake", async (
            [FromBody] ChangeWaterIntakeBody body,
            ClaimsPrincipal principal,
            [FromServices] ChangeWaterIntakeUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(
                new ChangeWaterIntakeInputDto(userId, body.Date, body.ExpectedVersion, body.OperationId, body.EntryId, body.Kind, body.Millilitres),
                cancellationToken)).ConfigureAwait(false);
        });

        hydration.MapGet("/reminders", async (
            ClaimsPrincipal principal,
            [FromServices] GetWaterReminderConfigurationUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(await useCase.ExecuteAsync(new GetWaterReminderConfigurationInputDto(userId), cancellationToken).ConfigureAwait(false));
        });

        hydration.MapPut("/reminders", async (
            [FromBody] SaveWaterReminderBody body,
            ClaimsPrincipal principal,
            [FromServices] SaveWaterReminderConfigurationUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(
                new SaveWaterReminderConfigurationInputDto(userId, body.ExpectedVersion, body.OptedIn, body.LocalTimes, body.QuietStart, body.QuietEnd, body.OperationId),
                cancellationToken)).ConfigureAwait(false);
        });

        var measurements = endpoints.MapGroup("/api/v1/progress/measurements").RequireAuthorization();

        measurements.MapGet("", async (
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly through,
            ClaimsPrincipal principal,
            [FromServices] ReadBodyMeasurementHistoryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out _)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(new BodyMeasurementHistoryInputDto(from, through), cancellationToken)).ConfigureAwait(false);
        });

        measurements.MapPost("", async (
            [FromBody] CreateBodyMeasurementInputDto input,
            ClaimsPrincipal principal,
            [FromServices] CreateBodyMeasurementUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out _)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(input, cancellationToken)).ConfigureAwait(false);
        });

        measurements.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] CorrectBodyMeasurementInputDto input,
            ClaimsPrincipal principal,
            [FromServices] CorrectBodyMeasurementUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out _)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(input with { Id = id }, cancellationToken)).ConfigureAwait(false);
        });

        measurements.MapDelete("/{id:guid}", async (
            Guid id,
            [FromQuery] Guid commandId,
            [FromQuery] int expectedVersion,
            ClaimsPrincipal principal,
            [FromServices] DeleteBodyMeasurementUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out _)) return Results.Unauthorized();
            try
            {
                await useCase.ExecuteAsync(new DeleteBodyMeasurementInputDto(id, commandId, expectedVersion), cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        return endpoints;
    }

    /// <summary>Runs a hydration operation and turns a thrown failure into a problem response, so one bad request cannot take</summary>
    /// the endpoint down with a bare 500.</summary>
    private static async Task<IResult> ExecuteSafeAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return Results.Ok(await operation().ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }


    /// <summary>A new water preset, with the operation id that makes a retried create idempotent.</summary>
    public sealed record CreateWaterPresetBody(string Label, double Millilitres, Guid OperationId, string? ExpectedVersion);
    /// <summary>An existing preset's new label and volume, with its operation id.</summary>
    public sealed record UpdateWaterPresetBody(string Label, double Millilitres, Guid OperationId, string? ExpectedVersion);
    /// <summary>The preset order the client wants, with the operation id.</summary>
    public sealed record ReorderWaterPresetsBody(IReadOnlyList<Guid> OrderedIds, Guid OperationId, string? ExpectedVersion);
    /// <summary>A day's water intake with the version the client read, so two devices cannot silently overwrite each other.</summary>
    public sealed record ChangeWaterIntakeBody(DateOnly Date, string? ExpectedVersion, Guid OperationId, Guid EntryId, WaterIntakeChangeKindDto Kind, int Millilitres);
    /// <summary>The reminder settings, sent with the version that was read.</summary>
    public sealed record SaveWaterReminderBody(string? ExpectedVersion, bool OptedIn, IReadOnlyList<TimeOnly>? LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd, Guid OperationId);

}
