using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;
using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;
using LegacyBatchResponse = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.BatchSaveNutritionDiaryResponse;
using LegacyCopyResponse = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.CopyNutritionDiaryResponse;

namespace MuscleCuties.Server.Nutrition;

/// <summary>The batch-save and copy-day routes, which are how a diary is written and duplicated.</summary>
public static class NutritionBatchAndCopyEndpointMappings
{
    private const string DiaryRoute = "/api/v1/nutrition/diary";

    /// <summary>Registers the batch and copy endpoints.</summary>
    public static IEndpointRouteBuilder MapNutritionBatchAndCopyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(DiaryRoute).RequireAuthorization();

        group.MapPost("/batch", async (
            JsonElement body,
            HttpContext context,
            [FromServices] NutritionBatchAndCopyUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(context.User, out var userId))
                return Results.Unauthorized();
            try
            {
                var legacy = HasProperty(body, "requestId");
                var request = legacy
                    ? ConvertLegacyBatch(Deserialize<BatchSaveNutritionDiaryEntries>(body))
                    : Deserialize<BatchSaveNutritionDiaryRequest>(body);
                var effectiveRequest = request with
                {
                    IdempotencyKey = IdempotencyKeyResolver.Resolve(context, request.IdempotencyKey)
                };
                var response = await useCase.BatchSaveAsync(userId, effectiveRequest, cancellationToken)
                    .ConfigureAwait(false);
                return legacy
                    ? Results.Ok(new LegacyBatchResponse(response.SavedIdentities))
                    : Results.Ok(response);
            }
            catch (PostgresIdempotencyConflictException)
            {
                return Results.Conflict();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }
        });

        group.MapPost("/copy", async (
            JsonElement body,
            HttpContext context,
            [FromServices] NutritionBatchAndCopyUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!ApiApplication.TryGetUserId(context.User, out var userId))
                return Results.Unauthorized();
            try
            {
                var legacy = HasProperty(body, "requestId");
                var request = legacy
                    ? ConvertLegacyCopy(Deserialize<CopyNutritionDiary>(body))
                    : Deserialize<CopyNutritionDiaryRequest>(body);
                var effectiveRequest = request with
                {
                    IdempotencyKey = IdempotencyKeyResolver.Resolve(context, request.IdempotencyKey)
                };
                var response = await useCase.CopyAsync(userId, effectiveRequest, cancellationToken)
                    .ConfigureAwait(false);
                return legacy
                    ? Results.Ok(new LegacyCopyResponse(response.CopiedCount, response.CopiedIdentities))
                    : Results.Ok(response);
            }
            catch (PostgresIdempotencyConflictException)
            {
                return Results.Conflict();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }
        });

        return endpoints;
    }

    /// <summary>True when the JSON body carries the named property, compared case-insensitively.</summary>
    private static bool HasProperty(JsonElement body, string propertyName) =>
        body.ValueKind == JsonValueKind.Object && body.EnumerateObject()
            .Any(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Deserializes a JSON body with web defaults and rejects an absent body.</summary>
    private static T Deserialize<T>(JsonElement body) =>
        body.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new ArgumentException("A request body is required.");

    /// <summary>Turns the older batch body into the current request shape, so a client built before the change keeps working.</summary>
    private static BatchSaveNutritionDiaryRequest ConvertLegacyBatch(BatchSaveNutritionDiaryEntries request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty)
            throw new ArgumentException("A non-empty request ID is required.", nameof(request));
        ArgumentNullException.ThrowIfNull(request.Entries);
        if (request.Entries.Any(entry => entry is null || entry.Date != request.Date))
            throw new ArgumentException("All entries in the batch must match the batch date.", nameof(request));

        return new BatchSaveNutritionDiaryRequest(
            $"{NutritionDiaryDefaults.BatchCommandPrefix}{request.RequestId:D}",
            request.Date,
            request.Entries.Where(entry => entry.IsDeleted).Select(entry => entry.Identity ?? string.Empty).ToArray(),
            request.Entries.Where(entry => !entry.IsDeleted).Select(entry => new BatchNutritionDiaryEntryInput(
                entry.Identity,
                entry.MealType,
                entry.Name,
                entry.ServingSize,
                entry.ServingUnit,
                entry.ServingsCount,
                entry.Calories,
                entry.Protein,
                entry.Carbs,
                entry.Fat,
                entry.LoggedAtUtc)).ToArray());
    }

    /// <summary>The same for the copy request.</summary>
    private static CopyNutritionDiaryRequest ConvertLegacyCopy(CopyNutritionDiary request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty)
            throw new ArgumentException("A non-empty request ID is required.", nameof(request));
        return new CopyNutritionDiaryRequest(
            $"{NutritionDiaryDefaults.CopyCommandPrefix}{request.RequestId:D}",
            request.SourceDate,
            request.TargetDate,
            request.MealType);
    }

}
