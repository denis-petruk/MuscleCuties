using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MuscleCuties.UseCases.Features.Fasting;
using MuscleCuties.UseCases.Features.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;

namespace MuscleCuties.Server.Hydration;

public sealed class HttpContextBodyMeasurementSession(IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider)
    : IBodyMeasurementSession
{
    public Guid CurrentUserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            var sub = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        }
    }

    public DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}

public static class HydrationAndFastingEndpointMappings
{
    public static IEndpointRouteBuilder MapHydrationProgressAndFastingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var hydration = endpoints.MapGroup("/api/v1/hydration").RequireAuthorization();

        hydration.MapGet("/presets", async (
            ClaimsPrincipal principal,
            [FromServices] GetWaterPresetsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(await useCase.ExecuteAsync(new GetWaterPresetsInputDto(userId), cancellationToken).ConfigureAwait(false));
        });

        hydration.MapPost("/presets/initialize", async (
            ClaimsPrincipal principal,
            [FromServices] InitializeWaterPresetsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(await useCase.ExecuteAsync(new InitializeWaterPresetsInputDto(userId), cancellationToken).ConfigureAwait(false));
        });

        hydration.MapPost("/presets", async (
            [FromBody] CreateWaterPresetBody body,
            ClaimsPrincipal principal,
            [FromServices] CreateWaterPresetUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
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
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
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
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
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
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
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
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(await useCase.ExecuteAsync(new GetWaterIntakeInputDto(userId, date), cancellationToken).ConfigureAwait(false));
        });

        hydration.MapPost("/intake", async (
            [FromBody] ChangeWaterIntakeBody body,
            ClaimsPrincipal principal,
            [FromServices] ChangeWaterIntakeUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(
                new ChangeWaterIntakeInputDto(userId, body.Date, body.ExpectedVersion, body.OperationId, body.EntryId, body.Kind, body.Millilitres),
                cancellationToken)).ConfigureAwait(false);
        });

        hydration.MapGet("/reminders", async (
            ClaimsPrincipal principal,
            [FromServices] GetWaterReminderConfigurationUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(await useCase.ExecuteAsync(new GetWaterReminderConfigurationInputDto(userId), cancellationToken).ConfigureAwait(false));
        });

        hydration.MapPut("/reminders", async (
            [FromBody] SaveWaterReminderBody body,
            ClaimsPrincipal principal,
            [FromServices] SaveWaterReminderConfigurationUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
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
            if (!TryGetUserId(principal, out _)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(new BodyMeasurementHistoryInputDto(from, through), cancellationToken)).ConfigureAwait(false);
        });

        measurements.MapPost("", async (
            [FromBody] CreateBodyMeasurementInputDto input,
            ClaimsPrincipal principal,
            [FromServices] CreateBodyMeasurementUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out _)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ExecuteAsync(input, cancellationToken)).ConfigureAwait(false);
        });

        measurements.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] CorrectBodyMeasurementInputDto input,
            ClaimsPrincipal principal,
            [FromServices] CorrectBodyMeasurementUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out _)) return Results.Unauthorized();
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
            if (!TryGetUserId(principal, out _)) return Results.Unauthorized();
            try
            {
                await useCase.ExecuteAsync(new DeleteBodyMeasurementInputDto(id, commandId, expectedVersion), cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        var fasting = endpoints.MapGroup("/api/v1/fasting").RequireAuthorization();

        fasting.MapGet("/session", async (
            [FromQuery] bool? clockUncertain,
            ClaimsPrincipal principal,
            [FromServices] FastingSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return Results.Ok(await useCase.GetStateAsync(userId, clockUncertain ?? false, cancellationToken).ConfigureAwait(false));
        });

        fasting.MapPost("/session/start", async (
            [FromBody] StartFastingBody body,
            ClaimsPrincipal principal,
            [FromServices] FastingSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.StartAsync(
                new StartFastingSessionInputDto(
                    userId,
                    body.SessionId == Guid.Empty ? Guid.CreateVersion7() : body.SessionId,
                    body.CommandId,
                    body.EligibilityResponse,
                    body.StartedAtUtc,
                    body.DisplayUtcOffsetMinutes,
                    body.PlannedEndAtUtc,
                    body.EnablePlannedEndReminder,
                    body.IsProfilePregnant),
                cancellationToken)).ConfigureAwait(false);
        });

        fasting.MapPost("/session/{sessionId:guid}/pause", async (
            Guid sessionId,
            [FromBody] TransitionFastingBody body,
            ClaimsPrincipal principal,
            [FromServices] FastingSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.PauseAsync(
                new TransitionFastingSessionInputDto(userId, sessionId, body.CommandId, body.ExpectedVersion, body.TransitionAtUtc),
                cancellationToken)).ConfigureAwait(false);
        });

        fasting.MapPost("/session/{sessionId:guid}/resume", async (
            Guid sessionId,
            [FromBody] TransitionFastingBody body,
            ClaimsPrincipal principal,
            [FromServices] FastingSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.ResumeAsync(
                new TransitionFastingSessionInputDto(userId, sessionId, body.CommandId, body.ExpectedVersion, body.TransitionAtUtc, body.EligibilityResponse, body.IsProfilePregnant),
                cancellationToken)).ConfigureAwait(false);
        });

        fasting.MapPost("/session/{sessionId:guid}/end", async (
            Guid sessionId,
            [FromBody] TransitionFastingBody body,
            ClaimsPrincipal principal,
            [FromServices] FastingSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.EndAsync(
                new TransitionFastingSessionInputDto(userId, sessionId, body.CommandId, body.ExpectedVersion, body.TransitionAtUtc),
                cancellationToken)).ConfigureAwait(false);
        });

        fasting.MapPut("/session/{sessionId:guid}/correction", async (
            Guid sessionId,
            [FromBody] CorrectFastingBody body,
            ClaimsPrincipal principal,
            [FromServices] FastingSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.CorrectTimesAsync(
                new CorrectFastingSessionInputDto(userId, sessionId, body.CommandId, body.ExpectedVersion, body.CorrectedStartedAtUtc, body.CorrectedEndedAtUtc, body.CorrectedAtUtc),
                cancellationToken)).ConfigureAwait(false);
        });

        fasting.MapPut("/session/{sessionId:guid}/planned-end", async (
            Guid sessionId,
            [FromBody] UpdateFastingPlannedEndBody body,
            ClaimsPrincipal principal,
            [FromServices] FastingSessionUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
            return await ExecuteSafeAsync(() => useCase.UpdatePlannedEndAsync(
                new UpdateFastingPlannedEndInputDto(userId, sessionId, body.CommandId, body.ExpectedVersion, body.PlannedEndAtUtc, body.EnablePlannedEndReminder),
                cancellationToken)).ConfigureAwait(false);
        });

        return endpoints;
    }

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

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return Guid.TryParse(subject, out userId) && userId != Guid.Empty;
    }

    public sealed record CreateWaterPresetBody(string Label, double Millilitres, Guid OperationId, string? ExpectedVersion);
    public sealed record UpdateWaterPresetBody(string Label, double Millilitres, Guid OperationId, string? ExpectedVersion);
    public sealed record ReorderWaterPresetsBody(IReadOnlyList<Guid> OrderedIds, Guid OperationId, string? ExpectedVersion);
    public sealed record ChangeWaterIntakeBody(DateOnly Date, string? ExpectedVersion, Guid OperationId, Guid EntryId, WaterIntakeChangeKindDto Kind, int Millilitres);
    public sealed record SaveWaterReminderBody(string? ExpectedVersion, bool OptedIn, IReadOnlyList<TimeOnly>? LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd, Guid OperationId);

    public sealed record StartFastingBody(
        Guid SessionId,
        Guid CommandId,
        FastingEligibilityResponseDto EligibilityResponse,
        DateTimeOffset StartedAtUtc,
        int DisplayUtcOffsetMinutes,
        DateTimeOffset? PlannedEndAtUtc,
        bool EnablePlannedEndReminder,
        bool? IsProfilePregnant);

    public sealed record TransitionFastingBody(
        Guid CommandId,
        int ExpectedVersion,
        DateTimeOffset TransitionAtUtc,
        FastingEligibilityResponseDto EligibilityResponse = FastingEligibilityResponseDto.YesAllApply,
        bool? IsProfilePregnant = null);

    public sealed record CorrectFastingBody(
        Guid CommandId,
        int ExpectedVersion,
        DateTimeOffset CorrectedStartedAtUtc,
        DateTimeOffset? CorrectedEndedAtUtc,
        DateTimeOffset CorrectedAtUtc);

    public sealed record UpdateFastingPlannedEndBody(
        Guid CommandId,
        int ExpectedVersion,
        DateTimeOffset? PlannedEndAtUtc,
        bool EnablePlannedEndReminder);
}
