using MuscleCuties.UseCases.Features.Nutrition.Barcode;
using MuscleCuties.UseCases.Features.Nutrition.Barcode.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch.Contracts;
using MuscleCuties.UseCases.Features.Nutrition.CustomFoods;
using MuscleCuties.UseCases.Features.Nutrition.CustomFoods.Contracts;
using System.Security.Claims;

namespace MuscleCuties.Server.Nutrition;

/// <summary>Maps authenticated nutrition catalog lookup and owner-scoped custom-food endpoints.</summary>
public static class FoodCatalogEndpointMappings
{
    private const string CatalogRoute = "/api/v1/nutrition/catalog";

    public static IEndpointRouteBuilder MapFoodCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(CatalogRoute).RequireAuthorization();
        group.MapGet("/search", async (
            string query,
            int? pageSize,
            int? pageNumber,
            HttpContext context,
            IFoodCatalogSearchUseCase search,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var ownerUserId))
                return Results.Forbid();
            var response = await search.SearchAsync(new FoodCatalogSearchRequest(
                query,
                pageSize ?? FoodCatalogSearchDefaults.DefaultPageSize,
                pageNumber ?? FoodCatalogSearchDefaults.FirstPageNumber), ownerUserId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        });
        group.MapPost("/barcode", async (
            BarcodeLookupRequest request,
            HttpContext context,
            ILookupBarcodeUseCase lookup,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var ownerUserId))
                return Results.Forbid();
            var response = await lookup.ExecuteAsync(request, ownerUserId, cancellationToken).ConfigureAwait(false);
            return response.IsValidBarcode && response.FoodItem is not null
                ? Results.Ok(response)
                : Results.NotFound(response);
        });
        group.MapPost("/custom-foods", async (
            CreateCustomFoodRequest request,
            HttpContext context,
            ICreateCustomFoodUseCase create,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var ownerUserId))
                return Results.Forbid();
            try
            {
                var response = await create.ExecuteAsync(request, ownerUserId, cancellationToken).ConfigureAwait(false);
                return Results.Created($"{CatalogRoute}/custom-foods/{response.FoodKey}", response);
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });
        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
