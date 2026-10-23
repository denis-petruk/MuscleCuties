using System.Security.Claims;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.Diary;
using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;

namespace MuscleCuties.Server.Nutrition;

/// <summary>Maps authenticated unified nutrition diary read and manual write endpoints.</summary>
public static class NutritionDiaryEndpointMappings
{
    private const string DiaryRoute = "/api/v1/nutrition/diary";
    private const int DefaultDayPageSize = 50;
    private const int DefaultHistoryPageSize = 30;
    private const int DefaultEventPageSize = 50;

    public static IEndpointRouteBuilder MapNutritionDiaryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(DiaryRoute).RequireAuthorization();

        group.MapGet("/day", async (
            DateOnly date,
            int? pageSize,
            string? pageToken,
            ClaimsPrincipal principal,
            NutritionDiaryUseCase diary,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var ownerUserId))
                return Results.Unauthorized();
            try
            {
                var response = await diary.GetDayAsync(
                    ownerUserId,
                    date,
                    pageSize ?? DefaultDayPageSize,
                    pageToken,
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(response);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
        });

        group.MapGet("/entries/{itemId:guid}", async (
            Guid itemId,
            ClaimsPrincipal principal,
            NutritionDiaryUseCase diary,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var ownerUserId))
                return Results.Unauthorized();
            var entry = await diary.GetSnapshotEntryByIdAsync(ownerUserId, itemId, cancellationToken).ConfigureAwait(false);
            return entry is null ? Results.NotFound() : Results.Ok(entry);
        });

        group.MapGet("/history", async (
            DateOnly beforeDate,
            int? pageSize,
            ClaimsPrincipal principal,
            NutritionDiaryUseCase diary,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var ownerUserId))
                return Results.Unauthorized();
            var response = await diary.GetHistoryAsync(
                ownerUserId,
                beforeDate,
                pageSize ?? DefaultHistoryPageSize,
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        });

        group.MapGet("/events", async (
            long? afterCursor,
            int? pageSize,
            ClaimsPrincipal principal,
            INutritionDiaryCommandEventStore eventStore,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var ownerUserId))
                return Results.Unauthorized();
            try
            {
                var response = await eventStore.GetEventsAfterAsync(
                    ownerUserId,
                    afterCursor ?? 0,
                    pageSize ?? DefaultEventPageSize,
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(response);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
        });

        group.MapPut("/entries", async (
            SaveNutritionDiaryEntry request,
            ClaimsPrincipal principal,
            NutritionDiaryUseCase diary,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var ownerUserId))
                return Results.Unauthorized();
            try
            {
                var identity = await diary.SaveManualEntryAsync(ownerUserId, request, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new SaveNutritionDiaryEntryResponse(identity));
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
        });

        group.MapPut("/metadata", async (
            UpdateNutritionDiaryMetadata request,
            ClaimsPrincipal principal,
            NutritionDiaryUseCase diary,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var ownerUserId))
                return Results.Unauthorized();
            await diary.UpdateMetadataAsync(ownerUserId, request, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        group.MapDelete("/entries/{identity}", async (
            string identity,
            DateOnly date,
            ClaimsPrincipal principal,
            NutritionDiaryUseCase diary,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var ownerUserId))
                return Results.Unauthorized();
            try
            {
                var entry = new SaveNutritionDiaryEntry(
                    Identity: identity,
                    Date: date,
                    MealType: MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.MealType.Breakfast,
                    Name: "Deleted entry",
                    ServingSize: 1m,
                    ServingUnit: "serving",
                    ServingsCount: 1m,
                    Calories: 0m,
                    Protein: 0m,
                    Carbs: 0m,
                    Fat: 0m,
                    LoggedAtUtc: DateTime.UtcNow,
                    IsDeleted: true);
                await diary.SaveManualEntryAsync(ownerUserId, entry, cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
        });

        return endpoints;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;

    public sealed record SaveNutritionDiaryEntryResponse(string Identity);
}
