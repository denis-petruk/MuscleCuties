using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261024160000_ReconcileWorkoutQuizInputsV010")]
public sealed class ReconcileWorkoutQuizInputsV010 : Migration
{
    private const int RetiredQuestionOrderOffset = 50_000;
    private const int CurrentCyclePhaseQuestionOrder = 1;
    private const int GoalQuestionOrder = 2;
    private const int GoalPaceQuestionOrder = 3;
    private const int ExperienceLevelQuestionOrder = 4;
    private const int AvailableWeekdaysQuestionOrder = 5;
    private const int DietaryPreferenceQuestionOrder = 6;
    private const int SessionDurationQuestionOrder = 7;
    private const int EquipmentQuestionOrder = 8;
    private const int MenstrualPainQuestionOrder = 9;
    private const int MenstrualEnergyQuestionOrder = 10;
    private const int FollicularPainQuestionOrder = 11;
    private const int FollicularEnergyQuestionOrder = 12;
    private const int OvulatoryPainQuestionOrder = 13;
    private const int OvulatoryEnergyQuestionOrder = 14;
    private const int LutealPainQuestionOrder = 15;
    private const int LutealEnergyQuestionOrder = 16;

    private const string LegacyWorkoutDaysQuestionSeedCode =
        "urn:musclecuties:seed:v1:f8ca7c8e-e348-4506-9f0a-981d2baa8a32";
    private const string SessionDurationQuestionSeedCode =
        "urn:musclecuties:seed:v1:1d8fe0cc-6093-4c6d-8f0e-301ab759bef6";
    private const string AvailableWeekdaysQuestionSeedCode =
        "urn:musclecuties:seed:v1:7f5bd296-1ba1-428c-8b42-9ca8251b4d33";
    private const string AvailableWeekdaysAnswerMondaySeedCode =
        "urn:musclecuties:seed:v1:4cadc12a-b17e-439e-b8b9-2e35aabcca10";
    private const string AvailableWeekdaysAnswerTuesdaySeedCode =
        "urn:musclecuties:seed:v1:e96a8f8e-6ad3-4082-a802-db517bfdfeb1";
    private const string AvailableWeekdaysAnswerWednesdaySeedCode =
        "urn:musclecuties:seed:v1:149955df-12ef-4ecb-ac5c-e5f14939c9af";
    private const string AvailableWeekdaysAnswerThursdaySeedCode =
        "urn:musclecuties:seed:v1:007f19a9-13a0-4d12-bd30-7e07aa7d152a";
    private const string AvailableWeekdaysAnswerFridaySeedCode =
        "urn:musclecuties:seed:v1:3462f8de-40bb-42b3-9f1e-969a51da9201";
    private const string AvailableWeekdaysAnswerSaturdaySeedCode =
        "urn:musclecuties:seed:v1:3939540a-12de-40aa-91cc-4479179252b9";
    private const string AvailableWeekdaysAnswerSundaySeedCode =
        "urn:musclecuties:seed:v1:e65171e1-ae22-421b-9dc4-485b6c1b49a5";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_quiz_questions_order_index",
            schema: "catalog",
            table: "quiz_questions");

        migrationBuilder.Sql($"""
            UPDATE catalog.quiz_questions
            SET question_type = 'RetiredLegacy',
                order_index = {RetiredQuestionOrderOffset} + id
            WHERE seed_code = '{LegacyWorkoutDaysQuestionSeedCode}';

            UPDATE catalog.quiz_questions
            SET question_type = 'SessionDuration',
                order_index = {SessionDurationQuestionOrder}
            WHERE seed_code = '{SessionDurationQuestionSeedCode}';

            UPDATE catalog.quiz_questions
            SET order_index = CASE question_type
                WHEN 'CurrentCyclePhase' THEN {CurrentCyclePhaseQuestionOrder}
                WHEN 'Goal' THEN {GoalQuestionOrder}
                WHEN 'GoalPace' THEN {GoalPaceQuestionOrder}
                WHEN 'ExperienceLevel' THEN {ExperienceLevelQuestionOrder}
                WHEN 'DietaryPreference' THEN {DietaryPreferenceQuestionOrder}
                WHEN 'SessionDuration' THEN {SessionDurationQuestionOrder}
                WHEN 'Equipment' THEN {EquipmentQuestionOrder}
                WHEN 'MenstrualPain' THEN {MenstrualPainQuestionOrder}
                WHEN 'MenstrualEnergy' THEN {MenstrualEnergyQuestionOrder}
                WHEN 'FollicularPain' THEN {FollicularPainQuestionOrder}
                WHEN 'FollicularEnergy' THEN {FollicularEnergyQuestionOrder}
                WHEN 'OvulatoryPain' THEN {OvulatoryPainQuestionOrder}
                WHEN 'OvulatoryEnergy' THEN {OvulatoryEnergyQuestionOrder}
                WHEN 'LutealPain' THEN {LutealPainQuestionOrder}
                WHEN 'LutealEnergy' THEN {LutealEnergyQuestionOrder}
                ELSE order_index
            END
            WHERE question_type <> 'RetiredLegacy';

            INSERT INTO catalog.quiz_questions (seed_code, question, order_index, question_type)
            VALUES ('{AvailableWeekdaysQuestionSeedCode}', 'Which days of the week are you available to train?',
                    {AvailableWeekdaysQuestionOrder}, 'AvailableWeekdays')
            ON CONFLICT (seed_code) DO UPDATE
                SET question = EXCLUDED.question,
                    order_index = EXCLUDED.order_index,
                    question_type = EXCLUDED.question_type;

            INSERT INTO catalog.quiz_answers (seed_code, question_id, text, order_index, mapped_value)
            SELECT answer.seed_code, question.id, answer.answer_text, answer.answer_order, answer.mapped_value
            FROM (VALUES
                ('{AvailableWeekdaysAnswerMondaySeedCode}', 'Monday', 1, 1),
                ('{AvailableWeekdaysAnswerTuesdaySeedCode}', 'Tuesday', 2, 2),
                ('{AvailableWeekdaysAnswerWednesdaySeedCode}', 'Wednesday', 3, 3),
                ('{AvailableWeekdaysAnswerThursdaySeedCode}', 'Thursday', 4, 4),
                ('{AvailableWeekdaysAnswerFridaySeedCode}', 'Friday', 5, 5),
                ('{AvailableWeekdaysAnswerSaturdaySeedCode}', 'Saturday', 6, 6),
                ('{AvailableWeekdaysAnswerSundaySeedCode}', 'Sunday', 7, 0)
            ) AS answer(seed_code, answer_text, answer_order, mapped_value)
            CROSS JOIN catalog.quiz_questions AS question
            WHERE question.seed_code = '{AvailableWeekdaysQuestionSeedCode}'
            ON CONFLICT (seed_code) DO UPDATE
                SET question_id = EXCLUDED.question_id,
                    text = EXCLUDED.text,
                    order_index = EXCLUDED.order_index,
                    mapped_value = EXCLUDED.mapped_value;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_quiz_questions_order_index",
            schema: "catalog",
            table: "quiz_questions",
            column: "order_index",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_quiz_questions_order_index",
            schema: "catalog",
            table: "quiz_questions");

        migrationBuilder.Sql($"""
            DELETE FROM app.user_quiz_responses response
            USING catalog.quiz_answers answer
            WHERE response.quiz_answer_id = answer.id
              AND answer.seed_code IN ('{AvailableWeekdaysAnswerMondaySeedCode}',
                  '{AvailableWeekdaysAnswerTuesdaySeedCode}', '{AvailableWeekdaysAnswerWednesdaySeedCode}',
                  '{AvailableWeekdaysAnswerThursdaySeedCode}', '{AvailableWeekdaysAnswerFridaySeedCode}',
                  '{AvailableWeekdaysAnswerSaturdaySeedCode}', '{AvailableWeekdaysAnswerSundaySeedCode}');

            DELETE FROM catalog.quiz_answers
            WHERE seed_code IN ('{AvailableWeekdaysAnswerMondaySeedCode}',
                '{AvailableWeekdaysAnswerTuesdaySeedCode}', '{AvailableWeekdaysAnswerWednesdaySeedCode}',
                '{AvailableWeekdaysAnswerThursdaySeedCode}', '{AvailableWeekdaysAnswerFridaySeedCode}',
                '{AvailableWeekdaysAnswerSaturdaySeedCode}', '{AvailableWeekdaysAnswerSundaySeedCode}');

            DELETE FROM catalog.quiz_questions WHERE seed_code = '{AvailableWeekdaysQuestionSeedCode}';

            UPDATE catalog.quiz_questions
            SET question_type = 'WorkoutDaysPerWeek', order_index = {AvailableWeekdaysQuestionOrder}
            WHERE seed_code = '{LegacyWorkoutDaysQuestionSeedCode}';
            """);

        migrationBuilder.CreateIndex(
            name: "IX_quiz_questions_order_index",
            schema: "catalog",
            table: "quiz_questions",
            column: "order_index",
            unique: true);
    }
}
