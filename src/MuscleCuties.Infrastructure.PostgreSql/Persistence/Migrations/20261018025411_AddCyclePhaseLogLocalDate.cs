using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCyclePhaseLogLocalDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "local_date",
                schema: "app",
                table: "cycle_phase_logs",
                type: "date",
                nullable: true);

            // Existing readers treated LoggedAt calendar boundaries as UTC. Preserve that established
            // projection convention, promote it to the explicit local-date column, then normalize the
            // timestamp to UTC midnight so all future writes and timestamp queries agree.
            migrationBuilder.Sql("UPDATE app.cycle_phase_logs SET local_date = (logged_at AT TIME ZONE 'UTC')::date");
            // Legacy same-day reads selected the row with the newest CreatedAt. Keep that exact
            // winner before enforcing one phase log per owner and calendar date. Id is only a
            // deterministic tie-breaker for legacy rows whose CreatedAt values are identical.
            migrationBuilder.Sql("""
                WITH ranked_phase_logs AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY user_id, local_date
                               ORDER BY created_at DESC, id DESC
                           ) AS duplicate_rank
                    FROM app.cycle_phase_logs
                )
                DELETE FROM app.cycle_phase_logs AS phase_log
                USING ranked_phase_logs AS ranked
                WHERE phase_log.id = ranked.id
                  AND ranked.duplicate_rank > 1
                """);
            migrationBuilder.Sql("UPDATE app.cycle_phase_logs SET logged_at = local_date::timestamp AT TIME ZONE 'UTC'");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "local_date",
                schema: "app",
                table: "cycle_phase_logs",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_cycle_phase_logs_owner_local_date",
                schema: "app",
                table: "cycle_phase_logs",
                columns: new[] { "user_id", "local_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_cycle_phase_logs_owner_local_date",
                schema: "app",
                table: "cycle_phase_logs");

            migrationBuilder.DropColumn(
                name: "local_date",
                schema: "app",
                table: "cycle_phase_logs");
        }
    }
}
