using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyConcurrencyAndHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_daily_nutrition_logs_owner_local_date",
                schema: "app",
                table: "daily_nutrition_logs");

            migrationBuilder.AddColumn<int>(
                name: "concurrency_version",
                schema: "app",
                table: "user_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "command_idempotency",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_command_idempotency", x => x.id);
                    table.UniqueConstraint("uq_command_idempotency_user_id_id", x => new { x.user_id, x.id });
                    table.CheckConstraint("ck_command_idempotency_completion_pair", "(response_json IS NULL AND completed_at IS NULL) OR (response_json IS NOT NULL AND completed_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_command_idempotency_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workout_session_logs_owner_started_at_desc",
                schema: "app",
                table: "workout_session_logs",
                columns: new[] { "user_id", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_workout_logs_owner_date_desc",
                schema: "app",
                table: "workout_logs",
                columns: new[] { "user_id", "date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_symptom_logs_owner_date_desc",
                schema: "app",
                table: "symptom_logs",
                columns: new[] { "user_id", "date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_photo_journey_entries_owner_date_desc",
                schema: "app",
                table: "photo_journey_entries",
                columns: new[] { "user_id", "date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_daily_readiness_logs_owner_date_desc",
                schema: "app",
                table: "daily_readiness_logs",
                columns: new[] { "user_id", "date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_daily_nutrition_logs_owner_local_date_desc",
                schema: "app",
                table: "daily_nutrition_logs",
                columns: new[] { "user_id", "local_date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_cycle_phase_logs_owner_logged_at_desc",
                schema: "app",
                table: "cycle_phase_logs",
                columns: new[] { "user_id", "logged_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_cycle_logs_owner_start_date_desc",
                schema: "app",
                table: "cycle_logs",
                columns: new[] { "user_id", "start_date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_command_idempotency_user_id",
                schema: "app",
                table: "command_idempotency",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_command_idempotency_user_key",
                schema: "app",
                table: "command_idempotency",
                columns: new[] { "user_id", "command_key" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE app.command_idempotency ENABLE ROW LEVEL SECURITY;
                ALTER TABLE app.command_idempotency FORCE ROW LEVEL SECURITY;
                CREATE POLICY command_idempotency_tenant_policy ON app.command_idempotency
                    USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                    WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS command_idempotency_tenant_policy ON app.command_idempotency;
                ALTER TABLE app.command_idempotency NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE app.command_idempotency DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "command_idempotency",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_workout_session_logs_owner_started_at_desc",
                schema: "app",
                table: "workout_session_logs");

            migrationBuilder.DropIndex(
                name: "ix_workout_logs_owner_date_desc",
                schema: "app",
                table: "workout_logs");

            migrationBuilder.DropIndex(
                name: "ix_symptom_logs_owner_date_desc",
                schema: "app",
                table: "symptom_logs");

            migrationBuilder.DropIndex(
                name: "ix_photo_journey_entries_owner_date_desc",
                schema: "app",
                table: "photo_journey_entries");

            migrationBuilder.DropIndex(
                name: "ix_daily_readiness_logs_owner_date_desc",
                schema: "app",
                table: "daily_readiness_logs");

            migrationBuilder.DropIndex(
                name: "ix_daily_nutrition_logs_owner_local_date_desc",
                schema: "app",
                table: "daily_nutrition_logs");

            migrationBuilder.DropIndex(
                name: "ix_cycle_phase_logs_owner_logged_at_desc",
                schema: "app",
                table: "cycle_phase_logs");

            migrationBuilder.DropIndex(
                name: "ix_cycle_logs_owner_start_date_desc",
                schema: "app",
                table: "cycle_logs");

            migrationBuilder.DropColumn(
                name: "concurrency_version",
                schema: "app",
                table: "user_profiles");

            migrationBuilder.CreateIndex(
                name: "ix_daily_nutrition_logs_owner_local_date",
                schema: "app",
                table: "daily_nutrition_logs",
                columns: new[] { "user_id", "local_date" });
        }
    }
}
