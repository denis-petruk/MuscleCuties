using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;
using MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts;
using LegacyBatchResponse = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.BatchSaveNutritionDiaryResponse;
using LegacyCopyResponse = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.CopyNutritionDiaryResponse;
using LegacyQuickAddResponse = MuscleCuties.UseCases.Features.Nutrition.Diary.Contracts.QuickAddNutritionDiaryResponse;

namespace MuscleCuties.Server.Nutrition;

public static class NutritionBatchAndCopyEndpointMappings
{
    private const string DiaryRoute = "/api/v1/nutrition/diary";

    public static IEndpointRouteBuilder MapNutritionBatchAndCopyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(DiaryRoute).RequireAuthorization();

        group.MapPost("/batch", async (
            JsonElement body,
            HttpContext context,
            [FromServices] NutritionBatchAndCopyUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var userId))
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
            if (!TryGetUserId(context.User, out var userId))
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

        group.MapPost("/copy/preview", async (
            PreviewNutritionCopyRequest request,
            HttpContext context,
            [FromServices] NutritionCopyPreviewUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var userId))
                return Results.Unauthorized();
            try
            {
                var response = await useCase.PreviewAsync(userId, request, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
        });

        group.MapPost("/copy/commit", async (
            CommitPreviewedNutritionCopyRequest request,
            HttpContext context,
            [FromServices] NutritionCopyPreviewUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var userId))
                return Results.Unauthorized();
            try
            {
                var effectiveRequest = request with
                {
                    IdempotencyKey = IdempotencyKeyResolver.Resolve(context, request.IdempotencyKey)
                };
                var response = await useCase.CommitAsync(userId, effectiveRequest, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
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
        });

        group.MapPost("/quick-add", async (
            JsonElement body,
            HttpContext context,
            [FromServices] NutritionBatchAndCopyUseCase useCase,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(context.User, out var userId))
                return Results.Unauthorized();
            try
            {
                var legacy = HasProperty(body, "requestId");
                var request = legacy
                    ? ConvertLegacyQuickAdd(Deserialize<QuickAddNutritionDiaryEntry>(body), timeProvider.GetUtcNow())
                    : Deserialize<QuickAddNutritionDiaryRequest>(body) with { LegacyTimestampUnspecified = false };
                var effectiveRequest = request with
                {
                    IdempotencyKey = IdempotencyKeyResolver.Resolve(context, request.IdempotencyKey)
                };
                var response = await useCase.QuickAddAsync(userId, effectiveRequest, cancellationToken)
                    .ConfigureAwait(false);
                return legacy
                    ? Results.Ok(new LegacyQuickAddResponse(response.Identity))
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

    private static bool HasProperty(JsonElement body, string propertyName) =>
        body.ValueKind == JsonValueKind.Object && body.EnumerateObject()
            .Any(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));

    private static T Deserialize<T>(JsonElement body) =>
        body.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new ArgumentException("A request body is required.");

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
                (MuscleCuties.Core.Models.Enums.Nutrition.MealType)(int)entry.MealType,
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

    private static CopyNutritionDiaryRequest ConvertLegacyCopy(CopyNutritionDiary request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty)
            throw new ArgumentException("A non-empty request ID is required.", nameof(request));
        return new CopyNutritionDiaryRequest(
            $"{NutritionDiaryDefaults.CopyCommandPrefix}{request.RequestId:D}",
            request.SourceDate,
            request.TargetDate,
            request.MealType.HasValue ? (MuscleCuties.Core.Models.Enums.Nutrition.MealType)(int)request.MealType.Value : null);
    }

    private static QuickAddNutritionDiaryRequest ConvertLegacyQuickAdd(
        QuickAddNutritionDiaryEntry request,
        DateTimeOffset currentUtcTime)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty)
            throw new ArgumentException("A non-empty request ID is required.", nameof(request));
        return new QuickAddNutritionDiaryRequest(
            $"{NutritionDiaryDefaults.QuickAddCommandPrefix}{request.RequestId:D}",
            request.Date,
            (MuscleCuties.Core.Models.Enums.Nutrition.MealType)(int)request.MealType,
            string.IsNullOrWhiteSpace(request.Name) ? NutritionDiaryDefaults.DefaultQuickAddTitle : request.Name.Trim(),
            request.ServingSize ?? NutritionDiaryDefaults.DefaultQuickAddServingSize,
            string.IsNullOrWhiteSpace(request.ServingUnit) ? NutritionDiaryDefaults.DefaultQuickAddServingUnit : request.ServingUnit,
            request.ServingsCount ?? NutritionDiaryDefaults.DefaultQuickAddServingsCount,
            request.Calories,
            request.Protein ?? NutritionDiaryDefaults.DefaultQuickAddMacroGrams,
            request.Carbs ?? NutritionDiaryDefaults.DefaultQuickAddMacroGrams,
            request.Fat ?? NutritionDiaryDefaults.DefaultQuickAddMacroGrams,
            DateTime.SpecifyKind(
                request.Date.ToDateTime(TimeOnly.FromDateTime(currentUtcTime.UtcDateTime)),
                DateTimeKind.Utc),
            LegacyTimestampUnspecified: true);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
