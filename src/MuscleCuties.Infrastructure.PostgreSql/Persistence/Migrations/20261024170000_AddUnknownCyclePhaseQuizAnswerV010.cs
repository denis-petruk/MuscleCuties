using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261024170000_AddUnknownCyclePhaseQuizAnswerV010")]
public sealed class AddUnknownCyclePhaseQuizAnswerV010 : Migration
{
    private const int UnknownCyclePhaseAnswerOrder = 5;
    private const int UnknownCyclePhaseMappedValue = 4;
    private const string UnknownCyclePhaseAnswerText = "Not sure";
    private const string CurrentCyclePhaseQuestionSeedCode =
        "urn:musclecuties:seed:v1:04fdd745-0da2-42fd-bc68-0f52b1e60486";
    private const string UnknownCyclePhaseAnswerSeedCode =
        "urn:musclecuties:seed:v1:4f26c8bc-cfb3-4c6d-a307-6c6e3ef17b75";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            INSERT INTO catalog.quiz_answers (seed_code, question_id, text, order_index, mapped_value)
            SELECT '{UnknownCyclePhaseAnswerSeedCode}', question.id, '{UnknownCyclePhaseAnswerText}',
                   {UnknownCyclePhaseAnswerOrder}, {UnknownCyclePhaseMappedValue}
            FROM catalog.quiz_questions AS question
            WHERE question.seed_code = '{CurrentCyclePhaseQuestionSeedCode}'
            ON CONFLICT (seed_code) DO UPDATE
                SET question_id = EXCLUDED.question_id,
                    text = EXCLUDED.text,
                    order_index = EXCLUDED.order_index,
                    mapped_value = EXCLUDED.mapped_value;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            DELETE FROM app.user_quiz_responses AS response
            USING catalog.quiz_answers AS answer
            WHERE response.quiz_answer_id = answer.id
              AND answer.seed_code = '{UnknownCyclePhaseAnswerSeedCode}';

            DELETE FROM catalog.quiz_answers
            WHERE seed_code = '{UnknownCyclePhaseAnswerSeedCode}';
            """);
    }
}
