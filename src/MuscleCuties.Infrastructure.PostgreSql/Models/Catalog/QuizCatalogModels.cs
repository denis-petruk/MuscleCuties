using MuscleCuties.Core.Models.Enums.Quiz;

namespace MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

/// <summary>Global, canonical quiz prompt. SeedCode is assigned by the reviewed identity manifest.</summary>
public sealed class QuizQuestion : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public string Question { get; set; } = null!;
    public int OrderIndex { get; set; }
    public QuizQuestionType QuestionType { get; set; }
    public ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
}

/// <summary>Global answer belonging to one canonical quiz prompt.</summary>
public sealed class QuizAnswer : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public int QuestionId { get; set; }
    public string Text { get; set; } = null!;
    public int OrderIndex { get; set; }
    public int MappedValue { get; set; }
    public QuizQuestion? Question { get; set; }
}
