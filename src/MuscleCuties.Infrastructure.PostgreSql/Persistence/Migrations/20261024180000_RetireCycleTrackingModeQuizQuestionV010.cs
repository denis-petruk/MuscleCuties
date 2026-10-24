using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261024180000_RetireCycleTrackingModeQuizQuestionV010")]
public sealed class RetireCycleTrackingModeQuizQuestionV010 : Migration
{
    private const int RetiredQuestionOrderOffset = 1_000_000_000;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            UPDATE catalog.quiz_questions
            SET question_type = 'RetiredLegacy',
                order_index = order_index + {RetiredQuestionOrderOffset}
            WHERE question_type = 'CycleTrackingMode';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            UPDATE catalog.quiz_questions
            SET question_type = 'CycleTrackingMode',
                order_index = order_index - {RetiredQuestionOrderOffset}
            WHERE question_type = 'RetiredLegacy'
              AND order_index >= {RetiredQuestionOrderOffset};
            """);
    }
}
