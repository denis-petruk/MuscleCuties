using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MuscleCuties.Infrastructure.PostgreSql.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes;
using MuscleCuties.UseCases.Features.Nutrition.Recipes.Contracts;

namespace MuscleCuties.Server.Nutrition;

/// <summary>The recipe routes: saving a recipe and reading the saved ones.</summary>
public static class RecipeEndpointMappings
{
    private const string NutritionRoute = "/api/v1/nutrition";

    /// <summary>Registers the recipe endpoints.</summary>
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(NutritionRoute).RequireAuthorization();

        group.MapGet("/recipes", async (
            int? mealType,
            ClaimsPrincipal principal,
            [FromServices] IGetRecipesAndTemplatesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await useCase.ExecuteAsync(userId, mealType, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        });

        group.MapPost("/recipes", async (
            CreateRecipeRequest request,
            ClaimsPrincipal principal,
            [FromServices] ICreateRecipeUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var result = await useCase.ExecuteAsync(userId, request, cancellationToken).ConfigureAwait(false);
                return Results.Created($"/api/v1/nutrition/recipes/{result.Id}", result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/recipes/{id:guid}", async (
            Guid id,
            UpdateRecipeRequest request,
            ClaimsPrincipal principal,
            [FromServices] ICreateRecipeUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var result = await useCase.UpdateAsync(
                    userId,
                    request with { RecipeId = id },
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(result);
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
        });

        group.MapGet("/recipes/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            [FromServices] PostgresRecipeStore store,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await store.GetRecipeByIdAsync(userId, id, cancellationToken).ConfigureAwait(false);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapDelete("/recipes/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            [FromServices] PostgresRecipeStore store,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            await store.DeleteRecipeAsync(userId, id, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        group.MapPost("/templates", async (
            SaveMealAsTemplateRequest request,
            ClaimsPrincipal principal,
            [FromServices] ISaveMealTemplateUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var result = await useCase.ExecuteAsync(userId, request, cancellationToken).ConfigureAwait(false);
                return Results.Created($"/api/v1/nutrition/templates/{result.Id}", result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/templates/{id:guid}", async (
            Guid id,
            SaveMealAsTemplateRequest request,
            ClaimsPrincipal principal,
            [FromServices] ISaveMealTemplateUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var result = await useCase.UpdateAsync(userId, id, request, cancellationToken).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/templates/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            [FromServices] PostgresRecipeStore store,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await store.GetMealTemplateByIdAsync(userId, id, cancellationToken).ConfigureAwait(false);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapDelete("/templates/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            [FromServices] PostgresRecipeStore store,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            await store.DeleteMealTemplateAsync(userId, id, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        return endpoints;
    }

}
