namespace MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

public interface ICatalogSeedRecord
{
    int Id { get; set; }
    string SeedCode { get; set; }
}
