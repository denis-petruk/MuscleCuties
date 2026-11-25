using MuscleCuties.Core.Models.Enums.Quiz;

namespace MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

/// <summary>Global, canonical quiz prompt. SeedCode is assigned by the reviewed identity manifest.</summary>
public sealed class QuizQuestion : ICatalogSeedRecord
{
    /// <summary>Primary key of the question.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The question text, sized to the column limit the client relies on.</summary>
    public string Question { get; set; } = null!;
    /// <summary>Position in the quiz, unique because the quiz is presented in one fixed order.</summary>
    public int OrderIndex { get; set; }
    /// <summary>Which profile field the answer feeds, stored as text.</summary>
    public QuizQuestionType QuestionType { get; set; }
    /// <summary>The options offered for it.</summary>
    public ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
}

/// <summary>Global answer belonging to one canonical quiz prompt.</summary>
public sealed class QuizAnswer : ICatalogSeedRecord
{
    /// <summary>Primary key of the option.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The question it answers.</summary>
    public int QuestionId { get; set; }
    /// <summary>The option text shown to her.</summary>
    public string Text { get; set; } = null!;
    /// <summary>Position within the question, unique so the options render in one order.</summary>
    public int OrderIndex { get; set; }
    /// <summary>The number the answer writes into the profile, which is what makes the quiz data rather than text.</summary>
    public int MappedValue { get; set; }
    /// <summary>Navigation to the question.</summary>
    public QuizQuestion? Question { get; set; }
}
