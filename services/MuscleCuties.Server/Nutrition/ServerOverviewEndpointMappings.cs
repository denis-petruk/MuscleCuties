using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Cycle.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview;
using MuscleCuties.UseCases.Features.Nutrition.Overview.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.Targets;

namespace MuscleCuties.Server.Nutrition;

/// <summary>Separate route module; ApiApplication composition is owned by the coordinator.</summary>
public static class ServerOverviewEndpointMappings
{
    public static IEndpointRouteBuilder MapServerOverviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var nutrition = endpoints.MapGroup("/api/v1/nutrition").RequireAuthorization();

        nutrition.MapGet("/targets", async (DateOnly date, ClaimsPrincipal principal,
            [FromServices] ServerNutritionTargetProjectionUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await useCase.LoadAsync(userId, date, cancellationToken).ConfigureAwait(false)); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(); }
        });

        nutrition.MapGet("/overview", async (DateOnly date, string? breakfastPreference, ClaimsPrincipal principal,
            [FromServices] ServerNutritionOverviewUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await useCase.GetOverviewAsync(userId, date, breakfastPreference ?? "Savoury", cancellationToken).ConfigureAwait(false)); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(); }
        });

        nutrition.MapGet("/meal-plan", async (DateOnly date, string? breakfastPreference, ClaimsPrincipal principal,
            [FromServices] ServerNutritionOverviewUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await useCase.GetDayMealPlanAsync(userId, date, breakfastPreference ?? "Savoury", cancellationToken).ConfigureAwait(false)); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(); }
        });

        nutrition.MapPost("/meal-plan/refresh", async ([FromBody] DayMealPlanItemDto item, string? breakfastPreference, ClaimsPrincipal principal,
            [FromServices] ServerNutritionOverviewUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await useCase.RefreshDayMealAsync(userId, item, breakfastPreference ?? "Savoury", cancellationToken).ConfigureAwait(false)); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(); }
        });

        nutrition.MapGet("/suggestions", async (string mealType, string? breakfastPreference, ClaimsPrincipal principal,
            [FromServices] ServerNutritionOverviewUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await useCase.GetMealSuggestionsAsync(userId, mealType, breakfastPreference ?? "Savoury", null, cancellationToken).ConfigureAwait(false)); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(); }
        });

        nutrition.MapPost("/meals", async ([FromBody] LogMealRequest request, ClaimsPrincipal principal,
            [FromServices] ServerNutritionOverviewUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            try
            {
                await useCase.LogMealAsync(userId, request, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new { succeeded = true });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        nutrition.MapPut("/meals/{mealId:int}", async (int mealId, [FromBody] UpdateMealRequest request, ClaimsPrincipal principal,
            [FromServices] ServerNutritionOverviewUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            try
            {
                var normalized = request with { MealId = mealId };
                await useCase.UpdateMealAsync(userId, normalized, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new { succeeded = true });
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        nutrition.MapGet("/meals/{mealId:int}/ingredients", async (int mealId, ClaimsPrincipal principal,
            [FromServices] ServerNutritionOverviewUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            try
            {
                var ingredients = await useCase.GetMealIngredientsAsync(userId, mealId, cancellationToken).ConfigureAwait(false);
                return Results.Ok(ingredients);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest();
            }
        });

        nutrition.MapPost("/custom-foods", async ([FromBody] CreateCustomFoodRequest request, ClaimsPrincipal principal,
            [FromServices] ServerNutritionOverviewUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            try
            {
                var result = await useCase.CreateCustomFoodAsync(userId, request, cancellationToken).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        var cycle = endpoints.MapGroup("/api/v1/cycle").RequireAuthorization();
        cycle.MapGet("/overview", async (DateOnly from, DateOnly through, DateOnly today, ClaimsPrincipal principal,
            [FromServices] ServerCycleOverviewUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!TryUser(principal, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await useCase.LoadAsync(userId, from, through, today, cancellationToken).ConfigureAwait(false)); }
            catch (ArgumentOutOfRangeException) { return Results.BadRequest(); }
        });

        return endpoints;
    }

    private static bool TryUser(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
