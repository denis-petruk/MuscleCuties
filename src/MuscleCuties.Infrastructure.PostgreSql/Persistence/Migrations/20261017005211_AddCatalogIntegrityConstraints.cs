using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_quiz_responses_quiz_answers_quiz_answer_id",
                schema: "app",
                table: "user_quiz_responses");

            migrationBuilder.DropForeignKey(
                name: "FK_user_quiz_responses_quiz_questions_quiz_question_id",
                schema: "app",
                table: "user_quiz_responses");

            migrationBuilder.DropIndex(
                name: "IX_user_quiz_responses_quiz_answer_id",
                schema: "app",
                table: "user_quiz_responses");

            migrationBuilder.DropIndex(
                name: "IX_user_quiz_responses_quiz_question_id",
                schema: "app",
                table: "user_quiz_responses");

            migrationBuilder.AddUniqueConstraint(
                name: "uq_quiz_answers_id_question_id",
                schema: "catalog",
                table: "quiz_answers",
                columns: new[] { "id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_quiz_responses_quiz_answer_id_quiz_question_id",
                schema: "app",
                table: "user_quiz_responses",
                columns: new[] { "quiz_answer_id", "quiz_question_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_food_items_fdc_seed_code_matches_id",
                schema: "catalog",
                table: "food_items",
                sql: "seed_code NOT LIKE 'fdc:%' OR (fdc_id IS NOT NULL AND seed_code = 'fdc:' || fdc_id::text)");

            migrationBuilder.AddForeignKey(
                name: "FK_user_quiz_responses_quiz_answers_quiz_answer_id_quiz_questi~",
                schema: "app",
                table: "user_quiz_responses",
                columns: new[] { "quiz_answer_id", "quiz_question_id" },
                principalSchema: "catalog",
                principalTable: "quiz_answers",
                principalColumns: new[] { "id", "question_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_quiz_responses_quiz_answers_quiz_answer_id_quiz_questi~",
                schema: "app",
                table: "user_quiz_responses");

            migrationBuilder.DropIndex(
                name: "IX_user_quiz_responses_quiz_answer_id_quiz_question_id",
                schema: "app",
                table: "user_quiz_responses");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_quiz_answers_id_question_id",
                schema: "catalog",
                table: "quiz_answers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_food_items_fdc_seed_code_matches_id",
                schema: "catalog",
                table: "food_items");

            migrationBuilder.CreateIndex(
                name: "IX_user_quiz_responses_quiz_answer_id",
                schema: "app",
                table: "user_quiz_responses",
                column: "quiz_answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_quiz_responses_quiz_question_id",
                schema: "app",
                table: "user_quiz_responses",
                column: "quiz_question_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_quiz_responses_quiz_answers_quiz_answer_id",
                schema: "app",
                table: "user_quiz_responses",
                column: "quiz_answer_id",
                principalSchema: "catalog",
                principalTable: "quiz_answers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_quiz_responses_quiz_questions_quiz_question_id",
                schema: "app",
                table: "user_quiz_responses",
                column: "quiz_question_id",
                principalSchema: "catalog",
                principalTable: "quiz_questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
