using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;

namespace MuscleCuties.Server.Profile.PhotoVault;

public static class PhotoVaultEndpointMappings
{
    private const string PhotosRoute = "/api/v1/photos";

    public static IEndpointRouteBuilder MapPhotoVaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(PhotosRoute).RequireAuthorization();

        group.MapPost("/upload-url", async (
            [FromBody] PhotoUploadUrlRequest request,
            ClaimsPrincipal principal,
            [FromServices] ServerPhotoVaultUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var response = await useCase.RequestUploadUrlAsync(userId, request, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
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

        group.MapPost("/commit", async (
            [FromBody] CommitPhotoMetadataRequest request,
            ClaimsPrincipal principal,
            [FromServices] ServerPhotoVaultUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            try
            {
                var response = await useCase.CommitPhotoMetadataAsync(userId, request, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
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

        group.MapGet("", async (
            ClaimsPrincipal principal,
            [FromServices] ServerPhotoVaultUseCase useCase,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var safeLimit = limit.GetValueOrDefault(50);
            var safeOffset = offset.GetValueOrDefault(0);

            var history = await useCase.GetPhotoHistoryAsync(userId, safeLimit, safeOffset, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(history);
        });

        group.MapGet("/{photoId:guid}/download-url", async (
            Guid photoId,
            ClaimsPrincipal principal,
            [FromServices] ServerPhotoVaultUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var download = await useCase.GetPhotoDownloadUrlAsync(userId, photoId, cancellationToken)
                .ConfigureAwait(false);

            return download is null ? Results.NotFound() : Results.Ok(download);
        });

        group.MapDelete("/{photoId:guid}", async (
            Guid photoId,
            ClaimsPrincipal principal,
            [FromServices] ServerPhotoVaultUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var deleted = await useCase.DeletePhotoAsync(userId, photoId, cancellationToken)
                .ConfigureAwait(false);

            return deleted ? Results.NoContent() : Results.NotFound();
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
