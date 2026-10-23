using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.Catalog;

public sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("quiz_questions", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.Question).HasColumnName("question").IsRequired().HasMaxLength(CatalogColumnLimits.QuizQuestion);
        builder.Property(x => x.OrderIndex).HasColumnName("order_index").IsRequired();
        builder.Property(x => x.QuestionType).HasColumnName("question_type").HasConversion<string>().IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => x.OrderIndex).IsUnique();
        builder.HasMany(x => x.Answers).WithOne(x => x.Question).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class QuizAnswerConfiguration : IEntityTypeConfiguration<QuizAnswer>
{
    public void Configure(EntityTypeBuilder<QuizAnswer> builder)
    {
        builder.ToTable("quiz_answers", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.QuestionId).HasColumnName("question_id").IsRequired();
        builder.Property(x => x.Text).HasColumnName("text").IsRequired().HasMaxLength(CatalogColumnLimits.QuizAnswer);
        builder.Property(x => x.OrderIndex).HasColumnName("order_index").IsRequired();
        builder.Property(x => x.MappedValue).HasColumnName("mapped_value").IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => new { x.QuestionId, x.OrderIndex }).IsUnique();
        builder.HasAlternateKey(x => new { x.Id, x.QuestionId }).HasName("uq_quiz_answers_id_question_id");
        builder.HasOne(x => x.Question).WithMany(x => x.Answers).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}
