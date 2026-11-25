namespace MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

/// <summary>Marks a catalog row that carries a seed code, which is what lets a seed run update rows instead of duplicating them.</summary>
public interface ICatalogSeedRecord
{
    int Id { get; set; }
    string SeedCode { get; set; }
}
