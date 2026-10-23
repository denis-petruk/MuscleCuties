using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Dashboard.Overview;

namespace MuscleCuties.Server.Dashboard;

public static class DashboardOverviewEndpointMappings
{
    private const string DashboardRoute = "/api/v1/dashboard";

    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(DashboardRoute).RequireAuthorization();

        group.MapGet("/overview", async (
            ClaimsPrincipal principal,
            [FromServices] ServerDashboardOverviewUseCase useCase,
            [FromQuery] DateOnly? date,
            [FromQuery] string? timeZoneId,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var today = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var overview = await useCase.LoadOverviewAsync(userId, today, timeZoneId, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(overview);
        });

        group.MapGet("/calendar", async (
            ClaimsPrincipal principal,
            [FromServices] ServerDashboardOverviewUseCase useCase,
            [FromQuery] int? year,
            [FromQuery] int? month,
            [FromQuery] DateOnly? today,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var utcNow = DateTime.UtcNow;
            var targetYear = year ?? utcNow.Year;
            var targetMonth = month ?? utcNow.Month;
            var currentDay = today ?? DateOnly.FromDateTime(utcNow);

            try
            {
                var calendar = await useCase.LoadCalendarAsync(userId, targetYear, targetMonth, currentDay, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(calendar);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      principal.FindFirstValue("sub");

        return Guid.TryParse(subject, out userId) && userId != Guid.Empty;
    }
}
