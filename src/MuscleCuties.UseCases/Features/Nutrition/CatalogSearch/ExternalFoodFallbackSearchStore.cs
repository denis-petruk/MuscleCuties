using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;

/// <summary>A generic food returned by an external catalog, with macronutrients per 100 g.</summary>
public sealed record ExternalFoodItem(
    int FdcId,
    string Name,
    float CaloriesPer100Grams,
    float ProteinPer100Grams,
    float CarbsPer100Grams,
    float FatsPer100Grams,
    string? DataType);

/// <summary>External generic-food lookup. Implementations never receive user identifiers.</summary>
public interface IExternalFoodSource
{
    /// <summary>False when no credential is configured; the fallback is then skipped entirely.</summary>
    bool IsConfigured { get; }

    Task<IReadOnlyList<ExternalFoodItem>> SearchAsync(string genericQuery, CancellationToken cancellationToken = default);
}

/// <summary>Persists external foods as owner-scoped rows so they can be logged like any other food.</summary>
public interface IExternalFoodImportStore
{
    Task<IReadOnlyList<FoodCatalogCandidate>> ImportAsync(
        Guid ownerUserId,
        IReadOnlyList<ExternalFoodItem> items,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Adds external generic foods to a sparse catalog search. Only the fail-closed generic form of the
///     query leaves the service; an unavailable external source never fails the catalog search.
/// </summary>
public sealed class ExternalFoodFallbackSearchStore(
    IFoodCatalogSearchStore inner,
    IExternalFoodSource source,
    IExternalFoodImportStore importStore) : IFoodCatalogSearchStore
{
    public const int SparseResultThreshold = 5;
    public const int MaximumImportedPerSearch = 10;

    public Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        inner.SearchCandidatesAsync(query, cancellationToken);

    public async Task<IReadOnlyList<FoodCatalogCandidate>> SearchCandidatesAsync(
        string query,
        Guid? ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var candidates = await inner.SearchCandidatesAsync(query, ownerUserId, cancellationToken)
            .ConfigureAwait(false);
        if (ownerUserId is null || !source.IsConfigured || candidates.Count >= SparseResultThreshold)
            return candidates;

        var genericQuery = GenericFoodQueryRule.NormalizeOrEmpty(query);
        if (genericQuery.Length == 0)
            return candidates;

        var external = await source.SearchAsync(genericQuery, cancellationToken).ConfigureAwait(false);
        var knownFdcIds = candidates.Where(candidate => candidate.FdcId.HasValue)
            .Select(candidate => candidate.FdcId!.Value)
            .ToHashSet();
        var fresh = external
            .Where(item => item.FdcId > 0 && !string.IsNullOrWhiteSpace(item.Name) && knownFdcIds.Add(item.FdcId))
            .Take(MaximumImportedPerSearch)
            .ToList();
        if (fresh.Count == 0)
            return candidates;

        var imported = await importStore.ImportAsync(ownerUserId.Value, fresh, cancellationToken)
            .ConfigureAwait(false);
        return [.. candidates, .. imported];
    }
}
