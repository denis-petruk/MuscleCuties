using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.CheckInAndInjury;

namespace MuscleCuties.Server.CheckInAndInjury;

public static class CheckInAndInjuryEndpointMappings
{
    private const string CheckInsRoute = "/api/v1/check-ins";
    private const string InjuriesRoute = "/api/v1/injuries";

    public static IEndpointRouteBuilder MapCheckInAndInjuryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var checkInsGroup = endpoints.MapGroup(CheckInsRoute).RequireAuthorization();
        checkInsGroup.MapGet("", async (
            DateOnly from,
            DateOnly through,
            ClaimsPrincipal principal,
            [FromServices] CheckInAndInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();
            try
            {
                return Results.Ok(await useCase.LoadCheckInsAsync(userId, from, through, cancellationToken).ConfigureAwait(false));
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest();
            }
        });
        checkInsGroup.MapPut("", async (
            SaveDailyCheckInBody body,
            ClaimsPrincipal principal,
            [FromServices] CheckInAndInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();
            try
            {
                var record = await useCase.SaveCheckInAsync(
                    new SaveDailyCheckInRequest(
                        userId,
                        body.Id,
                        body.Date,
                        body.SleepHours,
                        body.StepsYesterday,
                        body.Energy,
                        body.Pain,
                        body.Bloating,
                        body.WeightKg,
                        body.SleepQuality,
                        body.Soreness,
                        body.PreviousDayLoad,
                        body.Phase,
                        body.Sleep3dAvg,
                        body.Steps7dAvg,
                        body.PlanningSleepQuality,
                        body.PlanningSoreness,
                        body.PlanningStress,
                        body.PlanningTimeZoneId),
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(record);
            }
            catch (CheckInAndInjuryIdConflictException)
            {
                return Results.Conflict();
            }
            catch (CheckInAndInjuryDateConflictException)
            {
                return Results.Conflict();
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest();
            }
        });
        checkInsGroup.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            [FromServices] CheckInAndInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();
            return await useCase.DeleteCheckInAsync(userId, id, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.NotFound();
        });

        var injuriesGroup = endpoints.MapGroup(InjuriesRoute).RequireAuthorization();
        injuriesGroup.MapGet("", async (
            bool? activeOnly,
            ClaimsPrincipal principal,
            [FromServices] CheckInAndInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();
            return Results.Ok(await useCase.LoadInjuriesAsync(userId, activeOnly ?? false, cancellationToken).ConfigureAwait(false));
        });
        injuriesGroup.MapPut("", async (
            SaveInjuryObservationBody body,
            ClaimsPrincipal principal,
            [FromServices] CheckInAndInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();
            try
            {
                var effectiveId = body.Id is not null && body.Id.Value != Guid.Empty
                    ? body.Id.Value
                    : Guid.CreateVersion7();
                var effectiveSiteFlag = body.SiteFlag ?? 0;
                if (effectiveSiteFlag == 0 && !string.IsNullOrWhiteSpace(body.Site) &&
                    Enum.TryParse<MuscleCuties.UseCases.Features.Workout.Injury.WorkoutInjurySite>(body.Site, true, out var parsedSite))
                {
                    effectiveSiteFlag = (int)parsedSite;
                }
                var effectiveDate = body.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

                var record = await useCase.SaveInjuryAsync(
                    new SaveInjuryObservationRequest(
                        userId,
                        effectiveId,
                        effectiveSiteFlag,
                        body.Status,
                        body.Since,
                        body.Pain,
                        effectiveDate),
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(record);
            }
            catch (CheckInAndInjuryIdConflictException)
            {
                return Results.Conflict();
            }
            catch (ActiveWorkoutPreventsInjuryException exception)
            {
                return Results.Conflict(new { code = "ActiveWorkoutPreventsInjury", message = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { code = "InjuryUpdateUnavailable", message = exception.Message });
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest();
            }
        });
        injuriesGroup.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            [FromServices] CheckInAndInjuryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();
            return await useCase.DeleteInjuryAsync(userId, id, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.NotFound();
        });
        injuriesGroup.MapDelete("/{id:int}", (int id) => Results.NotFound());

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;

    public sealed record SaveDailyCheckInBody(
        Guid Id,
        DateOnly Date,
        decimal? SleepHours,
        int? StepsYesterday,
        int? Energy,
        int? Pain,
        bool? Bloating,
        decimal? WeightKg = null,
        int? SleepQuality = null,
        int? Soreness = null,
        decimal? PreviousDayLoad = null,
        string? Phase = null,
        decimal? Sleep3dAvg = null,
        int? Steps7dAvg = null,
        int? PlanningSleepQuality = null,
        int? PlanningSoreness = null,
        int? PlanningStress = null,
        string? PlanningTimeZoneId = null);

    public sealed record SaveInjuryObservationBody(
        Guid? Id,
        int? SiteFlag,
        string Status,
        DateOnly Since,
        int Pain,
        DateOnly? Date = null,
        string? Site = null);
}
